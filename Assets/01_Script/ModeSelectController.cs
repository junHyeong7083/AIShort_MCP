using UnityEngine;
using UnityEngine.UI;

public enum GenerationMode
{
    TextOnly = 0,
    ImageAndText = 1,
    // 나중에 필요하면 여기 계속 추가 (예: AudioOnly = 2, Etc = 3 ...)
}

public class ModeSelectController : MonoBehaviour
{
    [Header("모드 선택 Dropdown (0=TextOnly, 1=ImageAndText, ...")]
    [SerializeField] private Dropdown modeDropdown;

    [Header("모드별 루트 패널들 (index를 enum 값에 맞게 세팅)")]
    [SerializeField] private GameObject[] panels;

    public GenerationMode CurrentMode { get; private set; } = GenerationMode.TextOnly;

    // enum -> index 변환 (사실 캐스트 하나지만, 나중에 로직 추가할 수도 있으니 함수로 래핑)
    int IndexOf(GenerationMode mode)
    {
        return (int)mode;
    }

    /// <summary>
    /// 버튼 OnClick에 연결해서 쓰는 함수
    /// (Dropdown으로 고르고 → 버튼 눌러서 모드 적용)
    /// </summary>
    public void OnClick_ApplyMode()
    {
        ApplyModeFromDropdown();
    }

    private void ApplyModeFromDropdown()
    {
        if (modeDropdown == null)
        {
            Debug.LogError("[ModeSelectController] modeDropdown이 설정되어 있지 않습니다.");
            return;
        }

        if (panels == null || panels.Length == 0)
        {
            Debug.LogError("[ModeSelectController] panels 배열이 비어있습니다.");
            return;
        }

        // Dropdown value(0,1,2,...)를 enum으로 캐스팅
        CurrentMode = (GenerationMode)modeDropdown.value;

        int activeIndex = IndexOf(CurrentMode);

        // 안전 체크: enum 값이 panels 범위 밖이면 그냥 무시
        if (activeIndex < 0 || activeIndex >= panels.Length)
        {
            Debug.LogWarning($"[ModeSelectController] activeIndex({activeIndex})가 panels 길이({panels.Length}) 범위 밖입니다.");
            return;
        }

        // 모든 패널 비활성화 후, 현재 모드 패널만 활성화
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
                panels[i].SetActive(i == activeIndex);
        }

        Debug.Log($"[ModeSelectController] 모드 적용: {CurrentMode} (index={activeIndex})");
    }

    public void OnClick_PreStep()
    {
        PreStep();
    }
    private void PreStep()
    {
        for (int e = 0; e < panels.Length; ++ e)
            panels[e].gameObject.SetActive(false);

    }
}
