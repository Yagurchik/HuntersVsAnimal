using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyFirstEncounterUI : MonoBehaviour
{
    private sealed class Notice
    {
        public string TypeId;
        public string Title;
        public string Summary;
        public string Stats;
        public Sprite Portrait;
        public Color Accent;
    }

    private static EnemyFirstEncounterUI instance;

    [Header("Timing")]
    [SerializeField, Min(1f)] private float displayDuration = 5f;
    [SerializeField, Range(0.05f, 1f)] private float fadeInDuration = 0.24f;
    [SerializeField, Range(0.05f, 1f)] private float fadeOutDuration = 0.34f;

    [Header("Editable first encounter card")]
    [SerializeField] private Canvas encounterCanvas;
    [SerializeField] private RectTransform card;
    [SerializeField] private CanvasGroup cardGroup;
    [SerializeField] private Image accentBar;
    [SerializeField] private Image portraitFrame;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Text captionText;
    [SerializeField] private Text titleText;
    [SerializeField] private Text summaryText;
    [SerializeField] private Text statsText;

    private readonly HashSet<string> seenTypes = new HashSet<string>();
    private readonly Queue<Notice> pendingNotices = new Queue<Notice>();
    private Coroutine queueRoutine;
    private Vector2 shownPosition;

    public static EnemyFirstEncounterUI Instance => instance;
    public float DisplayDuration => Mathf.Max(1f, displayDuration);
    public int SeenTypeCount => seenTypes.Count;
    public int PendingNoticeCount => pendingNotices.Count;
    public Canvas EncounterCanvas => encounterCanvas;
    public RectTransform Card => card;
    public CanvasGroup CardGroup => cardGroup;
    public Text TitleText => titleText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => instance = null;

    public static EnemyFirstEncounterUI Ensure()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<EnemyFirstEncounterUI>(true);
        if (instance == null)
        {
            GameObject root = new GameObject("Enemy First Encounter UI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(EnemyFirstEncounterUI));
            instance = root.GetComponent<EnemyFirstEncounterUI>();
            instance.BuildEditableUI();
        }
        else
        {
            instance.ConfigureCanvas();
            if (!instance.HasCompleteUI())
            {
                instance.BuildEditableUI();
            }
        }
        return instance;
    }

    public static void ReportSpawned(Brick enemy)
    {
        if (!Application.isPlaying || enemy == null)
        {
            return;
        }

        Ensure().Register(enemy);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        ConfigureCanvas();
        if (!HasCompleteUI())
        {
            BuildEditableUI();
        }

        HideImmediately();
    }

    private void Register(Brick enemy)
    {
        Notice notice = CreateNotice(enemy);
        if (notice == null || string.IsNullOrWhiteSpace(notice.TypeId)
            || !seenTypes.Add(notice.TypeId))
        {
            return;
        }

        pendingNotices.Enqueue(notice);
        if (queueRoutine == null && isActiveAndEnabled)
        {
            queueRoutine = StartCoroutine(ShowQueue());
        }
    }

    private Notice CreateNotice(Brick enemy)
    {
        EnemyFirstEncounterProfile profile = enemy.GetComponent<EnemyFirstEncounterProfile>();
        bool summoned = enemy.IsSummonedVariant;
        string typeId;
        string title;
        string summary;
        Color accent;

        if (summoned)
        {
            typeId = "summoned_skeleton";
            title = "Призванный скелет";
            summary = "Зелёный слуга лича. Двигается как обычный скелет, но не приносит опыта.";
            accent = new Color(0.55f, 0.82f, 0.34f, 1f);
        }
        else if (profile != null)
        {
            typeId = profile.EnemyTypeId;
            title = profile.DisplayName;
            summary = profile.Summary;
            accent = profile.AccentColor;
        }
        else
        {
            ResolveFallback(enemy, out typeId, out title, out summary, out accent);
        }

        string experience = enemy.ExperienceReward > 0
            ? "ОПЫТ  " + enemy.ExperienceReward
            : "БЕЗ ОПЫТА";
        Vector2Int footprint = enemy.Footprint;
        string size = footprint == Vector2Int.one
            ? string.Empty
            : "  ·  РАЗМЕР  " + footprint.x + "×" + footprint.y;

        return new Notice
        {
            TypeId = typeId,
            Title = title,
            Summary = summary,
            Stats = "ЗДОРОВЬЕ  " + CompactNumberFormatter.Format(enemy.MaxHp)
                + "  ·  " + experience + size,
            Portrait = profile != null && profile.Portrait != null
                ? profile.Portrait
                : FindPortrait(enemy),
            Accent = accent
        };
    }

    private static void ResolveFallback(Brick enemy, out string typeId, out string title,
        out string summary, out Color accent)
    {
        if (enemy.TryGetComponent(out ZombieResurrection _))
        {
            typeId = "zombie";
            title = "Зомби";
            summary = "После первой смерти оставляет надгробие с полным запасом здоровья. Разбей его за ход, иначе зомби восстанет с половиной здоровья.";
            accent = new Color(0.58f, 0.72f, 0.25f, 1f);
            return;
        }
        if (enemy.TryGetComponent(out ArmoredSkeletonArmor _))
        {
            typeId = "armored_skeleton";
            title = "Скелет в доспехах";
            summary = "Атаки сверху наносят ему только половину урона. Ищи боковой или нижний угол.";
            accent = new Color(0.72f, 0.76f, 0.68f, 1f);
            return;
        }
        if (enemy.TryGetComponent(out LichSummoner _))
        {
            bool boss = enemy.Footprint.x > 1 || enemy.Footprint.y > 1;
            typeId = boss ? "lich_boss" : "lich";
            title = boss ? "Лич-хранитель" : "Лич";
            summary = boss
                ? "Занимает четыре клетки и каждый ход призывает рядом от одного до трёх зелёных скелетов."
                : "Каждый ход призывает рядом от одного до трёх зелёных скелетов. Призывы не приносят опыта.";
            accent = new Color(0.52f, 0.82f, 0.38f, 1f);
            return;
        }

        string cleanName = enemy.gameObject.name.Replace("(Clone)", string.Empty).Trim();
        bool baseSkeleton = cleanName == "Brick" || string.IsNullOrWhiteSpace(cleanName);
        typeId = baseSkeleton ? "forest_skeleton" : cleanName.ToLowerInvariant();
        title = baseSkeleton ? "Лесной скелет" : cleanName;
        summary = baseSkeleton
            ? "Шагает к линии поражения на одну клетку за ход. Останови его до прорыва."
            : "Новый противник. Изучи его поведение и не подпускай к линии поражения.";
        accent = new Color(0.92f, 0.49f, 0.16f, 1f);
    }

    private static Sprite FindPortrait(Brick enemy)
    {
        SpriteRenderer[] renderers = enemy.GetComponentsInChildren<SpriteRenderer>(true);
        Sprite fallback = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = renderer.sprite;
            }

            if (renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return renderer.sprite;
            }
        }
        return fallback;
    }

    private IEnumerator ShowQueue()
    {
        // Enemy Start calls can happen before the first-level tutorial activates
        // on the following frame. Give that flow time to establish its UI lock.
        yield return null;
        yield return null;

        while (pendingNotices.Count > 0)
        {
            while (ShouldWaitForTutorial())
            {
                yield return null;
            }

            if (GameManager.Instance != null && GameManager.Instance.GameEnded)
            {
                break;
            }

            Notice notice = pendingNotices.Dequeue();
            ApplyNotice(notice);
            float fadeIn = Mathf.Min(fadeInDuration, DisplayDuration * 0.25f);
            float fadeOut = Mathf.Min(fadeOutDuration, DisplayDuration * 0.3f);
            float hold = Mathf.Max(0f, DisplayDuration - fadeIn - fadeOut);
            yield return AnimateCard(0f, 1f, shownPosition + Vector2.right * 42f,
                shownPosition, fadeIn);
            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }

            yield return AnimateCard(1f, 0f, shownPosition,
                shownPosition + Vector2.right * 42f, fadeOut);
        }

        pendingNotices.Clear();
        HideImmediately();
        queueRoutine = null;
    }

    private static bool ShouldWaitForTutorial()
    {
        GameManager manager = GameManager.Instance;
        return manager != null && manager.IsTutorialFlowActive;
    }

    private void ApplyNotice(Notice notice)
    {
        captionText.text = "ВПЕРВЫЕ НА ЭТОМ УРОВНЕ";
        titleText.text = notice.Title;
        summaryText.text = notice.Summary;
        statsText.text = notice.Stats;
        accentBar.color = notice.Accent;
        portraitFrame.color = Color.Lerp(new Color(0.08f, 0.045f, 0.025f, 0.98f),
            notice.Accent, 0.18f);
        portraitImage.sprite = notice.Portrait;
        portraitImage.enabled = notice.Portrait != null;
        portraitImage.color = Color.white;
        card.gameObject.SetActive(true);
    }

    private IEnumerator AnimateCard(float fromAlpha, float toAlpha, Vector2 fromPosition,
        Vector2 toPosition, float duration)
    {
        float elapsed = 0f;
        cardGroup.alpha = fromAlpha;
        card.anchoredPosition = fromPosition;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            cardGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            card.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, eased);
            yield return null;
        }
        cardGroup.alpha = toAlpha;
        card.anchoredPosition = toPosition;
    }

    public void BuildEditableUI()
    {
        ConfigureCanvas();
        Transform oldCard = transform.Find("First Encounter Card");
        if (oldCard != null)
        {
            if (Application.isPlaying)
            {
                Destroy(oldCard.gameObject);
            }
            else
            {
                DestroyImmediate(oldCard.gameObject);
            }
        }

        card = CreateRect("First Encounter Card", transform);
        card.anchorMin = card.anchorMax = new Vector2(1f, 1f);
        card.pivot = new Vector2(1f, 1f);
        card.anchoredPosition = new Vector2(-30f, -145f);
        card.sizeDelta = new Vector2(500f, 238f);
        shownPosition = card.anchoredPosition;

        Image background = card.gameObject.AddComponent<Image>();
        background.color = new Color(0.075f, 0.038f, 0.022f, 0.96f);
        background.raycastTarget = false;
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.98f, 0.49f, 0.13f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);
        Shadow shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(-7f, -9f);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.interactable = false;
        cardGroup.blocksRaycasts = false;

        RectTransform accentRect = CreateRect("Amber Accent", card);
        Stretch(accentRect, Vector2.zero, new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(9f, 0f));
        accentBar = accentRect.gameObject.AddComponent<Image>();
        accentBar.color = new Color(0.96f, 0.45f, 0.12f, 1f);
        accentBar.raycastTarget = false;

        RectTransform portraitRect = CreateRect("Portrait Frame", card);
        portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(27f, -2f);
        portraitRect.sizeDelta = new Vector2(126f, 154f);
        portraitFrame = portraitRect.gameObject.AddComponent<Image>();
        portraitFrame.color = new Color(0.18f, 0.095f, 0.045f, 1f);
        portraitFrame.raycastTarget = false;
        Outline portraitOutline = portraitRect.gameObject.AddComponent<Outline>();
        portraitOutline.effectColor = new Color(0.95f, 0.55f, 0.18f, 0.5f);
        portraitOutline.effectDistance = new Vector2(1f, -1f);

        RectTransform imageRect = CreateRect("Enemy Portrait", portraitRect);
        Stretch(imageRect, Vector2.zero, Vector2.one,
            new Vector2(9f, 9f), new Vector2(-9f, -9f));
        portraitImage = imageRect.gameObject.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;

        captionText = CreateText("Caption", card, 18, FontStyle.Bold,
            TextAnchor.MiddleLeft, new Color(0.95f, 0.57f, 0.22f, 1f));
        Place(captionText.rectTransform, new Vector2(168f, -22f), new Vector2(302f, 30f));
        titleText = CreateText("Enemy Name", card, 31, FontStyle.Bold,
            TextAnchor.MiddleLeft, new Color(1f, 0.91f, 0.70f, 1f));
        Place(titleText.rectTransform, new Vector2(168f, -57f), new Vector2(302f, 45f));
        summaryText = CreateText("Summary", card, 20, FontStyle.Normal,
            TextAnchor.UpperLeft, new Color(0.91f, 0.86f, 0.75f, 1f));
        summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        summaryText.verticalOverflow = VerticalWrapMode.Truncate;
        Place(summaryText.rectTransform, new Vector2(168f, -105f), new Vector2(302f, 78f));
        statsText = CreateText("Stats", card, 17, FontStyle.Bold,
            TextAnchor.MiddleLeft, new Color(0.74f, 0.82f, 0.58f, 1f));
        Place(statsText.rectTransform, new Vector2(168f, -194f), new Vector2(302f, 27f));

        PreviewInEditor();
    }

    public void PreviewInEditor()
    {
        if (Application.isPlaying || !HasCompleteUI())
        {
            return;
        }

        ConfigureCanvas();
        card.gameObject.SetActive(true);
        cardGroup.alpha = 1f;
        cardGroup.blocksRaycasts = false;
        captionText.text = "ВПЕРВЫЕ НА ЭТОМ УРОВНЕ";
        titleText.text = "Скелет в доспехах";
        summaryText.text = "Атаки сверху наносят ему только половину урона. Ищи боковой или нижний угол.";
        statsText.text = "ЗДОРОВЬЕ  700  ·  ОПЫТ  2";
        accentBar.color = new Color(0.72f, 0.76f, 0.68f, 1f);
        shownPosition = card.anchoredPosition;
    }

    private void ConfigureCanvas()
    {
        encounterCanvas = encounterCanvas != null ? encounterCanvas : GetComponent<Canvas>();
        if (encounterCanvas == null)
        {
            return;
        }

        encounterCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        encounterCanvas.sortingOrder = 260;
        RectTransform root = transform as RectTransform;
        if (root != null)
        {
            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;
        }
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private bool HasCompleteUI()
    {
        return encounterCanvas != null && card != null && cardGroup != null
            && accentBar != null && portraitFrame != null && portraitImage != null
            && captionText != null && titleText != null && summaryText != null
            && statsText != null;
    }

    private void HideImmediately()
    {
        if (cardGroup != null)
        {
            cardGroup.alpha = 0f;
            cardGroup.interactable = false;
            cardGroup.blocksRaycasts = false;
        }
        if (card != null)
        {
            shownPosition = new Vector2(-30f, -145f);
            card.anchoredPosition = shownPosition + Vector2.right * 42f;
        }
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Text CreateText(string name, Transform parent, int size, FontStyle style,
        TextAnchor alignment, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.gameObject.AddComponent<CanvasRenderer>();
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = GameTypography.Font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        GameTypography.Apply(text);
        return text;
    }

    private static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        seenTypes.Clear();
        pendingNotices.Clear();
    }
}
