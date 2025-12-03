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
    public InputField promptInput;           // 왼쪽 큰 입력창 (줄글 시나리오)

    [Header("Buttons")]
    public Button transformButton;           // 1차 컷분할 버튼
    public Button refineButton;              // 2차 가공(스토리보드/프롬프트) 버튼

    [Header("Style Dropdowns (2차 가공용)")]
    public Dropdown cameraStyleDropdown;
    public Dropdown lightingStyleDropdown;
    public Dropdown movementSpeedDropdown;
    public Dropdown styleAestheticDropdown;
    public Dropdown movementTypesDropdown;
    public Dropdown textStylesDropdown;

    [Header("Cut List UI (1차 결과)")]
    public RectTransform cutsParent;         // ScrollView의 Content
    public GameObject cutItemPrefab;         // CutItem 프리팹

    [Header("2nd Pass Output")]
    public Text refineOutputText;            // 2차 가공 결과 출력 Text

    [Header("Runtime Cut Data")]
    public CutList currentCutList;           // 전체 컷 데이터 (Runway 호출 때 사용 예정)

    [Header("Character System")]
    public CharacterManager characterManager;  // @char 태그 처리용

    // 현재 선택된 컷 UI
    public CutItemUI SelectedCutItem { get; private set; }

    // 현재 시놉시스에서 파싱된 캐릭터/배경들
    public List<CharacterProfile> ParsedCharacters { get; private set; } = new List<CharacterProfile>();
    public List<BackgroundProfile> ParsedBackgrounds { get; private set; } = new List<BackgroundProfile>();

    // 원본 시놉시스 (태그 포함) - Runway referenceImages 빌드용
    public string OriginalSynopsis { get; private set; } = "";

    const string Endpoint = "https://api.openai.com/v1/chat/completions";
    const string Model = "gpt-4.1-mini";

    #region OpenAI DTO

    [Serializable]
    class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    class ChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public float temperature = 0.7f;
    }

    [Serializable]
    class ChatResponse
    {
        public Choice[] choices;
    }

    [Serializable]
    class Choice
    {
        public ChatMessage message;
    }

    #endregion

    class CutData
    {
        public int index;
        public string title;
        public string description;
    }

    readonly List<CutItemUI> cutItemUIList = new List<CutItemUI>();

    // ============================ 라이프사이클 ============================
    void Awake()
    {
        if (transformButton != null)
            transformButton.onClick.AddListener(OnClickTransform);
        else
            Debug.LogWarning("transformButton이 설정되지 않았습니다.");

        if (refineButton != null)
            refineButton.onClick.AddListener(OnClickRefine);
        else
            Debug.LogWarning("refineButton이 설정되지 않았습니다.");
    }

    void OnDestroy()
    {
        if (transformButton != null)
            transformButton.onClick.RemoveListener(OnClickTransform);

        if (refineButton != null)
            refineButton.onClick.RemoveListener(OnClickRefine);
    }

    // ============================ 1차: 줄글 → 컷 분할 ============================
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

        // 원본 시놉시스 저장 (태그 포함) - Runway referenceImages 빌드용
        OriginalSynopsis = raw;

        // @char 태그 파싱
        ParsedCharacters.Clear();
        ParsedBackgrounds.Clear();
        if (characterManager != null)
        {
            ParsedCharacters = characterManager.ParseCharacterTags(raw);
            if (ParsedCharacters.Count > 0)
            {
                Debug.Log($"[UIBinding] @char 태그에서 {ParsedCharacters.Count}명의 캐릭터 파싱됨:");
                foreach (var c in ParsedCharacters)
                    Debug.Log($"  - {c.name}: {c.textProfile}");
            }

            // @back 태그 파싱
            ParsedBackgrounds = characterManager.ParseBackgroundTags(raw);
            if (ParsedBackgrounds.Count > 0)
            {
                Debug.Log($"[UIBinding] @back 태그에서 {ParsedBackgrounds.Count}개의 배경 파싱됨:");
                foreach (var b in ParsedBackgrounds)
                    Debug.Log($"  - {b.name}: {b.description}");
            }
        }

        StartCoroutine(RequestCutSplit(raw));
    }

    IEnumerator RequestCutSplit(string scenario)
    {
        Debug.Log("⏳ GPT에 컷 분할 요청 중...");

        string systemPrompt =
@"너는 영상 연출을 위한 컷 분할 보조 도우미야.

사용자는 한국어로 된 줄글 시놉시스를 한 덩어리로 보낸다.
너의 역할은 이 이야기를 **한 장면에 하나의 컷**이 되도록 최대한 잘게 나누어,
""컷"" 리스트로 정리하고 각 컷마다 등장인물과 배경 정보를 함께 추출하는 것이다.

반드시 아래 형식으로만 출력해라:

GPT-컷분할
Cut 1 — [짧은 한 줄 제목]
설명: [이 컷에서 일어나는 일에 대한 1문장 설명]
등장인물:
- [인물1 이름과 간단한 한 줄 설명]
- [인물2 이름과 간단한 한 줄 설명]
배경:
- [장소와 배경 구조를 1~2문장으로 설명]

Cut 2 — [짧은 한 줄 제목]
설명: [이 컷에서 일어나는 일에 대한 1문장 설명]
등장인물:
- [...]
배경:
- [...]

Cut 3 — ...

규칙:
- 출력은 모두 한국어로 작성한다.
- 컷 번호는 Cut 1부터 순서대로 증가시킨다.
- **한 컷에는 하나의 핵심 장면/행동만 담는다.**
- 인물이 새로운 행동을 시작하거나, 장소가 바뀌거나, 시간이 바뀌거나,
  다른 인물이 합류·퇴장하면 반드시 새로운 컷으로 나눈다.
- ""설명""은 반드시 1문장만 작성한다.
- 컷 개수를 줄이려고 여러 사건을 한 컷에 넣지 말고,
  컷 수가 다소 많아지더라도 한 컷에 한 장면만 담도록 분할하라.
- 원문에 있는 중요한 사건은 빠뜨리지 말고 반드시 하나 이상의 컷으로 포함한다.
- 반드시 위에 제시한 형식과 줄 순서를 그대로 지킨다.
- ""GPT-컷분할""이라는 첫 줄 제목은 반드시 포함한다.
- 위에서 제시한 형식 외의 해설, 말투, 설명 문장은 절대 추가하지 않는다.";

        // @char 태그로 파싱된 캐릭터 프로필을 컨텍스트로 추가
        string characterContext = "";
        if (characterManager != null && ParsedCharacters.Count > 0)
        {
            characterContext = characterManager.BuildCharacterContext(ParsedCharacters);
        }

        // @back 태그로 파싱된 배경 프로필을 컨텍스트로 추가
        string backgroundContext = "";
        if (characterManager != null && ParsedBackgrounds.Count > 0)
        {
            backgroundContext = characterManager.BuildBackgroundContext(ParsedBackgrounds);
        }

        // @char, @back 태그 제거한 시나리오 (GPT에게는 태그 없는 순수 텍스트만 전달)
        string cleanScenario = characterManager != null
            ? characterManager.RemoveAllTags(scenario)
            : scenario;

        // 컨텍스트 결합 (캐릭터 + 배경)
        string fullContext = "";
        if (!string.IsNullOrEmpty(characterContext))
            fullContext += characterContext + "\n";
        if (!string.IsNullOrEmpty(backgroundContext))
            fullContext += backgroundContext + "\n";

        string userPrompt =
            (string.IsNullOrEmpty(fullContext) ? "" : fullContext + "\n") +
            "다음 시놉시스를 위 규칙에 맞는 컷 리스트로 분할해줘.\n\n" +
            cleanScenario;

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

    // ============================ 1차 결과 → 프리팹 생성 + CutInfo 저장 ============================
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

        var cutInfos = new CutInfo[cuts.Count];

        for (int i = 0; i < cuts.Count; i++)
        {
            var c = cuts[i];

            // 파싱된 캐릭터들의 이름 배열 생성
            string[] charNames = new string[ParsedCharacters.Count];
            for (int j = 0; j < ParsedCharacters.Count; j++)
                charNames[j] = ParsedCharacters[j].name;

            // 메인 캐릭터 이미지 경로 (첫 번째 캐릭터 사용)
            string mainCharImagePath = ParsedCharacters.Count > 0
                ? ParsedCharacters[0].imagePath
                : "";

            // 런타임 데이터 생성
            var info = new CutInfo
            {
                index = c.index,
                sceneDescriptionKo = c.description,

                cameraStyle = "",
                lightingStyle = "",
                movementSpeed = "",
                movementType = "",
                aestheticStyle = "",
                textStyle = "",
                koreanShot = "",
                englishPrompt = "",

                // @char 캐릭터 정보
                characterNames = charNames,
                characterImagePath = mainCharImagePath
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

        // 레이아웃 가라 리프레시
        var vlg = cutsParent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.enabled = false;
            Canvas.ForceUpdateCanvases();
            vlg.enabled = true;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(cutsParent);
    }

    // ============================ 컷 카드 선택 처리 ============================
    public void OnCutItemClicked(CutItemUI clicked)
    {
        SelectedCutItem = clicked;

        foreach (var ui in cutItemUIList)
            ui.SetSelected(ui == clicked);

        Debug.Log($"선택된 컷: {clicked.cutInfo.index}, desc={clicked.cutInfo.sceneDescriptionKo}");
    }

    // ============================ GPT 컷 텍스트 파싱 ============================
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

            if (line.StartsWith("Cut ") || line.StartsWith("컷 "))
            {
                if (current != null)
                {
                    if (string.IsNullOrEmpty(current.description))
                        current.description = current.title;
                    list.Add(current);
                }

                current = new CutData();

                // 번호
                int num = 0;
                int spaceIdx = line.IndexOf(' ');
                if (spaceIdx >= 0)
                {
                    int colonIdx = line.IndexOfAny(new char[] { '—', '-', ':' }, spaceIdx + 1);
                    string numPart;
                    if (colonIdx > spaceIdx)
                        numPart = line.Substring(spaceIdx + 1, colonIdx - (spaceIdx + 1));
                    else
                        numPart = line.Substring(spaceIdx + 1);

                    numPart = numPart.Trim().TrimEnd('.', '번', ':');
                    int.TryParse(numPart, out num);
                }
                current.index = num > 0 ? num : list.Count + 1;

                // 제목
                int sepIdx = line.IndexOf('—');
                if (sepIdx < 0) sepIdx = line.IndexOf('-');
                if (sepIdx < 0) sepIdx = line.IndexOf(':');

                if (sepIdx >= 0 && sepIdx + 1 < line.Length)
                    current.title = line.Substring(sepIdx + 1).Trim();
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
        {
            if (string.IsNullOrEmpty(current.description))
                current.description = current.title;
            list.Add(current);
        }

        return list;
    }

    // ============================ 2차: 선택 컷 → Storyboard/Prompt ============================
    void OnClickRefine()
    {
        if (SelectedCutItem == null || SelectedCutItem.cutInfo == null)
        {
            Debug.LogWarning("먼저 컷을 하나 선택하세요.");
            if (refineOutputText != null)
                refineOutputText.text = "먼저 컷 리스트에서 컷 하나를 클릭해서 선택하세요.";
            return;
        }

        if (apiConfig == null || string.IsNullOrEmpty(apiConfig.ChatGPT_API))
        {
            Debug.LogError("ChatGPT API 키가 설정되지 않았습니다.");
            if (refineOutputText != null)
                refineOutputText.text = "ChatGPT API 키가 설정되지 않았습니다.";
            return;
        }

        // 드롭다운 값 읽기
        string cam = cameraStyleDropdown != null
            ? cameraStyleDropdown.options[cameraStyleDropdown.value].text
            : "";
        string light = lightingStyleDropdown != null
            ? lightingStyleDropdown.options[lightingStyleDropdown.value].text
            : "";
        string moveSpeed = movementSpeedDropdown != null
            ? movementSpeedDropdown.options[movementSpeedDropdown.value].text
            : "";
        string aesthetic = styleAestheticDropdown != null
            ? styleAestheticDropdown.options[styleAestheticDropdown.value].text
            : "";
        string moveType = movementTypesDropdown != null
            ? movementTypesDropdown.options[movementTypesDropdown.value].text
            : "";
        string textStyle = textStylesDropdown != null
            ? textStylesDropdown.options[textStylesDropdown.value].text
            : "";

        var cut = SelectedCutItem.cutInfo;

        // CutInfo에 스타일 저장
        cut.cameraStyle = cam;
        cut.lightingStyle = light;
        cut.movementSpeed = moveSpeed;
        cut.movementType = moveType;
        cut.aestheticStyle = aesthetic;
        cut.textStyle = textStyle;

        StartCoroutine(RequestStoryboardForCut(
            cut, cam, light, moveSpeed, aesthetic, moveType, textStyle));
    }

    IEnumerator RequestStoryboardForCut(
        CutInfo cut,
        string cameraStyle,
        string lightingStyle,
        string movementSpeed,
        string aestheticStyle,
        string movementType,
        string textStyle
    )
    {
        if (refineOutputText != null)
            refineOutputText.text = $"선택된 컷 {cut.index}를 2차 가공 중...";

        // 슬라이드에서 보여준 Storyboard / Prompt 레이아웃에 맞춘 프롬프트
        string systemPrompt =
@"You are a storyboard and prompt engineering assistant for AI video generation.

The user will give you:
- One short Korean cut description (scene of a story),
- Several style options (camera, lighting, movement, aesthetic, text style).

Your job:
- Turn this into a ONE-SHOT storyboard + prompt
- in the following EXACT text layout.

==== OUTPUT FORMAT (must follow exactly) ====

Storyboard

Cut <index> — <짧은 한국어 컷 제목>

Base
Location: <장소를 한국어로 한 줄>
Time: <시간대를 한국어로 한 단어 또는 짧은 표현>
Characters: <등장인물 이름들, 한국어>
Core Event: <이 컷에서 일어나는 핵심 사건을 한국어 1문장으로>

Options-옵션들은 전부적용, 3개만출력
Camera Style: <camera style (use user option, English)>
Lighting: <lighting style (use user option, English)>
Style/Aesthetic: <aesthetic style (use user option, English)>

Prompt

KOR
<2~3문장으로, 카메라 구도 + 인물 행동 + 분위기를 한국어로 묘사한다.
각 문장은 줄바꿈으로 구분한다.>

ENG
<One paragraph English description (1~3 sentences) that combines
scene, characters, background, camera, lighting, movement, and aesthetic.
Write this as a coherent block of text that can be used directly
as an AI video prompt.>

==== RULES ====
- Keep the headers and labels EXACTLY as shown:
  Storyboard / Base / Options / Prompt / KOR / ENG.
- Use the user's style options as hard constraints (do not change their meaning).
- Do NOT add any other sections or explanations outside this format.
- All Korean text must be in Korean; all English text must be in English.";

        string userPrompt =
            $"[Cut Index]\n{cut.index}\n\n" +
            "[Cut Description (Korean)]\n" +
            cut.sceneDescriptionKo + "\n\n" +
            "[Style Options]\n" +
            $"CameraStyle = {cameraStyle}\n" +
            $"LightingStyle = {lightingStyle}\n" +
            $"MovementSpeed = {movementSpeed}\n" +
            $"MovementType = {movementType}\n" +
            $"Aesthetic = {aestheticStyle}\n" +
            $"TextStyle = {textStyle}\n\n" +
            "Generate the storyboard + prompt in the exact format.";

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
                Debug.LogError($"HTTP Error(2nd): {req.responseCode}\n{req.error}\n{req.downloadHandler.text}");
                if (refineOutputText != null)
                    refineOutputText.text = $"HTTP Error: {req.responseCode}\n{req.error}\n{req.downloadHandler.text}";
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
                Debug.LogError($"JSON 파싱 실패(2nd): {e.Message}\n{req.downloadHandler.text}");
                if (refineOutputText != null)
                    refineOutputText.text = $"JSON 파싱 실패: {e.Message}\n{req.downloadHandler.text}";
                yield break;
            }

            Debug.Log("2차 스토리보드 응답:\n" + content);

            if (refineOutputText != null)
                refineOutputText.text = content;

            // === CutInfo에 KOR / ENG 블록 저장 ===
            if (cut != null)
            {
                var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                bool inKor = false;
                bool inEng = false;
                var korSb = new StringBuilder();
                var engSb = new StringBuilder();

                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();

                    if (line == "KOR")
                    {
                        inKor = true;
                        inEng = false;
                        continue;
                    }

                    if (line == "ENG")
                    {
                        inKor = false;
                        inEng = true;
                        continue;
                    }

                    if (inKor)
                    {
                        if (korSb.Length > 0) korSb.Append("\n");
                        korSb.Append(line);
                    }
                    else if (inEng)
                    {
                        if (engSb.Length > 0) engSb.Append(" ");
                        engSb.Append(line);
                    }
                }

                cut.koreanShot = korSb.ToString().Trim();
                cut.englishPrompt = engSb.ToString().Trim();

                Debug.Log($"[Cut {cut.index}] KOR 저장: {cut.koreanShot}");
                Debug.Log($"[Cut {cut.index}] ENG 저장: {cut.englishPrompt}");
            }
        }
    }
}
