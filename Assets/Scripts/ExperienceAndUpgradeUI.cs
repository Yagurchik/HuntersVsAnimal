using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExperienceAndUpgradeUI : MonoBehaviour
{
    public const int MaximumOfferedUpgrades = 3;
    private const int CurrentInterfaceVersion = 2;
    private static ExperienceAndUpgradeUI instance;

    [System.Serializable]
    private class UpgradeCard
    {
        public int slot;
        public int animalIndex;
        public AnimalProjectileQueue.UpgradeKind upgradeKind;
        public Button button;
        public Text kindLabel;
        public Text titleLabel;
        public Text descriptionLabel;
        public Text chooseLabel;
        public Image icon;
        public Image frame;
        public Image accent;
        public Image portraitGlow;
        public Image kindPlate;
        public Image choosePlate;
        public List<Image> rankPips = new List<Image>();
    }
    [SerializeField] private List<UpgradeCard> upgradeCards = new List<UpgradeCard>();

    private GameManager gameManager;
    [SerializeField] private Text levelText;
    [SerializeField] private Text experienceText;
    [SerializeField] private Text pendingText;
    [SerializeField] private Image experienceFill;
    [SerializeField] private GameObject upgradeModal;
    [SerializeField] private RectTransform choicesRoot;
    [SerializeField] private Sprite[] animalPortraits = new Sprite[3];
    [SerializeField, HideInInspector] private int upgradeInterfaceVersion;
    private readonly System.Random offerRandom = new System.Random();
    public GameObject UpgradeModal => upgradeModal;
    public RectTransform ChoicesRoot => choicesRoot;

    public static ExperienceAndUpgradeUI Ensure(GameManager manager)
    {
        if (instance == null) instance = FindObjectOfType<ExperienceAndUpgradeUI>(true);
        if (instance != null)
        {
            instance.gameObject.SetActive(true);
            instance.BuildEditableInterface();
            instance.Bind(manager);
            return instance;
        }

        GameObject canvasObject = new GameObject(
            "Experience And Upgrade HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(ExperienceAndUpgradeUI));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 225;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 1028f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        instance = canvasObject.GetComponent<ExperienceAndUpgradeUI>();
        instance.BuildInterface(canvasObject.GetComponent<RectTransform>());
        instance.Bind(manager);
        return instance;
    }

    private void Awake()
    {
        instance = this;
        if (upgradeModal != null) upgradeModal.SetActive(false);
    }

    public void BuildEditableInterface()
    {
        transform.localScale = Vector3.one;
        if (levelText == null) BuildInterface(GetComponent<RectTransform>());
        else if (upgradeModal == null || upgradeInterfaceVersion < CurrentInterfaceVersion)
            RebuildUpgradeModal(animalPortraits);
    }

    public void RebuildUpgradeModal(Sprite[] portraits)
    {
        if (portraits != null && portraits.Length > 0)
        {
            animalPortraits = new Sprite[Mathf.Max(3, portraits.Length)];
            Array.Copy(portraits, animalPortraits, portraits.Length);
        }

        if (upgradeModal != null)
        {
            if (Application.isPlaying) Destroy(upgradeModal);
            else DestroyImmediate(upgradeModal);
        }
        upgradeModal = null;
        choicesRoot = null;
        pendingText = null;
        upgradeCards.Clear();
        BuildUpgradeModal(GetComponent<RectTransform>(), GameTypography.Font);
    }

    public void Preview(AnimalProjectileQueue queue, bool showChoices, int level = 1)
    {
        if (Application.isPlaying) return;
        BuildEditableInterface();
        levelText.text = LevelProgression.LocationNameForLevel(level) + " · уровень "
            + LevelProgression.LocalLevelFor(level) + " / " + LevelProgression.LevelsPerLocation;
        experienceText.text = "Опыт: 2 / 4";
        SetExperienceFill(0.5f);
        pendingText.text = "Усиление действует до конца текущего уровня";
        BuildUpgradeButtons(queue, false);
        upgradeModal.SetActive(showChoices);
    }

    public static void CloseUpgradeChoice()
    {
        if (instance != null && instance.upgradeModal != null)
        {
            instance.upgradeModal.SetActive(false);
        }
    }

    private void Bind(GameManager manager)
    {
        if (gameManager == manager)
        {
            Refresh();
            return;
        }

        if (gameManager != null)
        {
            gameManager.ExperienceChanged -= Refresh;
        }

        gameManager = manager;
        if (gameManager != null)
        {
            gameManager.ExperienceChanged += Refresh;
        }

        if (Application.isPlaying && upgradeModal != null) upgradeModal.SetActive(false);
        BuildUpgradeButtons(AnimalProjectileQueue.Instance, false);
        Refresh();
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.ExperienceChanged -= Refresh;
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    private void BuildInterface(RectTransform canvasRoot)
    {
        Font font = GameTypography.Font;

        RectTransform hudPanel = CreateImage(
            "Level And Experience",
            canvasRoot,
            new Color(0.04f, 0.08f, 0.04f, 0.9f));
        SetRect(hudPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(580f, 92f));
        hudPanel.GetComponent<Image>().raycastTarget = false;

        levelText = CreateText("Level", hudPanel, font, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetStretch(levelText.rectTransform, new Vector2(0f, 0.48f), new Vector2(0.46f, 1f),
            new Vector2(24f, 0f), new Vector2(-5f, -4f));

        experienceText = CreateText("Experience", hudPanel, font, 21, FontStyle.Normal, TextAnchor.MiddleRight);
        SetStretch(experienceText.rectTransform, new Vector2(0.42f, 0.48f), Vector2.one,
            new Vector2(5f, 0f), new Vector2(-24f, -4f));

        RectTransform barBackground = CreateImage(
            "Experience Bar",
            hudPanel,
            new Color(0.14f, 0.18f, 0.12f, 1f));
        SetStretch(barBackground, new Vector2(0f, 0f), new Vector2(1f, 0.36f),
            new Vector2(24f, 14f), new Vector2(-24f, -5f));
        barBackground.GetComponent<Image>().raycastTarget = false;

        RectTransform fillRect = CreateImage(
            "Fill",
            barBackground,
            new Color(0.95f, 0.68f, 0.16f, 1f));
        SetStretch(fillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        experienceFill = fillRect.GetComponent<Image>();
        experienceFill.type = Image.Type.Filled;
        experienceFill.fillMethod = Image.FillMethod.Horizontal;
        experienceFill.raycastTarget = false;

        BuildUpgradeModal(canvasRoot, font);
    }

    private void BuildUpgradeModal(RectTransform canvasRoot, Font font)
    {
        upgradeInterfaceVersion = CurrentInterfaceVersion;
        RectTransform modalRect = CreateImage(
            "Projectile Upgrade Choice",
            canvasRoot,
            new Color(0.015f, 0.025f, 0.018f, 0.9f));
        SetStretch(modalRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        upgradeModal = modalRect.gameObject;
        Canvas modalCanvas = upgradeModal.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 275;
        upgradeModal.AddComponent<GraphicRaycaster>();
        upgradeModal.AddComponent<CanvasGroup>();
        upgradeModal.AddComponent<UpgradeChoiceModalMotion>();

        RectTransform panel = CreateImage(
            "Upgrade Panel",
            modalRect,
            new Color(0.055f, 0.09f, 0.06f, 0.97f));
        SetStretch(panel, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f),
            Vector2.zero, Vector2.zero);
        StyleRounded(panel.GetComponent<Image>());
        AddOutline(panel.gameObject, new Color(0.42f, 0.31f, 0.15f, 0.96f), new Vector2(5f, -5f));

        RectTransform headerBand = CreateImage("Header Band", panel,
            new Color(0.11f, 0.18f, 0.105f, 1f));
        SetStretch(headerBand, new Vector2(0.035f, 0.825f), new Vector2(0.965f, 0.965f),
            Vector2.zero, Vector2.zero);
        StyleRounded(headerBand.GetComponent<Image>());
        headerBand.GetComponent<Image>().raycastTarget = false;

        CreateHeaderOrnament(headerBand);

        Text title = CreateText("Title", headerBand, font, 40, FontStyle.Bold, TextAnchor.MiddleCenter);
        title.text = "ДАР СУМЕРЕЧНОЙ ЧАЩИ";
        title.color = new Color(1f, 0.79f, 0.28f, 1f);
        SetStretch(title.rectTransform, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.94f),
            Vector2.zero, Vector2.zero);
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 23;
        title.resizeTextMaxSize = 40;

        Text subtitle = CreateText("Subtitle", headerBand, font, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
        subtitle.text = "Выбери одно из трёх усилений";
        subtitle.color = new Color(0.78f, 0.84f, 0.68f, 1f);
        SetStretch(subtitle.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.46f),
            Vector2.zero, Vector2.zero);

        GameObject choicesObject = new GameObject("Choices", typeof(RectTransform));
        choicesObject.transform.SetParent(panel, false);
        choicesRoot = choicesObject.GetComponent<RectTransform>();
        SetStretch(choicesRoot, new Vector2(0.045f, 0.135f), new Vector2(0.955f, 0.805f),
            Vector2.zero, Vector2.zero);

        pendingText = CreateText("Pending", panel, font, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
        pendingText.color = new Color(0.68f, 0.78f, 0.61f, 1f);
        SetStretch(pendingText.rectTransform, new Vector2(0.08f, 0.055f), new Vector2(0.92f, 0.125f),
            Vector2.zero, Vector2.zero);

        upgradeModal.SetActive(false);
    }

    public void ShowUpgradeChoice()
    {
        Refresh();
        BuildUpgradeButtons(AnimalProjectileQueue.Instance, true);
        upgradeModal.SetActive(true);
        upgradeModal.transform.SetAsLastSibling();
    }

    private void Refresh()
    {
        if (gameManager == null || levelText == null)
        {
            return;
        }

        if (gameManager.IsSurvivalMode)
        {
            levelText.text = "Уровень игрока " + gameManager.PlayerUpgradeLevel;
        }
        else
        {
            int levelNumber = LevelProgression.Instance != null
                ? LevelProgression.Instance.CurrentLevel
                : 1;
            levelText.text = LevelProgression.LocationNameForLevel(levelNumber) + " · уровень "
                + LevelProgression.LocalLevelFor(levelNumber) + " / " + LevelProgression.LevelsPerLocation;
        }
        experienceText.text = "Опыт: " + gameManager.CurrentExperience + " / "
            + gameManager.ExperienceForNextUpgrade;
        SetExperienceFill((float)gameManager.CurrentExperience /
            Mathf.Max(1, gameManager.ExperienceForNextUpgrade));

        if (pendingText != null)
        {
            pendingText.text = gameManager.PendingProjectileUpgrades > 1
                ? "Доступно улучшений: " + gameManager.PendingProjectileUpgrades
                : "Усиление действует до конца текущего уровня";
        }
    }

    private void SetExperienceFill(float value)
    {
        if (experienceFill == null) return;
        value = Mathf.Clamp01(value);
        experienceFill.fillAmount = value;
        // Image ignores Filled mode without a sprite; plain-color bars use their rect width.
        RectTransform rect = experienceFill.rectTransform;
        float width = experienceFill.sprite != null ? 1f : value;
        rect.anchorMax = new Vector2(Mathf.Lerp(rect.anchorMin.x, 1f, width), rect.anchorMax.y);
    }

    private void BuildUpgradeButtons(AnimalProjectileQueue queue, bool randomize)
    {
        foreach (UpgradeCard card in upgradeCards)
            if (card.button != null) card.button.gameObject.SetActive(false);
        if (queue == null)
        {
            return;
        }

        List<AnimalProjectileQueue.UpgradeOption> options = SelectOffers(
            queue.GetAvailableUpgradeOptions(), randomize, queue);
        int count = options.Count;
        Transform subtitleTransform = upgradeModal != null
            ? upgradeModal.transform.Find("Upgrade Panel/Header Band/Subtitle")
            : null;
        Text subtitle = subtitleTransform != null ? subtitleTransform.GetComponent<Text>() : null;
        if (subtitle != null)
        {
            subtitle.text = "Выберите одно улучшение";
        }
        if (count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            CreateUpgradeButton(i, count, options[i], queue);
        }
    }

    private List<AnimalProjectileQueue.UpgradeOption> SelectOffers(
        List<AnimalProjectileQueue.UpgradeOption> available,
        bool randomize, AnimalProjectileQueue queue)
    {
        var pool = new List<AnimalProjectileQueue.UpgradeOption>(available);
        if (!randomize || !Application.isPlaying)
        {
            // Keep the editable scene preview useful: show one card for every animal.
            // Runtime offers below remain genuinely random.
            var preview = new List<AnimalProjectileQueue.UpgradeOption>();
            for (int animalIndex = 0;
                 animalIndex < animalPortraits.Length && preview.Count < MaximumOfferedUpgrades;
                 animalIndex++)
            {
                AnimalProjectileQueue.UpgradeOption option = pool.Find(candidate =>
                    candidate.AnimalIndex == animalIndex);
                if (option != null) preview.Add(option);
            }

            for (int i = 0; i < pool.Count && preview.Count < MaximumOfferedUpgrades; i++)
                if (!preview.Contains(pool[i])) preview.Add(pool[i]);
            return preview;
        }

        if (randomize && Application.isPlaying)
        {
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int swap = offerRandom.Next(i + 1);
                AnimalProjectileQueue.UpgradeOption value = pool[i];
                pool[i] = pool[swap];
                pool[swap] = value;
            }

            AnimalProjectileQueue.UpgradeOption rabbitUnlock = pool.Find(option =>
                option.AnimalIndex == AnimalProjectileQueue.RabbitIndex
                && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo
                && !queue.IsAvailable(AnimalProjectileQueue.RabbitIndex));
            if (rabbitUnlock != null)
            {
                // Keep the locked Rabbit visible even on narrow screens where
                // the third card can be easy to miss.
                pool.Remove(rabbitUnlock);
                pool.Insert(0, rabbitUnlock);
            }
        }
        if (pool.Count > MaximumOfferedUpgrades)
            pool.RemoveRange(MaximumOfferedUpgrades, pool.Count - MaximumOfferedUpgrades);
        return pool;
    }

    private void CreateUpgradeButton(
        int slot,
        int visibleCount,
        AnimalProjectileQueue.UpgradeOption option,
        AnimalProjectileQueue queue)
    {
        UpgradeCard existing = upgradeCards.Find(card => card.slot == slot && card.button != null);
        if (existing != null)
        {
            LayoutCard(existing.button.transform as RectTransform, slot, visibleCount);
            RefreshCard(existing, option, queue);
            return;
        }
        Font font = GameTypography.Font;
        RectTransform buttonRect = CreateImage(
            "Upgrade Card " + (slot + 1),
            choicesRoot,
            new Color(0.2f, 0.27f, 0.16f, 1f));
        LayoutCard(buttonRect, slot, visibleCount);
        Image frame = buttonRect.GetComponent<Image>();
        StyleRounded(frame);
        AddOutline(buttonRect.gameObject, new Color(0.04f, 0.055f, 0.035f, 1f), new Vector2(6f, -7f));
        Shadow shadow = buttonRect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
        shadow.effectDistance = new Vector2(12f, -14f);

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.09f;
        button.colors = colors;

        UpgradeChoiceCardMotion motion = buttonRect.gameObject.AddComponent<UpgradeChoiceCardMotion>();
        motion.Configure(slot);

        RectTransform content = CreateImage("Card Surface", buttonRect,
            new Color(0.075f, 0.115f, 0.075f, 1f));
        SetStretch(content, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        StyleRounded(content.GetComponent<Image>());
        content.GetComponent<Image>().raycastTarget = false;

        RectTransform accentRect = CreateImage("Animal Accent", content, Color.white);
        SetStretch(accentRect, new Vector2(0.04f, 0.965f), new Vector2(0.96f, 0.995f),
            Vector2.zero, Vector2.zero);
        StyleRounded(accentRect.GetComponent<Image>());
        Image accent = accentRect.GetComponent<Image>();
        accent.raycastTarget = false;

        RectTransform kindRect = CreateImage("Upgrade Kind", content, Color.white);
        SetStretch(kindRect, new Vector2(0.17f, 0.89f), new Vector2(0.83f, 0.965f),
            Vector2.zero, Vector2.zero);
        StyleRounded(kindRect.GetComponent<Image>());
        Image kindPlate = kindRect.GetComponent<Image>();
        kindPlate.raycastTarget = false;
        Text kindLabel = CreateText("Kind", kindRect, font, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetStretch(kindLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
        kindLabel.resizeTextForBestFit = true;
        kindLabel.resizeTextMinSize = 11;
        kindLabel.resizeTextMaxSize = 18;

        RectTransform glowRect = CreateImage("Portrait Glow", content,
            new Color(0.8f, 0.65f, 0.25f, 0.16f));
        SetStretch(glowRect, new Vector2(0.08f, 0.475f), new Vector2(0.92f, 0.88f),
            Vector2.zero, Vector2.zero);
        StyleRounded(glowRect.GetComponent<Image>());
        Image portraitGlow = glowRect.GetComponent<Image>();
        portraitGlow.raycastTarget = false;

        RectTransform iconRect = CreateImage("Animal Portrait", content, Color.white);
        SetStretch(iconRect, new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.89f),
            new Vector2(8f, 4f), new Vector2(-8f, -4f));
        Image icon = iconRect.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        Text titleLabel = CreateText("Upgrade Title", content, font, 27,
            FontStyle.Bold, TextAnchor.MiddleCenter);
        SetStretch(titleLabel.rectTransform, new Vector2(0.07f, 0.355f), new Vector2(0.93f, 0.47f),
            Vector2.zero, Vector2.zero);
        titleLabel.resizeTextForBestFit = true;
        titleLabel.resizeTextMinSize = 16;
        titleLabel.resizeTextMaxSize = 27;

        Text descriptionLabel = CreateText("Upgrade Description", content, font, 19,
            FontStyle.Normal, TextAnchor.MiddleCenter);
        SetStretch(descriptionLabel.rectTransform, new Vector2(0.08f, 0.19f), new Vector2(0.92f, 0.355f),
            Vector2.zero, Vector2.zero);
        descriptionLabel.resizeTextForBestFit = true;
        descriptionLabel.resizeTextMinSize = 12;
        descriptionLabel.resizeTextMaxSize = 19;
        descriptionLabel.color = new Color(0.86f, 0.86f, 0.75f, 1f);

        RectTransform ranks = new GameObject("Upgrade Ranks", typeof(RectTransform)).GetComponent<RectTransform>();
        ranks.SetParent(content, false);
        SetStretch(ranks, new Vector2(0.2f, 0.145f), new Vector2(0.8f, 0.19f),
            Vector2.zero, Vector2.zero);
        var rankPips = new List<Image>();
        for (int i = 0; i < 3; i++)
        {
            RectTransform pipRect = CreateImage("Rank " + (i + 1), ranks,
                new Color(0.19f, 0.24f, 0.17f, 1f));
            float center = (i + 0.5f) / 3f;
            SetRect(pipRect, new Vector2(center, 0.5f), new Vector2(center, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 10f));
            StyleRounded(pipRect.GetComponent<Image>());
            pipRect.GetComponent<Image>().raycastTarget = false;
            rankPips.Add(pipRect.GetComponent<Image>());
        }

        RectTransform chooseRect = CreateImage("Choose Plate", content, Color.white);
        SetStretch(chooseRect, new Vector2(0.12f, 0.035f), new Vector2(0.88f, 0.125f),
            Vector2.zero, Vector2.zero);
        StyleRounded(chooseRect.GetComponent<Image>());
        Image choosePlate = chooseRect.GetComponent<Image>();
        choosePlate.raycastTarget = false;
        Text chooseLabel = CreateText("Choose", chooseRect, font, 21, FontStyle.Bold, TextAnchor.MiddleCenter);
        chooseLabel.text = "ВЫБРАТЬ";
        chooseLabel.color = new Color(0.09f, 0.075f, 0.035f, 1f);
        SetStretch(chooseLabel.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(8f, 0f), new Vector2(-8f, 0f));

        CreateCardStitches(content);

        var card = new UpgradeCard {
            slot = slot,
            animalIndex = option.AnimalIndex,
            upgradeKind = option.Kind,
            button = button,
            kindLabel = kindLabel,
            titleLabel = titleLabel,
            descriptionLabel = descriptionLabel,
            chooseLabel = chooseLabel,
            icon = icon,
            frame = frame,
            accent = accent,
            portraitGlow = portraitGlow,
            kindPlate = kindPlate,
            choosePlate = choosePlate,
            rankPips = rankPips
        };
        upgradeCards.Add(card);
        RefreshCard(card, option, queue);
    }

    private void RefreshCard(UpgradeCard card, AnimalProjectileQueue.UpgradeOption option,
        AnimalProjectileQueue queue)
    {
        card.button.gameObject.SetActive(true);
        card.animalIndex = option.AnimalIndex;
        card.upgradeKind = option.Kind;
        card.button.gameObject.name = "Upgrade Card " + (card.slot + 1) + " - " + option.Title;
        // Rebind runtime callbacks; keep the authored child objects and their styling.
        card.button.onClick.RemoveAllListeners();
        int index = option.AnimalIndex;
        AnimalProjectileQueue.UpgradeKind kind = option.Kind;
        card.button.onClick.AddListener(() => {
            if (Application.isPlaying && gameManager != null)
                gameManager.SelectProjectileUpgrade(index, kind);
        });
        if (card.icon != null)
        {
            card.icon.sprite = GetPortrait(option);
            card.icon.color = Color.white;
        }
        Color accent = GetAccent(option);
        bool isUltimate = option.Kind == AnimalProjectileQueue.UpgradeKind.HoneyBadgerUltimate
            || option.Kind == AnimalProjectileQueue.UpgradeKind.HedgehogUltimate
            || option.Kind == AnimalProjectileQueue.UpgradeKind.BoarUltimate
            || option.Kind == AnimalProjectileQueue.UpgradeKind.FireflyUltimate
            || option.Kind == AnimalProjectileQueue.UpgradeKind.RabbitUltimate
            || option.Kind == AnimalProjectileQueue.UpgradeKind.BeaverUltimate
            || option.Kind == AnimalProjectileQueue.UpgradeKind.FrogUltimate
            || option.Kind == AnimalProjectileQueue.UpgradeKind.SquirrelUltimate;
        Color frameColor = Color.Lerp(new Color(0.08f, 0.13f, 0.075f), accent,
            isUltimate ? 0.68f : 0.34f);
        Color glowColor = new Color(accent.r, accent.g, accent.b, isUltimate ? 0.48f : 0.18f);
        if (card.frame != null) card.frame.color = frameColor;
        if (card.accent != null) card.accent.color = accent;
        if (card.portraitGlow != null) card.portraitGlow.color = glowColor;
        if (card.kindPlate != null) card.kindPlate.color = Color.Lerp(new Color(0.12f, 0.16f, 0.1f), accent, 0.55f);
        if (card.choosePlate != null) card.choosePlate.color = accent;
        if (card.chooseLabel != null)
        {
            card.chooseLabel.text = isUltimate ? "ПРИНЯТЬ УЛЬТУ" : "ВЫБРАТЬ";
            card.chooseLabel.color = isUltimate
                ? new Color(0.18f, 0.045f, 0.01f, 1f)
                : new Color(0.09f, 0.075f, 0.035f, 1f);
        }
        if (card.kindLabel != null) card.kindLabel.text = GetKindLabel(option.Kind);
        if (card.titleLabel != null) card.titleLabel.text = CleanTitle(option.Title);
        if (card.descriptionLabel != null)
        {
            card.descriptionLabel.text = option.Description.Replace(" -> ", "  →  ");
        }
        int rank = GetCurrentRank(queue, option);
        for (int i = 0; i < card.rankPips.Count; i++)
        {
            if (card.rankPips[i] == null) continue;
            card.rankPips[i].color = i < rank ? accent
                : i == rank ? new Color(accent.r, accent.g, accent.b, 0.42f)
                : new Color(0.17f, 0.21f, 0.15f, 1f);
        }

        UltimateUpgradeCardGlow ultimateGlow =
            card.button.GetComponent<UltimateUpgradeCardGlow>();
        if (ultimateGlow == null)
            ultimateGlow = card.button.gameObject.AddComponent<UltimateUpgradeCardGlow>();
        ultimateGlow.Configure(isUltimate, card.frame, card.portraitGlow,
            frameColor, glowColor, accent);
    }

    private static void LayoutCard(RectTransform rect, int slot, int visibleCount)
    {
        visibleCount = Mathf.Clamp(visibleCount, 1, MaximumOfferedUpgrades);
        float totalWidth = visibleCount == 1 ? 0.38f : visibleCount == 2 ? 0.7f : 1f;
        const float gap = 0.018f;
        float width = (totalWidth - gap * (visibleCount - 1)) / visibleCount;
        float left = 0.5f - totalWidth * 0.5f + slot * (width + gap);
        SetStretch(rect, new Vector2(left, 0f), new Vector2(left + width, 1f),
            new Vector2(5f, 5f), new Vector2(-5f, -5f));
    }

    private Sprite GetPortrait(AnimalProjectileQueue.UpgradeOption option)
    {
        if (option.AnimalIndex >= 0 && animalPortraits != null
            && option.AnimalIndex < animalPortraits.Length
            && animalPortraits[option.AnimalIndex] != null)
            return animalPortraits[option.AnimalIndex];
        return option.Icon;
    }

    private static Color GetAccent(AnimalProjectileQueue.UpgradeOption option)
    {
        if (option.Kind == AnimalProjectileQueue.UpgradeKind.HoneyBadgerUltimate)
            return new Color(1f, 0.46f, 0.06f, 1f);
        if (option.Kind == AnimalProjectileQueue.UpgradeKind.HedgehogUltimate)
            return new Color(1f, 0.68f, 0.12f, 1f);
        if (option.Kind == AnimalProjectileQueue.UpgradeKind.BoarUltimate)
            return new Color(1f, 0.3f, 0.045f, 1f);
        if (option.Kind == AnimalProjectileQueue.UpgradeKind.FireflyUltimate)
            return new Color(1f, 0.58f, 0.035f, 1f);
        if (option.Kind == AnimalProjectileQueue.UpgradeKind.RabbitUltimate)
            return new Color(1f, 0.48f, 0.62f, 1f);
        if (option.Kind == AnimalProjectileQueue.UpgradeKind.SquirrelUltimate)
            return new Color(1f, 0.5f, 0.12f, 1f);
        switch (option.AnimalIndex)
        {
            case 0: return new Color(1f, 0.72f, 0.16f, 1f);
            case 1: return new Color(0.78f, 0.48f, 0.22f, 1f);
            case 2: return new Color(0.88f, 0.55f, 0.5f, 1f);
            case 3: return new Color(0.83f, 0.58f, 0.28f, 1f);
            case 4: return new Color(1f, 0.69f, 0.12f, 1f);
            default: return new Color(0.75f, 0.8f, 0.45f, 1f);
        }
    }

    private static string GetKindLabel(AnimalProjectileQueue.UpgradeKind kind)
    {
        switch (kind)
        {
            case AnimalProjectileQueue.UpgradeKind.Ammo: return "ЗАПАС СНАРЯДОВ";
            case AnimalProjectileQueue.UpgradeKind.RepeatFire: return "ПОВТОРНЫЙ ЗАЛП";
            case AnimalProjectileQueue.UpgradeKind.Burn: return "ГОРЕНИЕ";
            case AnimalProjectileQueue.UpgradeKind.FireflyUltimate: return "✦ УЛЬТИМАТИВНАЯ СПОСОБНОСТЬ ✦";
            case AnimalProjectileQueue.UpgradeKind.RabbitPackStrength: return "СИЛА СТАИ";
            case AnimalProjectileQueue.UpgradeKind.RabbitBreeding: return "БЫСТРОЕ РАЗМНОЖЕНИЕ";
            case AnimalProjectileQueue.UpgradeKind.RabbitFirstBreeding: return "РАННИЙ ПРИПЛОД";
            case AnimalProjectileQueue.UpgradeKind.RabbitUltimate: return "✦ УЛЬТИМАТИВНАЯ СПОСОБНОСТЬ ✦";
            case AnimalProjectileQueue.UpgradeKind.HedgehogRadius: return "РАДИУС ВЗРЫВА";
            case AnimalProjectileQueue.UpgradeKind.HedgehogQuillDamage: return "ОСТРЫЕ ИГОЛКИ";
            case AnimalProjectileQueue.UpgradeKind.HedgehogUltimate: return "✦ УЛЬТИМАТИВНАЯ СПОСОБНОСТЬ ✦";
            case AnimalProjectileQueue.UpgradeKind.HoneyBadgerRadius: return "РАДИУС ОХОТЫ";
            case AnimalProjectileQueue.UpgradeKind.HoneyBadgerChain: return "ЯРОСТНАЯ ЦЕПЬ";
            case AnimalProjectileQueue.UpgradeKind.HoneyBadgerUltimate: return "✦ УЛЬТИМАТИВНАЯ СПОСОБНОСТЬ ✦";
            case AnimalProjectileQueue.UpgradeKind.BoarBounces: return "БОЛЬШЕ ОТСКОКОВ";
            case AnimalProjectileQueue.UpgradeKind.BoarFirstHit: return "ПЕРВЫЙ ТАРАН";
            case AnimalProjectileQueue.UpgradeKind.BoarUltimate: return "✦ УЛЬТИМАТИВНАЯ СПОСОБНОСТЬ ✦";
            case AnimalProjectileQueue.UpgradeKind.SquirrelDetonation: return "РАННИЙ ВЗРЫВ";
            case AnimalProjectileQueue.UpgradeKind.SquirrelBinding: return "ЛЕСНЫЕ ПУТЫ";
            case AnimalProjectileQueue.UpgradeKind.BeaverUltimate:
            case AnimalProjectileQueue.UpgradeKind.FrogUltimate:
            case AnimalProjectileQueue.UpgradeKind.SquirrelUltimate: return "✦ УЛЬТИМАТИВНАЯ СПОСОБНОСТЬ ✦";
            default: return "СИЛА СТАИ";
        }
    }

    private static string CleanTitle(string title)
    {
        return string.IsNullOrEmpty(title) ? "УСИЛЕНИЕ" : title.Replace(": ", "\n").ToUpperInvariant();
    }

    private static int GetCurrentRank(AnimalProjectileQueue queue,
        AnimalProjectileQueue.UpgradeOption option)
    {
        if (queue == null || option.AnimalIndex < 0) return 0;
        switch (option.Kind)
        {
            case AnimalProjectileQueue.UpgradeKind.Ammo:
                return queue.GetAmmoUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.RepeatFire:
                return queue.GetRepeatFireUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.Burn:
                return queue.GetBurnUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.FireflyUltimate:
                return queue.HasFireflyUltimate(option.AnimalIndex) ? 3 : 0;
            case AnimalProjectileQueue.UpgradeKind.RabbitPackStrength:
                return queue.GetRabbitPackStrengthUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.RabbitBreeding:
                return queue.GetRabbitBreedingUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.RabbitFirstBreeding:
                return queue.GetRabbitFirstBreedingUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.RabbitUltimate:
                return queue.HasRabbitUltimate(option.AnimalIndex) ? 3 : 0;
            case AnimalProjectileQueue.UpgradeKind.HedgehogRadius:
                return queue.GetHedgehogRadiusUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.HedgehogQuillDamage:
                return queue.GetHedgehogQuillDamageUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.HedgehogUltimate:
                return queue.HasHedgehogUltimate(option.AnimalIndex) ? 3 : 0;
            case AnimalProjectileQueue.UpgradeKind.HoneyBadgerRadius:
                return queue.GetHoneyBadgerRadiusUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.HoneyBadgerChain:
                return queue.GetHoneyBadgerChainUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.HoneyBadgerUltimate:
                return queue.HasHoneyBadgerUltimate(option.AnimalIndex) ? 3 : 0;
            case AnimalProjectileQueue.UpgradeKind.BoarBounces:
                return queue.GetBoarBouncesUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.BoarFirstHit:
                return queue.GetBoarFirstHitUpgradeLevel(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.BoarUltimate:
                return queue.HasBoarUltimate(option.AnimalIndex) ? 3 : 0;
            case AnimalProjectileQueue.UpgradeKind.BeaverUltimate:
                return queue.HasBeaverUltimate(option.AnimalIndex) ? 3 : 0;
            case AnimalProjectileQueue.UpgradeKind.FrogUltimate:
                return queue.HasFrogUltimate(option.AnimalIndex) ? 3 : 0;
            case AnimalProjectileQueue.UpgradeKind.SquirrelDetonation:
                return queue.Animals[option.AnimalIndex].SquirrelDetonationUpgradeLevel;
            case AnimalProjectileQueue.UpgradeKind.SquirrelBinding:
                return queue.GetSquirrelBindingRank(option.AnimalIndex);
            case AnimalProjectileQueue.UpgradeKind.SquirrelUltimate:
                return queue.HasSquirrelUltimate(option.AnimalIndex) ? 3 : 0;
            default:
                return 0;
        }
    }

    private static void CreateCardStitches(RectTransform parent)
    {
        for (int i = 0; i < 4; i++)
        {
            RectTransform stitch = CreateImage("Corner Stitch " + (i + 1), parent,
                new Color(0.53f, 0.42f, 0.25f, 0.8f));
            float x = i % 2 == 0 ? 0.045f : 0.955f;
            float y = i < 2 ? 0.045f : 0.955f;
            SetRect(stitch, new Vector2(x, y), new Vector2(x, y), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(18f, 5f));
            stitch.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 42f : -42f);
            stitch.GetComponent<Image>().raycastTarget = false;
        }
    }

    private static void CreateHeaderOrnament(RectTransform parent)
    {
        for (int side = 0; side < 2; side++)
        {
            RectTransform line = CreateImage(side == 0 ? "Ornament Left" : "Ornament Right",
                parent, new Color(0.58f, 0.42f, 0.18f, 0.9f));
            if (side == 0)
                SetStretch(line, new Vector2(0.04f, 0.49f), new Vector2(0.25f, 0.51f), Vector2.zero, Vector2.zero);
            else
                SetStretch(line, new Vector2(0.75f, 0.49f), new Vector2(0.96f, 0.51f), Vector2.zero, Vector2.zero);
            line.GetComponent<Image>().raycastTarget = false;
        }
        RectTransform diamond = CreateImage("Ornament Seed", parent,
            new Color(0.98f, 0.7f, 0.2f, 1f));
        SetRect(diamond, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(18f, 18f));
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        diamond.GetComponent<Image>().raycastTarget = false;
    }

    private static void StyleRounded(Image image)
    {
        if (image == null) return;
        image.sprite = null;
        image.type = Image.Type.Simple;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static RectTransform CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return imageObject.GetComponent<RectTransform>();
    }

    private static Text CreateText(
        string name,
        RectTransform parent,
        Font font,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        GameTypography.Apply(text);
        return text;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void SetStretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
