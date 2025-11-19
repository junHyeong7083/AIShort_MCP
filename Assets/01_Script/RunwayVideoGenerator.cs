using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class RunwayVideoGenerator : MonoBehaviour
{
    [Header("API Config")]
    public API apiConfig;              // Runway_API 들어있는 SO

    [Header("UI References")]
    public Button selectPathButton;    // "저장 위치 설정" 버튼
    public Button generateButton;      // "영상 생성" 버튼
    public Text filePathText;          // 선택된 경로 표시
    public Text statusText;            // 상태/로그 표시

    [Header("Prompt Source")]
    [Tooltip("2차 가공된 Storyboard/Prompt가 들어있는 텍스트 (refineOutputText)")]
    public Text refineOutputText;      // 여기 내용으로 Runway 호출

    [Header("Runway Options")]
    [Tooltip("Runway Text-To-Video 엔드포인트")]
    public string textToVideoEndpoint = "https://api.dev.runwayml.com/v1/text_to_video";

    [Tooltip("Runway API Version 헤더 값 (공식 문서 보고 최신값으로 교체 가능)")]
    public string runwayApiVersion = "2024-11-06";

    [Tooltip("문서 기준 허용 값: gen3a_turbo, gen4.5, veo3, veo3.1, veo3.1_fast 중 하나")]
    public string runwayModel = "veo3.1";   // 🔹 기본값을 문서 예제랑 동일하게

    [Tooltip("해상도 비율 (예: 1280:720, 1920:1080, 1080:1920 등)")]
    public string ratio = "1280:720";

    [Tooltip("영상 길이(초)")]
    public int durationSeconds = 4;

    [Tooltip("오디오 포함 여부 (문서 예제: true/false)")]
    public bool includeAudio = false;

    // 선택된 파일 전체 경로 (C:\...\myvideo.mp4)
    private string _saveFilePath;

    #region 내부 Runway 응답용 클래스

    // 🔹 문서 예제 JSON과 필드 이름/구조를 맞춤
    [Serializable]
    private class RunwayTextToVideoRequest
    {
        public string promptText;
        public string ratio;
        public bool audio;
        public int duration;
        public string model;
    }

    [Serializable]
    private class RunwayTaskCreateResponse
    {
        public string id;
        public string status;
    }

    [Serializable]
    private class RunwayTaskStatusResponse
    {
        public string id;
        public string status;
        public string[] output;
    }

    #endregion

    private void Awake()
    {
        if (selectPathButton != null)
            selectPathButton.onClick.AddListener(OnClickSelectPath);
        if (generateButton != null)
            generateButton.onClick.AddListener(OnClickGenerate);
    }

    private void OnDestroy()
    {
        if (selectPathButton != null)
            selectPathButton.onClick.RemoveListener(OnClickSelectPath);
        if (generateButton != null)
            generateButton.onClick.RemoveListener(OnClickGenerate);
    }

    // ==============================
    // 1) 저장 위치 선택
    // ==============================
    private void OnClickSelectPath()
    {
#if UNITY_EDITOR
        string defaultName = "runway_video.mp4";

        string path = UnityEditor.EditorUtility.SaveFilePanel(
            "영상 저장 위치 선택",
            "",
            defaultName,
            "mp4"
        );

        if (!string.IsNullOrEmpty(path))
        {
            _saveFilePath = path;
            if (filePathText != null)
                filePathText.text = _saveFilePath;
        }
#else
        // 빌드 환경에서는 OS 파일 다이얼로그가 없으니
        // persistentDataPath 아래에 자동 저장
        string fileName = "runway_video.mp4";
        _saveFilePath = Path.Combine(Application.persistentDataPath, fileName);

        if (filePathText != null)
            filePathText.text =
                $"{_saveFilePath}\n(런타임: 기본 경로 자동 사용)";
#endif
    }

    // ==============================
    // 2) 영상 생성 버튼
    // ==============================
    private void OnClickGenerate()
    {
        if (apiConfig == null || string.IsNullOrEmpty(apiConfig.Runway_API))
        {
            SetStatus("❌ Runway API 키가 설정되지 않았습니다.");
            return;
        }

        if (refineOutputText == null || string.IsNullOrWhiteSpace(refineOutputText.text))
        {
            SetStatus("❌ 2차 가공 텍스트(refineOutputText)가 비어 있습니다.");
            return;
        }

        // 저장 경로가 아직 없으면 기본값으로 자동 지정
        if (string.IsNullOrEmpty(_saveFilePath))
        {
#if UNITY_EDITOR
            _saveFilePath = Path.Combine(Application.dataPath, "../runway_video.mp4");
#else
            _saveFilePath = Path.Combine(Application.persistentDataPath, "runway_video.mp4");
#endif
            if (filePathText != null)
                filePathText.text = _saveFilePath;
        }

        // refineOutputText 전체에서 ENG 블록만 뽑아서 쓰고,
        // 없으면 전체 텍스트를 프롬프트로 사용
        string raw = refineOutputText.text;
        string prompt = ExtractEngPrompt(raw);
        if (string.IsNullOrWhiteSpace(prompt))
            prompt = raw;

        StartCoroutine(Co_GenerateVideo(prompt, _saveFilePath));
    }

    // refineOutputText 안에서 "ENG" 이후 부분만 추출
    private string ExtractEngPrompt(string fullText)
    {
        if (string.IsNullOrWhiteSpace(fullText))
            return null;

        var lines = fullText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        bool inEng = false;
        var sb = new StringBuilder();

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line == "ENG")
            {
                inEng = true;
                continue;
            }

            if (inEng)
            {
                if (sb.Length > 0) sb.Append(" ");
                sb.Append(line);
            }
        }

        return sb.ToString().Trim();
    }

    // 상태 텍스트 간단 헬퍼
    private void SetStatus(string msg)
    {
        Debug.Log(msg);
        if (statusText != null)
            statusText.text = msg;
    }

    // ==============================
    // 3) Runway Text-to-Video 호출
    // ==============================
    private IEnumerator Co_GenerateVideo(string promptText, string savePath)
    {
        SetStatus("① Runway에 영상 생성 요청 중...");

        // 🔹 문서 예제와 동일한 구조로 body 구성
        var body = new RunwayTextToVideoRequest
        {
            promptText = promptText,
            ratio = ratio,
            audio = includeAudio,
            duration = durationSeconds,
            model = runwayModel   // 🔹 gen3a_turbo / gen4.5 / veo3 / veo3.1 / veo3.1_fast 중 하나
        };

        string json = JsonUtility.ToJson(body);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(textToVideoEndpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiConfig.Runway_API);
            req.SetRequestHeader("X-Runway-Version", runwayApiVersion);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"❌ HTTP Error(Create): {req.responseCode}\n{req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            RunwayTaskCreateResponse createRes = null;
            try
            {
                createRes = JsonUtility.FromJson<RunwayTaskCreateResponse>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                SetStatus($"❌ Runway 응답 파싱 실패(Create): {e.Message}\n{req.downloadHandler.text}");
                yield break;
            }

            if (createRes == null || string.IsNullOrEmpty(createRes.id))
            {
                SetStatus($"❌ Runway 응답에 task id가 없습니다.\n{req.downloadHandler.text}");
                yield break;
            }

            SetStatus($"② Task 생성 완료 (id={createRes.id}) – 렌더링 대기 중...");

            // Task 완료까지 폴링
            yield return StartCoroutine(Co_PollRunwayTask(createRes.id, savePath));
        }
    }

    // ==============================
    // 4) Runway Task 폴링 + 비디오 저장
    // ==============================
    private IEnumerator Co_PollRunwayTask(string taskId, string savePath)
    {
        string taskUrl = $"https://api.dev.runwayml.com/v1/tasks/{taskId}";

        while (true)
        {
            using (var req = UnityWebRequest.Get(taskUrl))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + apiConfig.Runway_API);
                req.SetRequestHeader("X-Runway-Version", runwayApiVersion);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    SetStatus($"❌ HTTP Error(Task): {req.responseCode}\n{req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                RunwayTaskStatusResponse taskRes = null;
                try
                {
                    taskRes = JsonUtility.FromJson<RunwayTaskStatusResponse>(req.downloadHandler.text);
                }
                catch (Exception e)
                {
                    SetStatus($"❌ Runway 응답 파싱 실패(Task): {e.Message}\n{req.downloadHandler.text}");
                    yield break;
                }

                if (taskRes == null)
                {
                    SetStatus($"❌ Task 응답이 비어 있습니다.\n{req.downloadHandler.text}");
                    yield break;
                }

                if (string.Equals(taskRes.status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(taskRes.status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    if (taskRes.output == null || taskRes.output.Length == 0)
                    {
                        SetStatus("❌ Task는 성공했지만 output URL이 없습니다.");
                        yield break;
                    }

                    string videoUrl = taskRes.output[0];
                    SetStatus("③ 렌더링 완료 – 비디오 다운로드 중...");

                    yield return StartCoroutine(Co_DownloadVideo(videoUrl, savePath));
                    yield break;
                }
                else if (string.Equals(taskRes.status, "FAILED", StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus($"❌ Runway Task 실패: {req.downloadHandler.text}");
                    yield break;
                }
                else
                {
                    SetStatus($"② Task 상태: {taskRes.status} ...");
                }
            }

            yield return new WaitForSeconds(3f);
        }
    }

    // ==============================
    // 5) 비디오 파일 다운로드 & 저장
    // ==============================
    private IEnumerator Co_DownloadVideo(string url, string savePath)
    {
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"❌ 비디오 다운로드 실패: {req.responseCode}\n{req.error}");
                yield break;
            }

            try
            {
                byte[] data = req.downloadHandler.data;
                Directory.CreateDirectory(Path.GetDirectoryName(savePath) ?? ".");
                File.WriteAllBytes(savePath, data);
            }
            catch (Exception e)
            {
                SetStatus($"❌ 파일 저장 실패: {e.Message}");
                yield break;
            }
        }

        SetStatus($"✅ 영상 저장 완료: {savePath}");
    }
}
