using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public sealed class FirstLevelTutorial : MonoBehaviour
{
    private enum Step
    {
        Inactive,
        Welcome,
        Queue,
        FireflyAction,
        FireflyFlight,
        Darkness,
        Rooms,
        Experience,
        AwaitingExperience,
        AwaitingHedgehogChoice,
        HedgehogIntro,
        HedgehogAction,
        HedgehogFlight,
        Complete
    }

    [Header("Editable tutorial UI")]
    [SerializeField] private Canvas tutorialCanvas;
    [SerializeField] private Image dimmer;
    [SerializeField] private RectTransform focusFrame;
    [SerializeField] private RectTransform card;
    [SerializeField] private Text progressText;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Image projectileIcon;
    [SerializeField] private Button continueButton;
    [SerializeField] private Text continueLabel;

    private GameManager manager;
    private AnimalProjectileQueue queue;
    private DefenseFrontUI frontUI;
    private Step step;
    private bool running;
    private bool hedgehogTargetsSpawned;
    private bool loadoutMode;

    public bool IsRunning => running;
    public string CurrentStepName => step.ToString();
    public Canvas TutorialCanvas => tutorialCanvas;
    public Button ContinueButton => continueButton;

    private void Awake()
    {
        if (Application.isPlaying && tutorialCanvas != null)
        {
            tutorialCanvas.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            Player.VolleyStarted += OnVolleyStarted;
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            Player.VolleyStarted -= OnVolleyStarted;
        }

        if (queue != null)
        {
            queue.UpgradesChanged -= OnQueueUpgradesChanged;
        }
    }

    private IEnumerator Start()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return null;

        manager = GameManager.Instance;
        queue = AnimalProjectileQueue.Instance;
        frontUI = manager != null && manager.Front != null ? manager.Front.Interface : null;
        bool firstCampaignLevel = manager != null && !manager.IsSurvivalMode
            && !GameRunMode.IsDeveloperRun
            && LevelProgression.Instance != null && LevelProgression.Instance.CurrentLevel == 1;
        if (!firstCampaignLevel || queue == null)
        {
            if (tutorialCanvas != null)
            {
                tutorialCanvas.gameObject.SetActive(false);
            }

            enabled = false;
            yield break;
        }

        loadoutMode = queue.HasRunLoadout || queue.StartingAnimalIndex >= 0;

        if (tutorialCanvas == null)
        {
            BuildEditableUI();
        }

        continueButton.onClick.RemoveListener(AdvanceFromButton);
        continueButton.onClick.AddListener(AdvanceFromButton);

        if (!loadoutMode)
        {
            // Legacy batch-mode tutorial keeps its guided hedgehog unlock.
            queue.SetMaximumAllowedAnimalIndex(AnimalProjectileQueue.HedgehogIndex);
            queue.Unlock(AnimalProjectileQueue.FireflyIndex);
            queue.ForceNextUpgradeOption(AnimalProjectileQueue.HedgehogIndex,
                AnimalProjectileQueue.UpgradeKind.Ammo);
        }
        queue.ReloadAll();
        queue.Select(AnimalProjectileQueue.FireflyIndex);
        queue.UpgradesChanged += OnQueueUpgradesChanged;

        running = true;
        manager.SetTutorialFlowActive(true);
        ShowWelcome();
    }

    private void Update()
    {
        if (!running)
        {
            return;
        }

        if (focusFrame != null && focusFrame.gameObject.activeSelf)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.018f;
            focusFrame.localScale = Vector3.one * pulse;
        }

        if (step == Step.FireflyFlight && !Player.HasActiveProjectiles)
        {
            ShowDarkness();
        }
        else if (step == Step.AwaitingExperience && manager.IsChoosingUpgrade)
        {
            ShowEarnedExperienceChoice();
        }
        else if (step == Step.HedgehogFlight && !Player.HasActiveProjectiles)
        {
            ShowComplete();
        }
    }

    private void OnVolleyStarted(int animalIndex)
    {
        if (!running)
        {
            return;
        }

        if (step == Step.FireflyAction && animalIndex == AnimalProjectileQueue.FireflyIndex)
        {
            step = Step.FireflyFlight;
            ShowAction(Progress(3), "СМОТРИ ЗА СВЕТЛЯЧКАМИ",
                "Дождись конца залпа. Попавший светлячок подсветит врага и оставит на нём горение.",
                AnimalProjectileQueue.FireflyIndex, FocusKind.Field);
        }
        else if (step == Step.HedgehogAction
            && animalIndex == AnimalProjectileQueue.HedgehogIndex)
        {
            step = Step.HedgehogFlight;
            ShowAction("8 / 9", "ИГОЛКИ РАЗЛЕТАЮТСЯ",
                "Дождись конца залпа. При каждом попадании ёж выпускает быстрый круг иголок по соседним врагам.",
                AnimalProjectileQueue.HedgehogIndex, FocusKind.Field);
        }
    }

    private void OnQueueUpgradesChanged()
    {
        if (loadoutMode && running
            && (step == Step.AwaitingExperience || step == Step.AwaitingHedgehogChoice))
        {
            ShowComplete();
            return;
        }
        if (!running || queue == null
            || !queue.IsAvailable(AnimalProjectileQueue.HedgehogIndex))
        {
            return;
        }

        if (step == Step.AwaitingExperience || step == Step.AwaitingHedgehogChoice)
        {
            ShowHedgehogIntro();
        }
    }

    private void AdvanceFromButton()
    {
        if (!running)
        {
            return;
        }

        switch (step)
        {
            case Step.Welcome:
                ShowQueue();
                break;
            case Step.Queue:
                BeginFireflyAction();
                break;
            case Step.Darkness:
                ShowRooms();
                break;
            case Step.Rooms:
                ShowExperience();
                break;
            case Step.Experience:
                BeginExperienceHunt();
                break;
            case Step.HedgehogIntro:
                BeginHedgehogAction();
                break;
            case Step.Complete:
                FinishTutorial();
                break;
        }
    }

    private void ShowWelcome()
    {
        step = Step.Welcome;
        ShowModal(Progress(1), "ПОСЛЕДНИЙ РУБЕЖ",
            "Монстры движутся к нижней границе комнаты. В главной комнате их сдерживает баррикада со 100 HP. Прорвавшийся враг наносит ей свой урон и возвращается в одну из трёх верхних строк. Поражение наступает, когда баррикада разрушена.\n\nСначала разберём твой ход.",
            -1, "ДАЛЬШЕ", FocusKind.None);
    }

    private void ShowQueue()
    {
        step = Step.Queue;
        ShowModal(Progress(2), "ОЧЕРЕДЬ СНАРЯДОВ",
            "На старте: Светлячок — " + AnimalProjectileQueue.StartingAmmoForAnimal(AnimalProjectileQueue.FireflyIndex)
                + ", Ёж — " + AnimalProjectileQueue.StartingAmmoForAnimal(AnimalProjectileQueue.HedgehogIndex)
                + ". У Светлячка всегда пять снарядов, а у Ежа навык «Количество» полностью прокачан. Внизу слева находится колесо снарядов: число возле зверька показывает оставшиеся патроны.\n\nВыбери зверька и выпусти его залп. Когда закончатся все патроны, враги сделают ход.",
            AnimalProjectileQueue.FireflyIndex, "ПОПРОБОВАТЬ", FocusKind.Wheel);
    }

    private void BeginFireflyAction()
    {
        step = Step.FireflyAction;
        queue.Select(AnimalProjectileQueue.FireflyIndex);
        ShowAction(Progress(3), "СВЕТЛЯЧОК · ТВОЙ ХОД",
            "Проведи от тыквы в сторону поля и отпусти. Выпусти залп светлячков по врагам.",
            AnimalProjectileQueue.FireflyIndex, FocusKind.Field);
        SetPaused(false);
    }

    private void ShowDarkness()
    {
        step = Step.Darkness;
        ShowModal(Progress(4), "ТЕМНОТА И ОГОНЬ",
            "Чем дальше линия, тем гуще тьма. Скрытого врага не видит прицел, но случайное попадание всё равно нанесёт урон.\n\nСветлячок раскрывает врага и область радиусом в одну клетку. Каждое попадание добавляет стак горения — максимум три. Огонь срабатывает после хода врагов.",
            AnimalProjectileQueue.FireflyIndex, "ПОКАЗАТЬ КАРТУ", FocusKind.Darkness);
    }

    private void ShowRooms()
    {
        step = Step.Rooms;
        if (frontUI == null && manager.Front != null)
        {
            frontUI = manager.Front.Interface;
        }

        frontUI?.OpenMap(true);
        ShowModal(Progress(5), "КОМНАТЫ И ЛИНИИ",
            "После зачистки линии открываются связанные комнаты следующей: 1, затем 3, 5 и 7. На карте видно, куда ведёт каждая ветка.\n\nЕсли комнату прорывают, враги сразу переходят во внутреннюю связанную комнату. Освобождённые комнаты приносят золото каждый ход.",
            -1, "ДАЛЬШЕ: ОПЫТ", FocusKind.Map);
    }

    private void ShowExperience()
    {
        step = Step.Experience;
        frontUI?.CloseMap();
        int required = manager != null
            ? manager.ExperienceForNextUpgrade
            : ExperienceEconomy.DefaultFirstUpgradeExperience;
        ShowModal(Progress(6), loadoutMode ? "ОПЫТ И УЛУЧШЕНИЯ" : "ОПЫТ И НОВЫЕ СНАРЯДЫ",
            "За побеждённых врагов начисляется опыт — у разных противников награда может отличаться. Зачищенная комната тоже даёт опыт, причём дальние линии ценнее.\n\nШкала находится наверху. Для первого уровня нужно "
            + required + " опыта, а каждое следующее улучшение стоит дороже. Окно выбора откроется после исчезновения выпущенных снарядов, перед ходом врагов.",
            -1, "ЗАРАБОТАТЬ ОПЫТ", FocusKind.Experience);
    }

    private void BeginExperienceHunt()
    {
        step = Step.AwaitingExperience;
        if (tutorialCanvas != null)
        {
            tutorialCanvas.gameObject.SetActive(false);
        }

        manager.SetTutorialFlowActive(false);
        SetPaused(false);
        Player.BlockInputUntilReleased();
        manager.TryCompletePlayerTurn();
    }

    private void ShowEarnedExperienceChoice()
    {
        step = Step.AwaitingHedgehogChoice;
        ShowAction(Progress(7), "НОВЫЙ УРОВЕНЬ",
            loadoutMode
                ? "Шкала опыта заполнена. Усиль Светлячка или Ежа, либо открой другой снаряд из выбранного набора."
                : "Шкала опыта заполнена. В первом выборе доступен только ёж — нажми на его карточку, чтобы открыть новый тип снаряда.",
            loadoutMode ? -1 : AnimalProjectileQueue.HedgehogIndex, FocusKind.Experience);
    }

    private void ShowHedgehogIntro()
    {
        step = Step.HedgehogIntro;
        frontUI?.CloseMap();
        manager.SetTutorialFlowActive(true);
        SpawnHedgehogTargets();
        queue.Select(AnimalProjectileQueue.HedgehogIndex);
        ShowModal("8 / 9", "ЁЖ · ВЗРЫВ ИГОЛОК",
            "Ёж летит и рикошетит как обычный снаряд. При каждом столкновении с врагом он не исчезает, а быстро выпускает иголки по площади.\n\nПрямое попадание наносит основной урон, а иголки задевают врагов вокруг.",
            AnimalProjectileQueue.HedgehogIndex, "ИСПЫТАТЬ ЕЖА", FocusKind.Wheel);
    }

    private void SpawnHedgehogTargets()
    {
        if (hedgehogTargetsSpawned || manager == null || manager.Front == null)
        {
            return;
        }

        LevelGenerator room = manager.Front.GetGenerator(manager.Front.CurrentRoomId);
        if (room == null || room.IsLost)
        {
            for (int i = 0; i < manager.rooms.Count; i++)
            {
                LevelGenerator candidate = manager.rooms[i];
                if (candidate != null && candidate.IsInitialized && !candidate.IsLost)
                {
                    room = candidate;
                    break;
                }
            }
        }
        if (room == null)
        {
            return;
        }

        int spawned = room.SpawnTutorialCluster(6);
        if (spawned <= 0)
        {
            return;
        }

        hedgehogTargetsSpawned = true;
        manager.Front.FocusRoom(room.RoomId, true);
    }

    private void BeginHedgehogAction()
    {
        step = Step.HedgehogAction;
        queue.Select(AnimalProjectileQueue.HedgehogIndex);
        Player.AllowTutorialVolley();
        ShowAction("8 / 9", "ПОПРОБУЙ ЕЖА",
            "Прицелься в скопление врагов и выпусти ежа. Обрати внимание на круг разлетающихся иголок.",
            AnimalProjectileQueue.HedgehogIndex, FocusKind.Field);
        SetPaused(false);
    }

    private void ShowComplete()
    {
        step = Step.Complete;
        ShowModal(Progress(loadoutMode ? 8 : 9), "ОБУЧЕНИЕ ЗАВЕРШЕНО",
            loadoutMode
                ? "Светлячки раскрывают тьму и поджигают цели. Ёж уже готов к бою — его иголки задевают соседних врагов. Остальные выбранные снаряды открываются через улучшения.\n\nРасходуй очередь осмысленно, защищай связанные комнаты и не подпускай монстров к концу главной комнаты."
                : "Используй светлячков, чтобы раскрывать тьму и поджигать цели. Еж лучше всего работает против плотной группы.\n\nРасходуй очередь осмысленно, защищай связанные комнаты и не подпускай монстров к концу главной комнаты.",
            -1, "ВЕРНУТЬСЯ В БОЙ", FocusKind.None);
    }

    private string Progress(int current) => current + " / " + (loadoutMode ? 8 : 9);

    private void FinishTutorial()
    {
        running = false;
        step = Step.Inactive;
        frontUI?.CloseMap();
        queue?.ClearForcedUpgradeOption();
        if (queue != null)
        {
            queue.SetMaximumAllowedAnimalIndex(queue.Animals.Count - 1);
        }

        if (tutorialCanvas != null)
        {
            tutorialCanvas.gameObject.SetActive(false);
        }

        manager.SetTutorialFlowActive(false);
        SetPaused(false);
        Player.BlockInputUntilReleased();
        manager.TryCompletePlayerTurn();
    }

    private enum FocusKind { None, Wheel, Field, Darkness, Map, Experience }

    private void ShowModal(string progress, string title, string body, int iconIndex,
        string buttonText, FocusKind focus)
    {
        if (tutorialCanvas == null)
        {
            return;
        }

        tutorialCanvas.gameObject.SetActive(true);
        dimmer.gameObject.SetActive(true);
        dimmer.raycastTarget = true;
        SetCardLayout(false);
        progressText.text = progress;
        titleText.text = title;
        bodyText.text = body;
        continueLabel.text = buttonText;
        continueButton.gameObject.SetActive(true);
        SetIcon(iconIndex);
        SetFocus(focus);
        SetPaused(true);
        Player.BlockInputUntilReleased();
    }

    private void ShowAction(string progress, string title, string body, int iconIndex, FocusKind focus)
    {
        if (tutorialCanvas == null)
        {
            return;
        }

        tutorialCanvas.gameObject.SetActive(true);
        dimmer.gameObject.SetActive(false);
        SetCardLayout(true);
        progressText.text = progress;
        titleText.text = title;
        bodyText.text = body;
        continueButton.gameObject.SetActive(false);
        SetIcon(iconIndex);
        SetFocus(focus);
        Player.BlockInputUntilReleased();
    }

    private void SetIcon(int index)
    {
        bool show = queue != null && index >= 0 && index < queue.Animals.Count;
        projectileIcon.gameObject.SetActive(show);
        if (!show)
        {
            return;
        }

        projectileIcon.sprite = queue.Animals[index].GetIcon();
        projectileIcon.color = queue.Animals[index].GetIconColor();
        projectileIcon.preserveAspect = true;
    }

    private void SetPaused(bool paused)
    {
        if (manager == null)
        {
            return;
        }

        manager.isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    private void SetCardLayout(bool action)
    {
        if (action)
        {
            Stretch(card, new Vector2(0.07f, 0.77f), new Vector2(0.93f, 0.94f));
        }
        else
        {
            Stretch(card, new Vector2(0.08f, 0.29f), new Vector2(0.92f, 0.70f));
        }
    }

    private void SetFocus(FocusKind kind)
    {
        if (focusFrame == null)
        {
            return;
        }

        focusFrame.localScale = Vector3.one;
        focusFrame.gameObject.SetActive(kind != FocusKind.None && kind != FocusKind.Map);
        switch (kind)
        {
            case FocusKind.Wheel:
                Stretch(focusFrame, new Vector2(0f, 0f), new Vector2(0.34f, 0.31f));
                break;
            case FocusKind.Field:
                Stretch(focusFrame, new Vector2(0.13f, 0.12f), new Vector2(0.87f, 0.77f));
                break;
            case FocusKind.Darkness:
                Stretch(focusFrame, new Vector2(0.14f, 0.43f), new Vector2(0.86f, 0.82f));
                break;
            case FocusKind.Experience:
                Stretch(focusFrame, new Vector2(0.28f, 0.88f), new Vector2(0.72f, 1f));
                break;
        }
    }

    [ContextMenu("Build editable first-level tutorial UI")]
    public void BuildEditableUI()
    {
        if (tutorialCanvas != null)
        {
            return;
        }

        GameObject root = new GameObject("First Level Tutorial UI", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        tutorialCanvas = root.GetComponent<Canvas>();
        tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tutorialCanvas.sortingOrder = 340;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform safe = Rect("Safe Area", root.transform);
        Stretch(safe, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<MenuSafeArea>();

        dimmer = Panel("Modal Dimmer", safe, new Color(0.015f, 0.022f, 0.018f, 0.82f));
        Stretch(dimmer.rectTransform, Vector2.zero, Vector2.one);

        Image focus = Panel("Tutorial Focus", safe, new Color(1f, 0.48f, 0.08f, 0.055f));
        focus.raycastTarget = false;
        Outline focusOutline = focus.gameObject.AddComponent<Outline>();
        focusOutline.effectColor = new Color(1f, 0.58f, 0.12f, 0.95f);
        focusOutline.effectDistance = new Vector2(4f, -4f);
        focusFrame = focus.rectTransform;

        Image cardImage = Panel("Tutorial Card", safe, new Color(0.075f, 0.105f, 0.085f, 0.985f));
        card = cardImage.rectTransform;
        Outline cardOutline = cardImage.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.95f, 0.48f, 0.11f, 0.95f);
        cardOutline.effectDistance = new Vector2(3f, -3f);
        Shadow shadow = cardImage.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(10f, -12f);

        Image topBand = Panel("Pumpkin Header", card, new Color(0.26f, 0.12f, 0.055f, 1f));
        Stretch(topBand.rectTransform, new Vector2(0f, 0.78f), Vector2.one);

        progressText = Label("Progress", card, "1 / 9", 24, TextAnchor.MiddleLeft);
        Stretch(progressText.rectTransform, new Vector2(0.055f, 0.83f), new Vector2(0.27f, 0.97f));
        progressText.color = new Color(1f, 0.72f, 0.23f, 1f);
        titleText = Label("Title", card, "ПОСЛЕДНИЙ РУБЕЖ", 38, TextAnchor.MiddleCenter);
        Stretch(titleText.rectTransform, new Vector2(0.20f, 0.79f), new Vector2(0.95f, 0.98f));

        projectileIcon = Panel("Projectile Icon", card, Color.white);
        Stretch(projectileIcon.rectTransform, new Vector2(0.055f, 0.32f), new Vector2(0.28f, 0.74f));
        projectileIcon.raycastTarget = false;
        projectileIcon.preserveAspect = true;

        bodyText = Label("Body", card, "Текст обучения", 28, TextAnchor.MiddleLeft);
        Stretch(bodyText.rectTransform, new Vector2(0.32f, 0.20f), new Vector2(0.94f, 0.76f));
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Truncate;
        bodyText.resizeTextForBestFit = true;
        bodyText.resizeTextMinSize = 19;
        bodyText.resizeTextMaxSize = 28;

        Image buttonImage = Panel("Continue", card, new Color(0.68f, 0.31f, 0.075f, 1f));
        Stretch(buttonImage.rectTransform, new Vector2(0.23f, 0.055f), new Vector2(0.77f, 0.18f));
        continueButton = buttonImage.gameObject.AddComponent<Button>();
        continueButton.targetGraphic = buttonImage;
        continueButton.transition = Selectable.Transition.ColorTint;
        continueLabel = Label("Label", buttonImage.rectTransform, "ДАЛЬШЕ", 28, TextAnchor.MiddleCenter);
        Stretch(continueLabel.rectTransform, Vector2.zero, Vector2.one);

        PreviewInEditor();
    }

    public void PreviewInEditor()
    {
        if (tutorialCanvas == null)
        {
            return;
        }

        tutorialCanvas.gameObject.SetActive(true);
        dimmer.gameObject.SetActive(true);
        SetCardLayout(false);
        progressText.text = "1 / 9";
        titleText.text = "ПОСЛЕДНИЙ РУБЕЖ";
        bodyText.text = "Обязательное обучение первого уровня. В Play Mode текст меняется по реальным шагам игрока.";
        projectileIcon.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(true);
        continueLabel.text = "ДАЛЬШЕ";
        SetFocus(FocusKind.None);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image Panel(string name, Transform parent, Color color)
    {
        RectTransform rect = Rect(name, parent);
        rect.gameObject.AddComponent<CanvasRenderer>();
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text Label(string name, Transform parent, string value, int size, TextAnchor alignment)
    {
        RectTransform rect = Rect(name, parent);
        rect.gameObject.AddComponent<CanvasRenderer>();
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = GameTypography.Font;
        label.text = value;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.alignment = alignment;
        label.color = new Color(0.95f, 0.94f, 0.84f, 1f);
        label.raycastTarget = false;
        GameTypography.Apply(label);
        return label;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
