using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UIBinding : MonoBehaviour
{
    [Header("API Config")]
    public API apiConfig;

    [Header("Input")]
    public InputField promptInput;           // 왼쪽 큰 입력창

    [Header("Buttons")]
    public Button transformButton;           // "변환하기" 버튼

    [Header("Style Dropdowns (2차 가공용, 지금은 미사용)")]
    public Dropdown cameraStyleDropdown;
    public Dropdown lightingStyleDropdown;
    public Dropdown movementSpeedDropdown;
    public Dropdown styleAestheticDropdown;
    public Dropdown movementTypesDropdown;
    public Dropdown textStylesDropdown;

    [Header("Cut List UI")]
    public RectTransform cutsParent;         // ScrollView의 Content
    public GameObject cutItemPrefab;         // CutItem 프리팹

    [Header("Runtime Cut Data")]
    public CutList currentCutList;           // 나중에 Runway API 호출할 때 사용할 데이터

    // 현재 선택된 컷 UI (없으면 null)
    public CutItemUI SelectedCutItem { get; private set; }

    const string Endpoint = "https://api.openai.com/v1/chat/completions";
    const string Model = "gpt-4.1-mini";

    [Serializable]
    class ChatMessage { public string role; public string content; }

    [Serializable]
    class ChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public float temperature = 0.7f;
    }

    [Serializable]
    class ChatResponse { public Choice[] choices; }

    [Serializable]
    class Choice { public ChatMessage message; }

    class CutData
    {
        public int index;
        public string title;
        public string description;
    }

    // 내부에서 관리할 UI 리스트
    readonly List<CutItemUI> cutItemUIList = new List<CutItemUI>();

    // ============================ 라이프사이클 ============================
    void Awake()
    {
        if (transformButton != null)
            transformButton.onClick.AddListener(OnClickTransform);
        else
            Debug.LogWarning("transformButton이 설정되지 않았습니다.");
    }

    void OnDestroy()
    {
        if (transformButton != null)
            transformButton.onClick.RemoveListener(OnClickTransform);
    }

    // ============================ 버튼 클릭 ============================
    void OnClickTransform()
    {
        var raw = promptInput != null ? promptInput.text : "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            Debug.LogWarning("시나리오를 먼저 입력하세요.");
            return;
        }

        if (apiConfig == null || string.IsNullOrEmpty(apiConfig.ChatGPT_API))
        {
            Debug.LogError("ChatGPT API 키가 설정되지 않았습니다.");
            return;
        }

        StartCoroutine(RequestCutSplit(raw));
    }

    // ============================ GPT 컷 분할 요청 ============================
    IEnumerator RequestCutSplit(string scenario)
    {
        Debug.Log("⏳ GPT에 컷 분할 요청 중...");

        string systemPrompt =
@"너는 영상 연출을 위한 컷 분할 보조 도우미야.

(중략)  // 여기는 네가 쓰던 프롬프트 그대로 두면 됨";

        string userPrompt =
            "다음 시놉시스를 위 규칙에 맞는 컷 리스트로 분할해줘.\n\n" +
            scenario;

        var requestBody = new ChatRequest
        {
            model = Model,
            messages = new[]
            {
                new ChatMessage { role = "system", content = systemPrompt },
                new ChatMessage { role = "user", content = userPrompt }
            }
        };

        string json = JsonUtility.ToJson(requestBody);

        using (var req = new UnityWebRequest(Endpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiConfig.ChatGPT_API);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"HTTP Error: {req.responseCode}\n{req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            string content;
            try
            {
                var res = JsonUtility.FromJson<ChatResponse>(req.downloadHandler.text);
                content = res.choices[0].message.content.Trim();
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON 파싱 실패: {e.Message}\n{req.downloadHandler.text}");
                yield break;
            }

            Debug.Log("GPT-컷분할 응답:\n" + content);

            BuildCutItems(content);
        }
    }

    // ============================ 프리팹 생성 + CutInfo 저장 ============================
    void BuildCutItems(string gptText)
    {
        if (cutsParent == null || cutItemPrefab == null)
        {
            Debug.LogError("cutsParent나 cutItemPrefab이 설정되지 않았습니다.");
            return;
        }

        // 기존 자식 제거
        for (int i = cutsParent.childCount - 1; i >= 0; i--)
            Destroy(cutsParent.GetChild(i).gameObject);

        cutItemUIList.Clear();
        SelectedCutItem = null;

        List<CutData> cuts = ParseCuts(gptText);

        // --- CutInfo 배열 생성해서 Runtime 데이터로 저장 ---
        var cutInfos = new CutInfo[cuts.Count];
        for (int i = 0; i < cuts.Count; i++)
        {
            var c = cuts[i];
            var info = new CutInfo
            {
                index = c.index,
                sceneDescription = c.description,
                shotType = "",          // 나중에 별도 UI/프롬프트로 채울 부분
                characterPrompt = "",
                backgroundPrompt = "",
                cameraPrompt = "",
                duration = ""
            };

            cutInfos[i] = info;

            // 프리팹 생성 + UI 세팅
            var go = Instantiate(cutItemPrefab, cutsParent);
            var ui = go.GetComponent<CutItemUI>();
            if (ui != null)
            {
                ui.Setup(this, info, c.title, c.description);
                cutItemUIList.Add(ui);
            }
        }

        currentCutList = new CutList { cuts = cutInfos };

        // 레이아웃 가라 리프레시 (네가 말한 껐다 켜기)
        var vlg = cutsParent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.enabled = false;
            Canvas.ForceUpdateCanvases();
            vlg.enabled = true;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(cutsParent);
    }

    // ============================ 카드 선택 처리 ============================
    public void OnCutItemClicked(CutItemUI clicked)
    {
        SelectedCutItem = clicked;

        // 한 개만 선택 상태로 유지
        foreach (var ui in cutItemUIList)
            ui.SetSelected(ui == clicked);

        // 여기서 clicked.cutInfo 를 가지고
        // 나중에 Runway API 호출 파라미터 구성하면 됨
        Debug.Log($"선택된 컷: {clicked.cutInfo.index}, desc={clicked.cutInfo.sceneDescription}");
    }

    // ============================ GPT 텍스트 파싱 ============================
    List<CutData> ParseCuts(string gptText)
    {
        var list = new List<CutData>();
        if (string.IsNullOrWhiteSpace(gptText))
            return list;

        var lines = gptText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        CutData current = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            if (line.StartsWith("GPT-컷분할"))
                continue;

            if (line.StartsWith("Cut "))
            {
                if (current != null)
                    list.Add(current);

                current = new CutData();

                int num = 0;
                var parts = line.Split(new[] { ' ' }, 3);
                if (parts.Length >= 2)
                    int.TryParse(parts[1], out num);

                current.index = num > 0 ? num : list.Count + 1;

                int dashIndex = line.IndexOf('—');
                if (dashIndex < 0)
                    dashIndex = line.IndexOf('-');

                if (dashIndex >= 0 && dashIndex + 1 < line.Length)
                    current.title = line.Substring(dashIndex + 1).Trim();
                else
                    current.title = line;
            }
            else
            {
                if (current != null)
                {
                    if (string.IsNullOrEmpty(current.description))
                        current.description = line;
                    else
                        current.description += "\n" + line;
                }
            }
        }

        if (current != null)
            list.Add(current);

        return list;
    }
}
