using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 목록의 개별 아이템 UI
/// </summary>
public class CharacterItemUI : MonoBehaviour
{
    [Header("UI References")]
    public Text nameText;
    public Text profileText;
    public RawImage thumbnailImage;
    public Button deleteButton;

    private CharacterProfile _profile;
    private CharacterRegisterUI _parentUI;

    public void Setup(CharacterProfile profile, CharacterRegisterUI parentUI)
    {
        _profile = profile;
        _parentUI = parentUI;

        if (nameText != null)
            nameText.text = $"@char {profile.name}";

        if (profileText != null)
            profileText.text = string.IsNullOrEmpty(profile.textProfile)
                ? "(프로필 없음)"
                : profile.textProfile;

        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnClickDelete);

        // 썸네일 로드 (로컬 이미지가 있으면)
        if (thumbnailImage != null && !string.IsNullOrEmpty(profile.imagePath))
        {
            LoadThumbnail(profile.imagePath);
        }
    }

    void OnDestroy()
    {
        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(OnClickDelete);
    }

    void LoadThumbnail(string path)
    {
        if (!System.IO.File.Exists(path))
            return;

        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            thumbnailImage.texture = tex;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CharacterItemUI] 썸네일 로드 실패: {e.Message}");
        }
    }

    void OnClickDelete()
    {
        if (_profile != null && _parentUI != null)
        {
            _parentUI.OnDeleteCharacter(_profile.name);
        }
    }
}
