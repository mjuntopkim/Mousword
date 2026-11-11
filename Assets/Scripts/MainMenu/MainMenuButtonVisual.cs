using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuButtonVisual : MonoBehaviour, IPointerEnterHandler,
    ISelectHandler, IDeselectHandler
{
    [SerializeField] private Text label;
    [SerializeField] private Image diamond;
    [SerializeField] private bool centeredLabel;
    private Selectable selectable;
    private float blend;
    private readonly Color idle = new Color(1f, 0.94f, 0.77f);
    private readonly Color accent = new Color(1f, 0.79f, 0.34f);

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData data)
    {
        // Mouse and keyboard share EventSystem's single selection.
        if (selectable.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject, data);
    }

    public void OnSelect(BaseEventData data) { ApplyVisual(); }

    public void OnDeselect(BaseEventData data)
    {
        // Clear immediately so the old button never remains highlighted during a fade.
        blend = 0;
        ApplyVisual();
    }

    private void Update()
    {
        bool selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        blend = selected ? Mathf.MoveTowards(blend, 1f, Time.unscaledDeltaTime * 6f) : 0f;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        label.color = Color.Lerp(idle, accent, blend);
        label.rectTransform.anchoredPosition = new Vector2(centeredLabel ? 0f : 10f * blend, 0);
        diamond.color = new Color(accent.r, accent.g, accent.b, blend);
        diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f + Mathf.Sin(Time.unscaledTime * 1.4f) * 8f);
    }

    private void OnDisable()
    {
        blend = 0;
        ApplyVisual();
    }
}
