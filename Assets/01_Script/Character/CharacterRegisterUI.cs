using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 등록 UI
/// - 이미지 선택
/// - 이름 입력
/// - 텍스트 프로필 입력 (선택)
/// - 등록 버튼
/// </summary>
public class CharacterRegisterUI : MonoBehaviour
{
    [Header("UI References")]
    public InputField nameInput;           // 캐릭터 이름 입력
    public InputField profileInput;        // 텍스트 프로필 입력 (선택)
    public Button selectImageButton;       // 이미지 선택 버튼
    public Button registerButton;          // 등록 버튼
    public RawImage previewImage;          // 선택된 이미지 미리보기
    public Text statusText;                // 상태 메시지

    [Header("Character List UI (선택)")]
    public RectTransform characterListParent;  // 등록된 캐릭터 목록 표시용
    public GameObject characterItemPrefab;      // 캐릭터 아이템 프리팹

    // 선택된 로컬 이미지 경로
    private string _selectedImagePath;

    void Awake()
    {
        if (selectImageButton != null)
            selectImageButton.onClick.AddListener(OnClickSelectImage);

        if (registerButton != null)
            registerButton.onClick.AddListener(OnClickRegister);
    }

    void Start()
    {
        RefreshCharacterList();
    }

    void OnDestroy()
    {
        if (selectImageButton != null)
            selectImageButton.onClick.RemoveListener(OnClickSelectImage);

        if (registerButton != null)
            registerButton.onClick.RemoveListener(OnClickRegister);
    }

    // ======================== 이미지 선택 ========================

    void OnClickSelectImage()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel(
            "캐릭터 이미지 선택",
            "",
            "png,jpg,jpeg");

        if (!string.IsNullOrEmpty(path))
        {
            _selectedImagePath = path;
            SetStatus($"이미지 선택됨: {System.IO.Path.GetFileName(path)}");
            LoadPreviewImage(path);
        }
#else
        // 런타임에서는 파일 브라우저 플러그인 필요
        SetStatus("런타임 파일 선택은 아직 미지원");
#endif
    }

    void LoadPreviewImage(string path)
    {
        if (previewImage == null)
            return;

        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            previewImage.texture = tex;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CharacterRegisterUI] 이미지 로드 실패: {e.Message}");
        }
    }

    // ======================== 캐릭터 등록 ========================

    void OnClickRegister()
    {
        if (CharacterManager.Instance == null)
        {
            SetStatus("CharacterManager가 없습니다.");
            return;
        }

        string charName = nameInput != null ? nameInput.text.Trim() : "";
        string textProfile = profileInput != null ? profileInput.text.Trim() : "";

        if (string.IsNullOrEmpty(charName))
        {
            SetStatus("캐릭터 이름을 입력하세요.");
            return;
        }

        if (string.IsNullOrEmpty(_selectedImagePath))
        {
            SetStatus("이미지를 선택하세요.");
            return;
        }

        // 동기 방식으로 등록 (서버 업로드 없이 로컬 경로만 저장)
        var profile = CharacterManager.Instance.RegisterCharacter(charName, textProfile, _selectedImagePath);

        if (profile != null)
        {
            SetStatus($"'{profile.name}' 등록 완료! (@char({profile.name}) 으로 사용)");
            ClearInputs();
            RefreshCharacterList();
        }
        else
        {
            SetStatus("등록 실패 (이름 중복 또는 파일 오류)");
        }
    }

    void ClearInputs()
    {
        if (nameInput != null) nameInput.text = "";
        if (profileInput != null) profileInput.text = "";
        if (previewImage != null) previewImage.texture = null;
        _selectedImagePath = null;
    }

    void SetStatus(string msg)
    {
        Debug.Log($"[CharacterRegisterUI] {msg}");
        if (statusText != null)
            statusText.text = msg;
    }

    // ======================== 캐릭터 목록 표시 ========================

    public void RefreshCharacterList()
    {
        if (characterListParent == null || CharacterManager.Instance == null)
            return;

        // 기존 아이템 제거
        for (int i = characterListParent.childCount - 1; i >= 0; i--)
            Destroy(characterListParent.GetChild(i).gameObject);

        // 캐릭터 목록 표시
        foreach (var profile in CharacterManager.Instance.characters)
        {
            if (characterItemPrefab != null)
            {
                var go = Instantiate(characterItemPrefab, characterListParent);
                var itemUI = go.GetComponent<CharacterItemUI>();
                if (itemUI != null)
                    itemUI.Setup(profile, this);
            }
            else
            {
                // 프리팹이 없으면 간단한 텍스트로 표시
                var go = new GameObject(profile.name);
                go.transform.SetParent(characterListParent, false);
                var text = go.AddComponent<Text>();
                text.text = $"@char {profile.name}";
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.color = Color.white;
            }
        }
    }

    /// <summary>
    /// 캐릭터 삭제 (CharacterItemUI에서 호출)
    /// </summary>
    public void OnDeleteCharacter(string name)
    {
        if (CharacterManager.Instance == null)
            return;

        if (CharacterManager.Instance.DeleteCharacter(name))
        {
            SetStatus($"'{name}' 삭제됨");
            RefreshCharacterList();
        }
    }
}
