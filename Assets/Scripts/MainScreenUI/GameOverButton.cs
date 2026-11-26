using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 게임 오버 화면 버튼의 선택 연출 (메인 메뉴와 같은 크림색 → 금색 + 양옆 다이아몬드).
/// 마우스를 올리면 그 버튼이 선택되므로 키보드·마우스 연출이 하나로 통일된다.
/// Time.timeScale 이 0 이어도 동작하도록 실제 시간으로 움직인다.
/// </summary>
[RequireComponent(typeof(Button))]
public class GameOverButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    private static readonly Color Idle = new Color(1f, 0.94f, 0.77f);
    private static readonly Color Accent = new Color(1f, 0.79f, 0.34f);

    private Text label;
    private Graphic[] diamonds;
    private bool selected;
    private float blend;

    public void Configure(Text label, params Graphic[] diamonds)
    {
        this.label = label;
        this.diamonds = diamonds;
        Apply();
    }

    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        var button = GetComponent<Button>();
        if (button.interactable && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    private void Update()
    {
        blend = Mathf.MoveTowards(blend, selected ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        Apply();
    }

    private void Apply()
    {
        float e = blend * blend * (3f - 2f * blend);
        if (label != null) label.color = Color.Lerp(Idle, Accent, e);
        transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.06f, e);
        if (diamonds == null) return;
        foreach (Graphic d in diamonds)
        {
            if (d == null) continue;
            d.color = new Color(Accent.r, Accent.g, Accent.b, e);
            d.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, e);
        }
    }
}
