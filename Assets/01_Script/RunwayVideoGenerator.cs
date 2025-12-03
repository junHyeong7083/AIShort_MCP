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

    [Header("Character Integration")]
    [Tooltip("UIBinding 참조 - 선택된 컷의 캐릭터 이미지 가져오기용")]
    public UIBinding uiBinding;        // @char 캐릭터 이미지 연동

    [Header("Runway Options")]
    [Tooltip("Runway Text-To-Video 엔드포인트")]
    public string textToVideoEndpoint = "https://api.dev.runwayml.com/v1/text_to_video";
    [Tooltip("Runway Image-To-Video 엔드포인트")]
    public string imageToVideoEndpoint = "https://api.dev.runwayml.com/v1/image_to_video";
    [Tooltip("Runway Text-To-Image 엔드포인트 (캐릭터 참조용)")]
    public string textToImageEndpoint = "https://api.dev.runwayml.com/v1/text_to_image";
    [Tooltip("Runway API Version 헤더 값 (공식 문서 보고 최신값으로 교체 가능)")]
    public string runwayApiVersion = "2024-11-06";
    [Tooltip("Text-to-Image 모델")]
    public string imageModel = "gen4_image_turbo";

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
    private class RunwayImageToVideoRequest   // 추가
    {
        public string promptImage;  // 업로드된 이미지 URL
        public string promptText;   // 프롬프트 텍스트
        public string ratio;
        public bool audio;
        public int duration;
        public string model;
    }

    // Text-to-Image API용 클래스들
    [Serializable]
    private class RunwayTextToImageRequest
    {
        public string promptText;
        public string ratio;
        public string model;
        public ReferenceImage[] referenceImages;  // 1~3개 캐릭터 참조 이미지
    }

    [Serializable]
    private class ReferenceImage
    {
        public string uri;   // Base64 Data URI 또는 URL
        public string tag;   // 캐릭터 이름 (선택)
    }

    [Serializable]
    private class RunwayImageResponse
    {
        public string id;
        public string status;
        public string[] output;  // 생성된 이미지 URL
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

        // uiBinding이 null이면 자동으로 찾기
        if (uiBinding == null)
        {
            uiBinding = FindObjectOfType<UIBinding>();
            Debug.LogWarning("[Runway] uiBinding이 Inspector에서 설정되지 않아 자동으로 찾았습니다.");
        }

        // @char 또는 @back 태그가 있는지 확인 (원본 시놉시스에서 체크!)
        // GPT 출력(ENG 블록)에는 태그가 없으므로, UIBinding에 저장된 원본 시놉시스를 사용
        string originalSynopsis = uiBinding != null ? uiBinding.OriginalSynopsis : "";

        Debug.Log($"[Runway] ========== 태그 체크 시작 ==========");
        Debug.Log($"[Runway] uiBinding null? {uiBinding == null}");
        Debug.Log($"[Runway] OriginalSynopsis 길이: {originalSynopsis?.Length ?? 0}");
        Debug.Log($"[Runway] CharacterManager.Instance null? {CharacterManager.Instance == null}");

        bool hasReferenceTags = CharacterManager.Instance != null &&
                                !string.IsNullOrEmpty(originalSynopsis) &&
                                CharacterManager.Instance.HasReferenceTags(originalSynopsis);

        Debug.Log($"[Runway] hasReferenceTags = {hasReferenceTags}");
        if (!string.IsNullOrEmpty(originalSynopsis))
            Debug.Log($"[Runway] OriginalSynopsis: {originalSynopsis.Substring(0, Math.Min(100, originalSynopsis.Length))}...");
        else
            Debug.LogWarning("[Runway] OriginalSynopsis가 비어있습니다! UIBinding에서 1차 컷분할을 먼저 실행했는지 확인하세요.");

        if (hasReferenceTags)
        {
            // 태그 있음 → Text-to-Image → Image-to-Video 파이프라인
            // 원본 시놉시스로 referenceImages를 빌드하고, ENG prompt로 API 호출
            SetStatus("@char/@back 태그 감지됨 → Text-to-Image → Image-to-Video 모드");
            StartCoroutine(Co_GenerateWithReferences(originalSynopsis, prompt, _saveFilePath));
        }
        else
        {
            // 태그 없음 → Text-to-Video 직접 호출
            StartCoroutine(Co_GenerateVideo(prompt, _saveFilePath));
        }
    }
    public void GenerateFromImageUrl(string promptImageUrl)
    {
        if (apiConfig == null || string.IsNullOrEmpty(apiConfig.Runway_API))
        {
            SetStatus("Runway API 키가 설정되지 않았습니다.");
            return;
        }

        if (refineOutputText == null || string.IsNullOrWhiteSpace(refineOutputText.text))
        {
            SetStatus("2차 가공 텍스트(refineOutputText)가 비어 있습니다.");
            return;
        }

        if (string.IsNullOrEmpty(promptImageUrl))
        {
            SetStatus("이미지 URL이 비어 있습니다.");
            return;
        }

        // 저장 경로 없으면 기본값으로 자동 지정
        if (string.IsNullOrEmpty(_saveFilePath))
        {
#if UNITY_EDITOR
            _saveFilePath = Path.Combine(Application.dataPath, "../runway_image2video.mp4");
#else
            _saveFilePath = Path.Combine(Application.persistentDataPath, "runway_image2video.mp4");
#endif
            if (filePathText != null)
                filePathText.text = _saveFilePath;
        }

        string raw = refineOutputText.text;
        string prompt = ExtractEngPrompt(raw);
        if (string.IsNullOrWhiteSpace(prompt))
            prompt = raw;

        StartCoroutine(Co_GenerateVideoFromImage(promptImageUrl, prompt, _saveFilePath));
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
    private IEnumerator Co_GenerateVideoFromImage(string promptImageUrl, string promptText, string savePath)
    {
        SetStatus("Runway(ImageToVideo)에 영상 생성 요청 중...");

        var body = new RunwayImageToVideoRequest
        {
            promptImage = promptImageUrl,
            promptText = promptText,
            ratio = ratio,
            audio = includeAudio,
            duration = durationSeconds,
            model = runwayModel
        };

        string json = JsonUtility.ToJson(body);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(imageToVideoEndpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiConfig.Runway_API);
            req.SetRequestHeader("X-Runway-Version", runwayApiVersion);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"HTTP Error(ImageToVideo Create): {req.responseCode}\n{req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            RunwayTaskCreateResponse createRes = null;
            try
            {
                createRes = JsonUtility.FromJson<RunwayTaskCreateResponse>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                SetStatus($"Runway 응답 파싱 실패(ImageToVideo Create): {e.Message}\n{req.downloadHandler.text}");
                yield break;
            }

            if (createRes == null || string.IsNullOrEmpty(createRes.id))
            {
                SetStatus($"Runway 응답에 task id가 없습니다.\n{req.downloadHandler.text}");
                yield break;
            }

            SetStatus($"ImageToVideo Task 생성 완료 (id={createRes.id}) – 렌더링 대기 중...");
            yield return StartCoroutine(Co_PollRunwayTask(createRes.id, savePath));
        }
    }

    // ==============================
    // 6) @char/@back 태그 기반 Text-to-Image → Image-to-Video 파이프라인
    // ==============================
    /// <summary>
    /// Text-to-Image → Image-to-Video 파이프라인
    /// </summary>
    /// <param name="originalSynopsis">원본 시놉시스 (태그 포함) - referenceImages 빌드용</param>
    /// <param name="engPrompt">GPT가 생성한 ENG 프롬프트 - API 호출용</param>
    /// <param name="savePath">비디오 저장 경로</param>
    private IEnumerator Co_GenerateWithReferences(string originalSynopsis, string engPrompt, string savePath)
    {
        Debug.Log($"[Runway] ========== Co_GenerateWithReferences 시작 ==========");
        Debug.Log($"[Runway] originalSynopsis: {originalSynopsis?.Substring(0, Math.Min(100, originalSynopsis?.Length ?? 0))}...");
        Debug.Log($"[Runway] engPrompt: {engPrompt?.Substring(0, Math.Min(100, engPrompt?.Length ?? 0))}...");

        // 1. 원본 시놉시스에서 referenceImages 빌드
        var refAssets = CharacterManager.Instance.BuildReferenceAssets(originalSynopsis);
        Debug.Log($"[Runway] BuildReferenceAssets 결과: {refAssets.Count}개");

        if (refAssets.Count == 0)
        {
            SetStatus("참조 이미지를 찾을 수 없습니다. Text-to-Video로 전환합니다.");
            Debug.LogWarning("[Runway] refAssets가 0개! @char/@back 태그가 올바른지, 등록된 캐릭터/배경이 있는지 확인하세요.");
            yield return StartCoroutine(Co_GenerateVideo(engPrompt, savePath));
            yield break;
        }

        SetStatus($"① {refAssets.Count}개의 참조 이미지로 Text-to-Image 호출 중...");

        // 2. ReferenceImage 배열 생성 (Base64 인코딩)
        var referenceImages = new ReferenceImage[refAssets.Count];
        for (int i = 0; i < refAssets.Count; i++)
        {
            Debug.Log($"[Runway] 이미지 인코딩 중: {refAssets[i].name} → {refAssets[i].imagePath}");

            string base64Uri = CharacterManager.ImagePathToBase64DataUri(refAssets[i].imagePath);
            if (string.IsNullOrEmpty(base64Uri))
            {
                SetStatus($"이미지 인코딩 실패: {refAssets[i].name}");
                Debug.LogError($"[Runway] Base64 인코딩 실패! 파일 경로: {refAssets[i].imagePath}");
                yield break;
            }

            // Runway API tag 규칙: 영문+숫자+언더스코어만, 최소 3자, 영문으로 시작
            // 한글 이름을 영문 태그로 변환
            string validTag = GenerateValidTag(refAssets[i].type, i);

            referenceImages[i] = new ReferenceImage
            {
                uri = base64Uri,
                tag = validTag
            };
            Debug.Log($"[Runway] referenceImage[{i}]: name={refAssets[i].name}, tag={validTag}, type={refAssets[i].type}, base64길이={base64Uri.Length}");
        }

        // 3. ENG 프롬프트 사용 (GPT가 생성한 자연스러운 영문 프롬프트)
        string cleanPrompt = engPrompt;
        Debug.Log($"[Runway] Text-to-Image 프롬프트: {cleanPrompt}");

        // 4. Text-to-Image API 호출
        var imageRequest = new RunwayTextToImageRequest
        {
            promptText = cleanPrompt,
            ratio = "1024:1024",  // 이미지 생성용 비율
            model = imageModel,
            referenceImages = referenceImages
        };

        string json = JsonUtility.ToJson(imageRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        Debug.Log($"[Runway] Text-to-Image Request: {json}");

        string generatedImageUrl = null;

        using (var req = new UnityWebRequest(textToImageEndpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiConfig.Runway_API);
            req.SetRequestHeader("X-Runway-Version", runwayApiVersion);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"❌ Text-to-Image 실패: {req.responseCode}\n{req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            // Task ID 받아서 폴링
            RunwayTaskCreateResponse createRes = null;
            try
            {
                createRes = JsonUtility.FromJson<RunwayTaskCreateResponse>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                SetStatus($"❌ Text-to-Image 응답 파싱 실패: {e.Message}");
                yield break;
            }

            if (createRes == null || string.IsNullOrEmpty(createRes.id))
            {
                SetStatus($"❌ Text-to-Image에서 task id가 없습니다.");
                yield break;
            }

            SetStatus($"② Text-to-Image Task 생성 (id={createRes.id}) – 이미지 생성 대기 중...");

            // 이미지 생성 완료 대기 (폴링)
            generatedImageUrl = null;
            yield return StartCoroutine(Co_PollImageTask(createRes.id, (url) => generatedImageUrl = url));
        }

        if (string.IsNullOrEmpty(generatedImageUrl))
        {
            SetStatus("❌ 이미지 생성 실패. 비디오 생성을 중단합니다.");
            yield break;
        }

        SetStatus($"③ 이미지 생성 완료! Image-to-Video 호출 중...");
        Debug.Log($"[Runway] Generated Image URL: {generatedImageUrl}");

        // 5. 생성된 이미지로 Image-to-Video 호출
        yield return StartCoroutine(Co_GenerateVideoFromImage(generatedImageUrl, cleanPrompt, savePath));
    }

    // ==============================
    // 7) Runway API용 유효한 태그 생성
    // ==============================
    /// <summary>
    /// Runway API 태그 규칙에 맞는 유효한 태그 생성
    /// 규칙: /^[a-z][a-z0-9_]+$/i, 최소 3자
    /// </summary>
    /// <param name="type">"character" 또는 "background"</param>
    /// <param name="index">인덱스 번호</param>
    /// <returns>유효한 영문 태그 (예: "char_0", "back_1")</returns>
    private string GenerateValidTag(string type, int index)
    {
        // type에 따라 영문 접두사 결정
        string prefix;
        if (string.Equals(type, "character", StringComparison.OrdinalIgnoreCase))
        {
            prefix = "char";
        }
        else if (string.Equals(type, "background", StringComparison.OrdinalIgnoreCase))
        {
            prefix = "back";
        }
        else
        {
            prefix = "ref";  // 기본값
        }

        // "char_0", "back_1" 등의 형태로 생성 (최소 3자 이상)
        return $"{prefix}_{index}";
    }

    // ==============================
    // 8) Text-to-Image Task 폴링 (이미지 URL 반환)
    // ==============================
    private IEnumerator Co_PollImageTask(string taskId, Action<string> onComplete)
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
                    SetStatus($"❌ Image Task 폴링 실패: {req.responseCode}\n{req.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                RunwayTaskStatusResponse taskRes = null;
                try
                {
                    taskRes = JsonUtility.FromJson<RunwayTaskStatusResponse>(req.downloadHandler.text);
                }
                catch (Exception e)
                {
                    SetStatus($"❌ Image Task 응답 파싱 실패: {e.Message}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                if (taskRes == null)
                {
                    onComplete?.Invoke(null);
                    yield break;
                }

                if (string.Equals(taskRes.status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(taskRes.status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    if (taskRes.output != null && taskRes.output.Length > 0)
                    {
                        onComplete?.Invoke(taskRes.output[0]);
                        yield break;
                    }
                    else
                    {
                        SetStatus("❌ 이미지 생성 완료했지만 output이 없습니다.");
                        onComplete?.Invoke(null);
                        yield break;
                    }
                }
                else if (string.Equals(taskRes.status, "FAILED", StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus($"❌ Image Task 실패: {req.downloadHandler.text}");
                    onComplete?.Invoke(null);
                    yield break;
                }
                else
                {
                    SetStatus($"② Image Task 상태: {taskRes.status} ...");
                }
            }

            yield return new WaitForSeconds(2f);
        }
    }
}
