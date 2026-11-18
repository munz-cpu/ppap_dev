using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public sealed class MenuMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField, Min(0f)] private float delay;
    [SerializeField, Min(0.01f)] private float duration = 0.34f;
    [SerializeField, Min(0f)] private float slideDistance = 14f;
    [SerializeField] private bool reactsToPointer;

    private RectTransform rect;
    private CanvasGroup group;
    private Selectable selectable;
    private Vector2 restingPosition;
    private Vector3 restingScale;
    private float elapsed;
    private float interactionScale;
    private bool hovered;
    private bool selected;
    private bool pressed;

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        selectable = GetComponent<Selectable>();
        restingPosition = rect.anchoredPosition;
        restingScale = rect.localScale;
    }

    private void OnEnable()
    {
        hovered = selected = pressed = false;
        elapsed = 0f;
        interactionScale = 1f;
        rect.localScale = restingScale;
        SetEntrance(0f);
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime;
        if (elapsed < delay + duration)
        {
            elapsed += step;
            SetEntrance(Mathf.Clamp01((elapsed - delay) / duration));
        }

        if (!reactsToPointer)
            return;

        float target = 1f;
        if (selectable == null || selectable.IsInteractable())
            target = pressed ? 0.95f : hovered || selected ? 1.045f : 1f;
        interactionScale = Mathf.Lerp(interactionScale, target, 1f - Mathf.Exp(-step * 21f));
        rect.localScale = restingScale * interactionScale;
    }

    private void SetEntrance(float progress)
    {
        float rest = 1f - progress;
        float eased = 1f - rest * rest * rest;
        group.alpha = Mathf.Clamp01(progress * 2f);
        if (reactsToPointer)
            group.blocksRaycasts = progress >= 0.18f;
        rect.anchoredPosition = restingPosition - Vector2.up * (slideDistance * (1f - eased));
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) => pressed = eventData.button == PointerEventData.InputButton.Left;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;
}
