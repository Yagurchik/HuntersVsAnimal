using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public sealed class UpgradeChoiceCardMotion : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField, Range(0, 2)] private int appearanceOrder;
    [SerializeField, Range(1f, 1.08f)] private float hoverScale = 1.035f;
    [SerializeField, Range(0.9f, 1f)] private float pressedScale = 0.975f;
    [SerializeField, Min(1f)] private float response = 12f;

    private RectTransform rect;
    private CanvasGroup group;
    private float age;
    private bool hovered;
    private bool pressed;

    public void Configure(int order)
    {
        appearanceOrder = Mathf.Clamp(order, 0, 2);
    }

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (rect == null)
        {
            rect = (RectTransform)transform;
        }

        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
        }

        hovered = pressed = false;
        if (!Application.isPlaying)
        {
            rect.localScale = Vector3.one;
            group.alpha = 1f;
            return;
        }
        age = -appearanceOrder * 0.07f;
        rect.localScale = Vector3.one * 0.9f;
        group.alpha = 0f;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        age += Time.unscaledDeltaTime;
        float reveal = Mathf.Clamp01(age / 0.2f);
        group.alpha = reveal;
        float target = pressed ? pressedScale : hovered ? hoverScale : 1f;
        float shownScale = Mathf.Lerp(0.9f, target, 1f - Mathf.Pow(1f - reveal, 3f));
        rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * shownScale,
            1f - Mathf.Exp(-response * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
}
