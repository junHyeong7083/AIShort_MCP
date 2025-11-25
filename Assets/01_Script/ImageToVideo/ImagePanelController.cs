using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ImagePanelController : MonoBehaviour
{
    [Header("이미지 선택 버튼")]
    [SerializeField] private Button selectImageButton;

    [Header("선택된 경로 표시 텍스트 (옵션)")]
    [SerializeField] private Text imagePathText;

    [Header("이미지 미리보기 (옵션, RawImage 권장)")]
    [SerializeField] private RawImage previewImage;

    [Header("Runway 플로우 (이미지 경로 넘겨줄 곳)")]
    [SerializeField] private RunwayImageToVideoFlow runwayFlow;

    private string _imagePath;

    private void Awake()
    {
        if (selectImageButton != null)
            selectImageButton.onClick.AddListener(OnClick_SelectImage);
    }

    public void OnClick_SelectImage()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel(
            "영상에 사용할 이미지 선택",
            "",
            "png,jpg,jpeg"
        );

        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("[ImagePanelController] 이미지 선택 취소");
            return;
        }

        _imagePath = path;
        Debug.Log("[ImagePanelController] 선택된 이미지 경로: " + _imagePath);

        // 1) 경로 텍스트 갱신
        if (imagePathText != null)
            imagePathText.text = _imagePath;

        // 2) Runway 플로우에 경로 전달
        if (runwayFlow != null)
            runwayFlow.SetLocalImagePath(_imagePath);

        // 3) 미리보기 갱신 (옵션)
        if (previewImage != null)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(_imagePath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    previewImage.texture = tex;
                    // 비율 맞추고 싶으면 여기서 rectTransform 조절 가능
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ImagePanelController] 미리보기 로드 실패: " + e.Message);
            }
        }
#else
        Debug.LogWarning("[ImagePanelController] 로컬 파일 선택은 에디터 전용입니다. 빌드에서는 별도 파일 선택 UI가 필요합니다.");
#endif
    }
}
