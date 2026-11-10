using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A prefab-authored health bar shown in the foreground combat canvas.</summary>
[DisallowMultipleComponent]
public sealed class EnemyHealthBarView : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private Image missing;
    [SerializeField] private Image damageTrail;
    [SerializeField] private TextMeshProUGUI number;
    private RectTransform frameRect;
    private float targetFraction = 1f;
    private float displayedFraction = 1f;
    private float trailFraction = 1f;
    private float fillVelocity;
    private float trailVelocity;
    private float trailDelay;
    private float hitPulse;
    private bool initialized;
    private bool animating;
    private string lastText;

    public RectTransform RectTransform => (RectTransform)transform;

    private void OnEnable()
    {
        // Bars are pooled, so the next enemy must start at its own HP value.
        initialized = false; animating = false; lastText = null;
        fillVelocity = 0f;
        trailVelocity = 0f;
        trailDelay = 0f;
        hitPulse = 0f;
        if (number != null) number.rectTransform.localScale = Vector3.one;
    }

    public void SetValue(string value, float fraction)
    {
        if (number != null && lastText != value) { number.text = value; lastText = value; }
        float nextFraction = Mathf.Clamp01(fraction);
        if (!initialized || !Application.isPlaying)
        {
            targetFraction = displayedFraction = trailFraction = nextFraction;
            initialized = true;
            UpdateFillGeometry();
            return;
        }

        if (nextFraction < targetFraction - 0.0001f)
        {
            trailFraction = Mathf.Max(trailFraction, displayedFraction);
            trailDelay = 0.12f;
            trailVelocity = 0f;
            hitPulse = 1f;
        }
        else if (nextFraction > targetFraction + 0.0001f)
        {
            trailFraction = displayedFraction;
            trailDelay = 0f;
            trailVelocity = 0f;
            fillVelocity = 0f;
        }
        if (nextFraction != targetFraction) animating = true;
        targetFraction = nextFraction;
    }

    private void Update()
    {
        if (!initialized || !animating) return;
        float delta = Time.unscaledDeltaTime;
        displayedFraction = Mathf.SmoothDamp(displayedFraction, targetFraction,
            ref fillVelocity, 0.13f, Mathf.Infinity, delta);
        if (Mathf.Abs(displayedFraction - targetFraction) < 0.001f)
        {
            displayedFraction = targetFraction;
            fillVelocity = 0f;
        }

        if (trailDelay > 0f) trailDelay -= delta;
        else trailFraction = Mathf.SmoothDamp(trailFraction, displayedFraction,
            ref trailVelocity, 0.24f, Mathf.Infinity, delta);
        trailFraction = Mathf.Max(trailFraction, displayedFraction);
        if (Mathf.Abs(trailFraction - displayedFraction) < 0.001f)
        {
            trailFraction = displayedFraction;
            trailVelocity = 0f;
        }

        hitPulse = Mathf.MoveTowards(hitPulse, 0f, delta / 0.26f);
        if (number != null)
            number.rectTransform.localScale = Vector3.one * (1f + 0.07f * hitPulse * hitPulse);
        UpdateFillGeometry();
        animating = displayedFraction != targetFraction || trailFraction != displayedFraction || trailDelay > 0f || hitPulse > 0f;
    }

    public void SetDimensions(float width, float height)
    {
        RectTransform root = RectTransform;
        Vector2 size = new Vector2(Mathf.Max(24f, width), Mathf.Max(28f, height));
        if ((root.sizeDelta - size).sqrMagnitude < 0.01f) return;
        root.sizeDelta = size;
        if (frameRect == null) frameRect = transform.Find("Generated Frame") as RectTransform;
        if (frameRect != null) frameRect.sizeDelta = size;
        if (missing == null) missing = transform.Find("Dark Track")?.GetComponent<Image>();
        Vector2 trackSize = new Vector2(size.x * 0.82f, size.y * 0.62f);
        if (missing != null) missing.rectTransform.sizeDelta = trackSize;
        UpdateFillGeometry();
        if (number != null)
        {
            number.rectTransform.sizeDelta = size;
            number.fontSize = Mathf.Clamp(size.y * 0.61f, 17f, 25f);
        }
    }

    private void UpdateFillGeometry()
    {
        if (fill == null) return;
        Vector2 size = RectTransform.sizeDelta;
        float trackWidth = size.x * 0.82f;
        // Sprite-less UI Images ignore fillAmount. Animate the two authored
        // rectangles from the same left edge so the red track stays visible.
        SetSegmentWidth(fill.rectTransform, displayedFraction, trackWidth, size.y * 0.62f);
        if (damageTrail != null)
            SetSegmentWidth(damageTrail.rectTransform, trailFraction, trackWidth, size.y * 0.62f);
    }

    private static void SetSegmentWidth(RectTransform rect, float fraction, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(-width * 0.5f, 0f);
        rect.sizeDelta = new Vector2(width * fraction, height);
    }

#if UNITY_EDITOR
    public void Configure(Image fillImage, Image missingImage, Image trailImage,
        TextMeshProUGUI numberLabel)
    {
        fill = fillImage;
        missing = missingImage;
        damageTrail = trailImage;
        number = numberLabel;
    }
#endif
}
