using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only: a combo never changes damage, experience or turn timing.
[DisallowMultipleComponent]
[DefaultExecutionOrder(1100)]
public sealed class ProjectileComboUI : MonoBehaviour
{
    [Header("Editable HUD")]
    [SerializeField] private RectTransform safeArea;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private TextMeshProUGUI countLabel;
    [SerializeField] private TextMeshProUGUI captionLabel;
    [SerializeField] private Image accent;
    [Header("Animation")]
    [SerializeField, Min(0.1f)] private float resultHold = 1.1f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.35f;
    [SerializeField, Min(0.05f)] private float pulseDuration = 0.16f;
    [SerializeField, Range(0f, 0.4f)] private float pulseScale = 0.17f;
    [SerializeField] private Color normalColor = new Color(1f, 0.79f, 0.31f);
    [SerializeField] private Color hotColor = new Color(1f, 0.43f, 0.12f);

    private float resultRemaining;
    private float pulseRemaining;
    private Vector3 labelScale = Vector3.one;
    private Rect lastSafeArea;
    private Vector2Int lastScreen;
    public static ProjectileComboUI Instance { get; private set; }
    public int HitCount { get; private set; }
    public int VolleyId { get; private set; }
    public bool IsCounting { get; private set; }
    public int PendingEffects { get; private set; }
    public float VisibleAlpha => visibility != null ? visibility.alpha : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => Instance = null;

    public static ProjectileComboUI Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ProjectileComboUI ui = FindObjectOfType<ProjectileComboUI>(true);
        if (ui == null)
        {
            var root = new GameObject("Projectile Combo HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(ProjectileComboUI));
            ui = root.GetComponent<ProjectileComboUI>();
            ui.BuildInterface();
        }
        if (Application.isPlaying)
        {
            Instance = ui;
        }

        return ui;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (visibility == null)
        {
            BuildInterface();
        }

        labelScale = countLabel != null ? countLabel.transform.localScale : Vector3.one;
        Hide();
        RefreshSafeArea();
    }

    public int BeginVolley()
    {
        if (visibility == null)
        {
            BuildInterface();
        }

        VolleyId = VolleyId == int.MaxValue ? 1 : VolleyId + 1;
        HitCount = PendingEffects = 0;
        IsCounting = true;
        resultRemaining = 0f;
        pulseRemaining = pulseDuration;
        RefreshLabels();
        visibility.alpha = 1f;
        return VolleyId;
    }

    public void RegisterHit(int volleyId)
    {
        if (!IsCounting || volleyId == 0 || volleyId != VolleyId)
        {
            return;
        }

        HitCount++;
        pulseRemaining = pulseDuration;
        RefreshLabels();
    }

    public bool BeginEffect(int volleyId)
    {
        if (!IsCounting || volleyId == 0 || volleyId != VolleyId)
        {
            return false;
        }

        PendingEffects++;
        return true;
    }

    public void EndEffect(int volleyId)
    {
        if (volleyId == VolleyId)
        {
            PendingEffects = Mathf.Max(0, PendingEffects - 1);
        }
    }

    private void LateUpdate()
    {
        RefreshSafeArea();
        GameManager manager = GameManager.Instance;
        if (manager != null && manager.GameEnded) { Hide(); return; }
        if (IsCounting && !Player.HasActiveProjectiles && PendingEffects == 0)
        {
            IsCounting = false;
            resultRemaining = resultHold + fadeDuration;
            if (captionLabel != null)
            {
                captionLabel.text = "попаданий за залп";
            }
        }
        bool paused = manager != null && manager.isPaused;
        bool modal = manager != null && (manager.IsChoosingUpgrade || manager.IsFrontMapOpen);
        if (paused) { if (visibility != null) { visibility.alpha = 0f; } return; }
        float delta = Time.unscaledDeltaTime;
        pulseRemaining = Mathf.Max(0f, pulseRemaining - delta);
        if (countLabel != null)
        {
            float t = pulseRemaining / Mathf.Max(0.05f, pulseDuration);
            countLabel.transform.localScale = labelScale * (1f + pulseScale * t * t);
        }
        if (!IsCounting)
        {
            resultRemaining = Mathf.Max(0f, resultRemaining - delta);
        }

        if (visibility != null)
        {
            visibility.alpha = modal ? 0f : IsCounting ? 1f
            : Mathf.Clamp01(resultRemaining / Mathf.Max(0.05f, fadeDuration));
        }
    }

    private void RefreshLabels()
    {
        Color color = Color.Lerp(normalColor, hotColor, Mathf.Clamp01(HitCount / 40f));
        if (countLabel != null) { countLabel.SetText("{0}", HitCount); countLabel.color = color; }
        if (accent != null)
        {
            accent.color = color;
        }

        if (captionLabel != null)
        {
            captionLabel.text = "попаданий";
        }
    }

    private void Hide()
    {
        IsCounting = false;
        PendingEffects = 0;
        resultRemaining = pulseRemaining = 0f;
        if (visibility != null)
        {
            visibility.alpha = 0f;
        }

        if (countLabel != null)
        {
            countLabel.transform.localScale = labelScale;
        }
    }

    private void OnDisable() => Hide();
    private void OnDestroy() { if (Instance == this)
        {
            Instance = null;
        }
    }

    private void RefreshSafeArea()
    {
        if (safeArea == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        var screen = new Vector2Int(Screen.width, Screen.height);
        Rect area = Screen.safeArea;
        if (lastSafeArea == area && lastScreen == screen)
        {
            return;
        }

        lastSafeArea = area;
        lastScreen = screen;
        if (area.width <= 0f || area.height <= 0f)
        {
            area = new Rect(0, 0, Screen.width, Screen.height);
        }

        safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
    }

    public void Preview(int hits, bool show)
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (visibility == null)
        {
            BuildInterface();
        }

        HitCount = Mathf.Max(0, hits);
        RefreshLabels();
        visibility.alpha = show ? 1f : 0f;
        RefreshSafeArea();
    }

    [ContextMenu("Preview combo (24 hits)")]
    private void PreviewCombo() => Preview(24, true);
    [ContextMenu("Hide combo preview")]
    private void HidePreview() => Preview(0, false);

    public void BuildInterface()
    {
        if (visibility != null)
        {
            return; // Never rebuild the artist's authored hierarchy.
        }

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 230; // Below map, upgrades and pause; above the battlefield darkness.
        transform.localScale = Vector3.one;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        safeArea.SetParent(transform, false);
        Stretch(safeArea, Vector2.zero, Vector2.one);
        RectTransform panel = MakeImage("Combo Plate", safeArea, new Color(0.055f, 0.085f, 0.052f, 0.96f)).rectTransform;
        ApplyRecommendedLayout(panel);
        visibility = panel.gameObject.AddComponent<CanvasGroup>();
        visibility.blocksRaycasts = visibility.interactable = false;
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.44f, 0.33f, 0.15f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        accent = MakeImage("Amber Accent", panel, normalColor);
        Stretch(accent.rectTransform, new Vector2(0.15f, 0.96f), new Vector2(0.85f, 0.98f));
        var title = MakeText("Combo Title", panel, 23f, new Color(0.84f, 0.79f, 0.6f));
        title.text = "КОМБО";
        title.characterSpacing = 6f;
        Stretch(title.rectTransform, new Vector2(0.06f, 0.7f), new Vector2(0.94f, 0.93f));
        countLabel = MakeText("Hit Count", panel, 59f, normalColor);
        countLabel.enableAutoSizing = true;
        countLabel.fontSizeMin = 28f;
        countLabel.fontSizeMax = 59f;
        Stretch(countLabel.rectTransform, new Vector2(0.08f, 0.23f), new Vector2(0.92f, 0.72f));
        captionLabel = MakeText("Combo Caption", panel, 18f, new Color(0.72f, 0.77f, 0.62f));
        Stretch(captionLabel.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.25f));
        RefreshLabels();
        visibility.alpha = 0f;
    }

    public void ApplyRecommendedLayout()
    {
        if (visibility != null)
        {
            ApplyRecommendedLayout(visibility.transform as RectTransform);
        }
    }

    private static void ApplyRecommendedLayout(RectTransform panel)
    {
        if (panel == null)
        {
            return;
        }

        panel.anchorMin = panel.anchorMax = new Vector2(0.97f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-54f, -168f);
        panel.sizeDelta = new Vector2(220f, 86f);
    }

    private static Image MakeImage(string name, Transform parent, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, float size, Color color)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        GameTypography.Apply(label);
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        return label;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
