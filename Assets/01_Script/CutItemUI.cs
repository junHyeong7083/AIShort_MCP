using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CutItemUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI References")]
    public Text titleText;
    public Text descriptionText;
    public Image background;   // 핑크 배경 Image

    [Header("Colors")]
    public Color normalColor = new Color(0.93f, 0.78f, 0.78f, 1f);
    public Color hoverColor = new Color(0.96f, 0.84f, 0.84f, 1f);
    public Color selectedColor = new Color(0.90f, 0.60f, 0.60f, 1f);

    [HideInInspector] public CutInfo cutInfo;   // 이 카드가 가진 데이터

    UIBinding owner;
    bool isSelected;

    // UIBinding에서 세팅할 함수
    public void Setup(UIBinding owner, CutInfo info, string title, string description)
    {
        this.owner = owner;
        this.cutInfo = info;

        if (titleText != null)
            titleText.text = $"Cut {info.index} — {title}";

        if (descriptionText != null)
            descriptionText.text = description;

        SetSelected(false);
    }

    // 선택/해제 외부에서 바꾸게 해줄 함수
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }

    // ================= Mouse Events =================

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isSelected) return;
        if (background != null)
            background.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected) return;
        if (background != null)
            background.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null)
            owner.OnCutItemClicked(this);
    }
}
