using UnityEngine;
using UnityEngine.UI;

public class DefenseFrontUI : MonoBehaviour
{
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private Text toolbarText;
    [SerializeField] private Text statusText;
    [SerializeField] private Button mapButton;
    [SerializeField] private Button defendRoomButton;
    [SerializeField] private Image defendRoomBackground;
    [SerializeField] private Text defendRoomLabel;
    [SerializeField] private Button waitButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button[] roomButtons;
    [SerializeField] private Text[] roomLabels;
    [SerializeField] private Image[] roomBackgrounds;
    [SerializeField] private Image[] selectedBorders;
    [SerializeField] private Image[] breachFills;
    [SerializeField] private Image[] breachHalos;
    [SerializeField] private Image[] connections;

    [Header("Breach danger visuals")]
    [SerializeField] private Color breachFillColor = new Color(0.47f, 0.045f, 0.13f, 0.96f);
    [SerializeField] private Color breachHaloColor = new Color(1f, 0.20f, 0.31f, 1f);

    [Header("Room light meter")]
    [SerializeField] private Image[] lightMeterLit;
    [SerializeField] private Image[] lightMeterDark;
    [SerializeField, Tooltip("Lit share of the room.")]
    private Color litMeterColor = new Color(1f, 0.86f, 0.18f, 1f);
    [SerializeField, Tooltip("Darkened share of the room.")]
    private Color darkMeterColor = new Color(0.56f, 0.24f, 0.04f, 1f);
    private const float LightMeterWidth = 0.12f;
    private const string LegendText = "Золотая рамка — вы здесь · Зелёная — доход\n"
        + "Бордовое заполнение — враг ближе к прорыву\n"
        + "Мигающая рамка — прорыв через ход\n"
        + "Полоса справа: жёлтая — освещено, оранжевая — темнота";

    private DefenseLineRuntime front;
    private GameManager manager;
    private bool dirty = true;
    private bool lastCanSwitch;
    private bool lastEnded;
    public bool IsMapOpen => mapPanel != null && mapPanel.activeSelf;
    public Button DefendRoomButton => defendRoomButton;

    public static DefenseFrontUI Ensure(DefenseLineRuntime runtime, GameManager game)
    {
        DefenseFrontUI ui = FindObjectOfType<DefenseFrontUI>(true);
        if (ui == null)
        {
            GameObject root = new GameObject("Defense Front HUD", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DefenseFrontUI));
            ui = root.GetComponent<DefenseFrontUI>();
        }
        if (ui.mapPanel == null)
        {
            ui.BuildInterface();
        }

        ui.GetComponent<Canvas>().sortingOrder = 240;
        ui.Bind(runtime, game);
        return ui;
    }

    private void Bind(DefenseLineRuntime runtime, GameManager game)
    {
        EnsureThreatVisuals();
        if (game != null && game.Clock != null && mapPanel != null)
        {
            Text title = mapPanel.transform.Find("Title/Label")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = "ВЫЖИВАНИЕ · ЧАСЫ";
            }
        }
        if (front != null)
        {
            front.Changed -= MarkDirty;
        }

        front = runtime;
        manager = game;
        front.Changed += MarkDirty;
        mapButton.onClick.RemoveAllListeners();
        mapButton.onClick.AddListener(ToggleMap);
        if (defendRoomButton == null)
        {
            defendRoomButton = transform.Find("Safe Area/Defend Room Button")?.GetComponent<Button>();
        }

        if (defendRoomButton != null)
        {
            if (defendRoomBackground == null)
            {
                defendRoomBackground = defendRoomButton.GetComponent<Image>();
            }

            if (defendRoomLabel == null)
            {
                defendRoomLabel = defendRoomButton.transform.Find("Label")?.GetComponent<Text>();
            }

            defendRoomButton.onClick.RemoveAllListeners();
            defendRoomButton.onClick.AddListener(ToggleDefendRoom);
        }
        if (waitButton != null)
        {
            waitButton.onClick.RemoveAllListeners();
            waitButton.gameObject.SetActive(false);
        }
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(CloseMap);
        for (int i = 0; i < roomButtons.Length; i++)
        {
            int captured = i;
            roomButtons[i].onClick.RemoveAllListeners();
            roomButtons[i].onClick.AddListener(() => {
                if (front.FocusRoom(captured))
                {
                    CloseMap();
                }
            });
        }
        CloseMap();
        Refresh();
    }

    private void MarkDirty() => dirty = true;

    public void ConfigureDefendRoomButton(Button button, Image background, Text label)
    {
        defendRoomButton = button;
        defendRoomBackground = background;
        defendRoomLabel = label;
    }

    private void ToggleDefendRoom()
    {
        if (front == null)
        {
            return;
        }

        front.ToggleDefendCurrentRoom();
        Refresh();
    }
    private void LateUpdate()
    {
        if (manager == null || front == null)
        {
            return;
        }

        bool canSwitch = manager.CanSwitchRooms;
        if (dirty || lastCanSwitch != canSwitch || lastEnded != manager.GameEnded)
        {
            Refresh();
        }

        if (IsMapOpen)
        {
            PulseDangerHalos();
        }

        if (IsMapOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMap();
        }
    }

    public void ToggleMap()
    {
        if (IsMapOpen) { CloseMap(); return; }
        OpenMap(false);
    }

    public bool OpenMap(bool force = false)
    {
        if (IsMapOpen)
        {
            return true;
        }

        if (manager == null || (!force && !manager.CanSwitchRooms))
        {
            return false;
        }

        Player.BlockInputUntilReleased();
        mapPanel.SetActive(true);
        Refresh();
        return true;
    }

    public void CloseMap()
    {
        if (mapPanel != null)
        {
            mapPanel.SetActive(false);
        }

        dirty = true;
        if (Application.isPlaying)
        {
            Player.BlockInputUntilReleased();
        }
    }

    private void Refresh()
    {
        dirty = false;
        lastCanSwitch = manager.CanSwitchRooms;
        lastEnded = manager.GameEnded;
        mapButton.gameObject.SetActive(!manager.GameEnded && !IsMapOpen);
        mapButton.interactable = lastCanSwitch;
        if (defendRoomButton != null)
        {
            defendRoomButton.gameObject.SetActive(!manager.GameEnded && !IsMapOpen);
            defendRoomButton.interactable = lastCanSwitch
                && front.CanSelect(front.CurrentRoomId);
            bool defending = front.IsDefendingRoom;
            if (defendRoomBackground != null)
            {
                defendRoomBackground.color = defending
                    ? new Color(0.51f, 0.32f, 0.13f, 1f)
                    : new Color(0.15f, 0.30f, 0.22f, 1f);
            }

            if (defendRoomLabel != null)
            {
                defendRoomLabel.text = defending ? "СНЯТЬ ЗАЩИТУ" : "ЗАЩИЩАТЬ КОМНАТУ";
            }
        }
        if (waitButton != null)
        {
            waitButton.gameObject.SetActive(false);
        }

        DefenseFront.Room selected = front.Model.GetRoom(front.CurrentRoomId);
        toolbarText.text = "КАРТА  ·  " + (selected != null ? selected.Label : "—")
            + "     БАРРИКАДА " + Mathf.CeilToInt(front.BarricadeHealth)
            + "/" + Mathf.CeilToInt(front.BarricadeMaxHealth)
            + "     ЗОЛОТО " + MetaProgression.Gold
            // Income would reveal how many clock fronts are free of enemies.
            + (manager.Clock != null ? "" : "  (+" + front.IncomePerTurn + "/ход)");
        statusText.text = GameRunMode.IsDeveloperRun
            ? "РЕЖИМ РАЗРАБОТЧИКА  ·  ХОД " + (manager.TurnNumber + 1)
                + "\nВСЕ КОМНАТЫ ОТКРЫТЫ · БЕЗ НАГРАД И ПРОГРЕССА"
            : manager.Clock != null
            ? "ВЫЖИВАНИЕ  ·  ХОД " + (manager.TurnNumber + 1) + "  ·  РЕКОРД " + GameRunMode.BestTurnsForCurrentMode
                + "\n" + manager.Clock.StatusLine()
            : manager.IsSurvivalMode
            ? "ВЫЖИВАНИЕ  ·  ХОД " + (manager.TurnNumber + 1)
                + "  ·  ДАВЛЕНИЕ " + manager.SurvivalPressureTurns
                + "\nРЕКОРД " + GameRunMode.BestTurnsForCurrentMode + "  ·  ДОХОД +" + front.IncomePerTurn
            : "ХОД " + (manager.TurnNumber + 1) + "   ·   ДОХОД +" + front.IncomePerTurn
                + "   ·   ЗА БОЙ +" + front.Model.EarnedGold + " ЗОЛОТА";
        RenderRooms(front.Model, front.CurrentRoomId, lastCanSwitch, front.GoldPerSecuredRoom);
    }

    public void Preview(DefenseFront model, int selectedId, int level, bool showMap)
    {
        if (Application.isPlaying)
        {
            return;
        }

        BuildInterface();
        mapPanel.SetActive(showMap);
        mapButton.gameObject.SetActive(!showMap);
        mapButton.interactable = true;
        if (waitButton != null)
        {
            waitButton.gameObject.SetActive(false);
        }

        toolbarText.text = "КАРТА  ·  " + model.GetRoom(selectedId).Label
            + "     ЗОЛОТО " + MetaProgression.Gold + "  (+" + model.Income(1) + "/ход)";
        statusText.text = LevelProgression.LocationNameForLevel(level) + " · УРОВЕНЬ "
            + LevelProgression.LocalLevelFor(level)
            + "\nПРЕДПРОСМОТР · ЛИНИЯ " + model.OpenedLine;
        RenderRooms(model, selectedId, false, 1);
    }

    private void RenderRooms(DefenseFront model, int selectedId, bool canSwitch, int goldPerRoom)
    {
        ClockSiegeDirector clock = model.IsClockLayout && manager != null ? manager.Clock : null;
        if (clock != null)
        {
            ApplyClockLayout(model);
        }

        foreach (DefenseFront.Room room in model.Rooms)
        {
            int id = room.Id;
            Color color;
            string description;
            // The clock siege map shows only rooms whose enemies the player has seen.
            // Every other room looks the same: no enemy count or breach warning.
            // A dark storm also blacks out the light meters of the fronts.
            bool hidden = clock != null && !clock.IsRoomRevealed(id);
            bool stormDark = clock != null && clock.IsStormActive && room.ClockHour > 0;
            switch (room.State)
            {
                case DefenseFront.RoomState.Disabled:
                    color = new Color(0.055f, 0.065f, 0.07f, 0.72f);
                    description = "НЕТ НА УРОВНЕ";
                    break;
                case DefenseFront.RoomState.Secured:
                    color = new Color(0.14f, 0.40f, 0.29f);
                    description = "+" + goldPerRoom + "/ход";
                    break;
                case DefenseFront.RoomState.Contested:
                    color = new Color(0.46f, 0.26f, 0.13f);
                    description = room.Enemies + " враг.\n"
                        + (manager != null && manager.IsSurvivalFinalLineActive && room.Line == 4
                            ? "∞ волн"
                            : room.Waves + " волн");
                    break;
                case DefenseFront.RoomState.Lost:
                    color = new Color(0.34f, 0.12f, 0.13f);
                    description = "ПОТЕРЯНА";
                    break;
                default:
                    color = new Color(0.14f, 0.17f, 0.19f);
                    description = "ЗАКРЫТА";
                    break;
            }
            if (clock != null)
            {
                ClockAppearance(room, hidden, stormDark, ref color, ref description);
            }

            roomLabels[id].text = room.Label + "\n" + description;
            roomBackgrounds[id].color = color;
            selectedBorders[id].enabled = id == selectedId;
            float progress = front != null && !hidden ? front.RoomBreachProgress(id) : 0f;
            if (breachFills != null && id < breachFills.Length && breachFills[id] != null)
            {
                Image fill = breachFills[id];
                fill.color = breachFillColor;
                fill.enabled = progress > 0f;
                fill.rectTransform.anchorMax = new Vector2(1f, progress);
            }
            if (breachHalos != null && id < breachHalos.Length && breachHalos[id] != null)
            {
                breachHalos[id].enabled = front != null && !hidden && front.TurnsUntilRoomBreach(id) <= 1;
            }

            bool showLight = room.State == DefenseFront.RoomState.Contested
                || room.State == DefenseFront.RoomState.Secured || hidden;
            float lit = stormDark ? 0f : front != null ? front.RoomLitFraction(id) : 1f;
            if (lightMeterLit != null && id < lightMeterLit.Length && lightMeterLit[id] != null)
            {
                Image meter = lightMeterLit[id];
                meter.color = litMeterColor;
                meter.enabled = showLight && lit > 0f;
                meter.rectTransform.anchorMin = new Vector2(1f - LightMeterWidth, 0f);
                meter.rectTransform.anchorMax = new Vector2(1f, lit);
            }
            if (lightMeterDark != null && id < lightMeterDark.Length && lightMeterDark[id] != null)
            {
                Image meter = lightMeterDark[id];
                meter.color = darkMeterColor;
                meter.enabled = showLight && lit < 1f;
                meter.rectTransform.anchorMin = new Vector2(1f - LightMeterWidth, lit);
                meter.rectTransform.anchorMax = Vector2.one;
            }
            roomButtons[id].interactable = canSwitch && room.CanSelect
                && (front == null || front.CanFocusWhileDefending(id));
            if (id > 0)
            {
                connections[id - 1].color = room.State == DefenseFront.RoomState.Disabled
                ? new Color(0.11f, 0.12f, 0.12f, 0.45f)
                : room.State == DefenseFront.RoomState.Lost
                ? new Color(0.85f, 0.33f, 0.20f) : room.State == DefenseFront.RoomState.Locked
                    ? new Color(0.23f, 0.28f, 0.28f) : new Color(0.52f, 0.68f, 0.53f);
            }
        }
    }

    private static void ClockAppearance(DefenseFront.Room room, bool hidden, bool stormDark,
        ref Color color, ref string description)
    {
        if (room.State == DefenseFront.RoomState.Disabled)
        {
            return;
        }

        if (!hidden)
        {
            // Seen enemies: show how many, but not whether more waves will come.
            if (room.State == DefenseFront.RoomState.Contested)
            {
                description = room.Enemies + " враг.";
            }

            return;
        }
        color = stormDark ? new Color(0.13f, 0.10f, 0.19f)
            : room.IsClockCenter ? new Color(0.20f, 0.27f, 0.22f) : new Color(0.16f, 0.20f, 0.18f);
        description = stormDark ? "БУРЯ" : "?";
    }

    // Clock siege: twelve fronts on a dial around the centre. Positions follow
    // the current hour of each front, which a dark storm can shuffle.
    private int[] clockLayoutHours;

    private void ApplyClockLayout(DefenseFront model)
    {
        if (roomBackgrounds == null)
        {
            return;
        }

        bool changed = clockLayoutHours == null || clockLayoutHours.Length != model.Rooms.Count;
        if (!changed)
        {
            for (int i = 0; i < model.Rooms.Count; i++)
            {
                changed |= clockLayoutHours[i] != model.Rooms[i].ClockHour;
            }
        }

        if (!changed)
        {
            return;
        }

        clockLayoutHours = new int[model.Rooms.Count];
        Vector2 center = ClockGraphPosition(model.GetRoom(0));
        foreach (DefenseFront.Room room in model.Rooms)
        {
            int id = room.Id;
            clockLayoutHours[id] = room.ClockHour;
            bool shown = room.Id == 0 || room.ClockHour > 0;
            Vector2 position = ClockGraphPosition(room);
            Vector2 half = id == 0 ? new Vector2(0.075f, 0.11f) : new Vector2(0.045f, 0.068f);
            if (id < roomBackgrounds.Length && roomBackgrounds[id] != null)
            {
                roomBackgrounds[id].gameObject.SetActive(shown);
                Stretch(roomBackgrounds[id].rectTransform, position - half, position + half);
            }
            if (selectedBorders != null && id < selectedBorders.Length && selectedBorders[id] != null)
            {
                selectedBorders[id].gameObject.SetActive(shown);
                Vector2 border = half + Vector2.one * 0.004f;
                Stretch(selectedBorders[id].rectTransform, position - border, position + border);
            }
            if (breachHalos != null && id < breachHalos.Length && breachHalos[id] != null)
            {
                breachHalos[id].gameObject.SetActive(shown);
                Vector2 halo = half + Vector2.one * 0.01f;
                Stretch(breachHalos[id].rectTransform, position - halo, position + halo);
            }
            if (id > 0 && connections != null && id - 1 < connections.Length && connections[id - 1] != null)
            {
                connections[id - 1].gameObject.SetActive(shown);
                DefenseMapConnection connection = connections[id - 1].GetComponent<DefenseMapConnection>();
                if (connection != null && shown)
                {
                    connection.Configure(position, center);
                }
            }
        }
    }

    private static Vector2 ClockGraphPosition(DefenseFront.Room room)
    {
        if (room == null || room.ClockHour <= 0)
        {
            return new Vector2(0.5f, 0.5f);
        }

        float angle = (90f - room.ClockHour * 30f) * Mathf.Deg2Rad;
        return new Vector2(0.5f + 0.27f * Mathf.Cos(angle), 0.5f + 0.39f * Mathf.Sin(angle));
    }

    [ContextMenu("Build editable defense map")]
    public void BuildInterface()
    {
        if (mapPanel != null) { EnsureThreatVisuals(); return; }
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        RectTransform safe = Rect("Safe Area", transform);
        Stretch(safe, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<MenuSafeArea>();

        RectTransform toolbar = Panel("Front Map Button", safe, new Color(0.06f, 0.14f, 0.12f, 0.96f));
        Stretch(toolbar, new Vector2(0.08f, 0.866f), new Vector2(0.92f, 0.913f));
        mapButton = toolbar.gameObject.AddComponent<Button>();
        mapButton.targetGraphic = toolbar.GetComponent<Image>();
        toolbarText = Label(toolbar, "КАРТА ОБОРОНЫ  ·  ЗОЛОТО", 26);
        RectTransform overlay = Panel("Defense Map", safe, new Color(0.025f, 0.05f, 0.045f, 1f));
        Stretch(overlay, Vector2.zero, Vector2.one);
        mapPanel = overlay.gameObject;
        RectTransform title = Rect("Title", overlay);
        Stretch(title, new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.96f));
        Label(title, "ЛИНИИ ОБОРОНЫ", 44);
        RectTransform status = Rect("Status", overlay);
        Stretch(status, new Vector2(0.03f, 0.77f), new Vector2(0.97f, 0.88f));
        statusText = Label(status, "Выберите комнату", 27);

        RectTransform graph = Rect("Room Connections", overlay);
        Stretch(graph, new Vector2(0.025f, 0.22f), new Vector2(0.975f, 0.76f));
        // Fixed aspect keeps connection endpoints aligned on phones and wide Game views.
        AspectRatioFitter fit = graph.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = 1.65f;
        DefenseFront layout = new DefenseFront();
        connections = new Image[15];
        foreach (DefenseFront.Room room in layout.Rooms)
        {
            if (room.ParentId < 0)
            {
                continue;
            }

            Vector2 from = GraphPosition(room);
            Vector2 to = GraphPosition(layout.GetRoom(room.ParentId));
            RectTransform edge = Panel(room.Label + " to parent", graph, new Color(0.4f, 0.55f, 0.43f));
            // Store endpoints; the tiny layout helper updates only when the rect size changes.
            DefenseMapConnection connection = edge.gameObject.AddComponent<DefenseMapConnection>();
            connection.Configure(from, to);
            connections[room.Id - 1] = edge.GetComponent<Image>();
            connections[room.Id - 1].raycastTarget = false;
        }
        roomButtons = new Button[16];
        roomLabels = new Text[16];
        roomBackgrounds = new Image[16];
        selectedBorders = new Image[16];
        foreach (DefenseFront.Room room in layout.Rooms)
        {
            Vector2 center = GraphPosition(room);
            Vector2 half = new Vector2(0.061f, 0.091f);
            RectTransform border = Panel("Selected " + room.Label, graph, new Color(1f, 0.84f, 0.29f));
            Stretch(border, center - half - Vector2.one * 0.004f, center + half + Vector2.one * 0.004f);
            border.GetComponent<Image>().raycastTarget = false;
            selectedBorders[room.Id] = border.GetComponent<Image>();
            selectedBorders[room.Id].enabled = room.Id == 0;
            RectTransform node = Panel("Room " + room.Label, graph, new Color(0.14f, 0.22f, 0.19f));
            Stretch(node, center - half, center + half);
            roomBackgrounds[room.Id] = node.GetComponent<Image>();
            roomButtons[room.Id] = node.gameObject.AddComponent<Button>();
            roomButtons[room.Id].targetGraphic = roomBackgrounds[room.Id];
            roomButtons[room.Id].transition = Selectable.Transition.None;
            roomLabels[room.Id] = Label(node, room.Label, 24);
            roomLabels[room.Id].resizeTextMinSize = 12;
        }
        EnsureThreatVisuals();
        RectTransform legend = Rect("Legend", overlay);
        Stretch(legend, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.24f));
        Label(legend, LegendText, 24);
        RectTransform close = Panel("Return To Battle", overlay, new Color(0.2f, 0.39f, 0.31f));
        Stretch(close, new Vector2(0.25f, 0.035f), new Vector2(0.75f, 0.105f));
        closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close.GetComponent<Image>();
        closeButton.onClick.AddListener(CloseMap);
        Label(close, "ВЕРНУТЬСЯ В БОЙ", 30);
        mapPanel.SetActive(false);
        waitButton = null;
    }

    private void EnsureThreatVisuals()
    {
        if (roomBackgrounds == null || roomBackgrounds.Length == 0)
        {
            return;
        }

        if (breachFillColor.a <= 0f)
        {
            breachFillColor = new Color(0.47f, 0.045f, 0.13f, 0.96f);
        }

        if (breachHaloColor.a <= 0f)
        {
            breachHaloColor = new Color(1f, 0.20f, 0.31f, 1f);
        }

        Transform legend = mapPanel != null ? mapPanel.transform.Find("Legend/Label") : null;
        Text legendText = legend != null ? legend.GetComponent<Text>() : null;
        // Upgrade only the known older built-in legends; custom text is kept.
        if (legendText != null && (legendText.text.Contains("Связи показывают путь врагов")
            || legendText.text == "Золотая рамка — вы здесь · Зелёная — доход\n"
                + "Бордовое заполнение — враг ближе к прорыву\n"
                + "Мигающая рамка — прорыв через ход"))
        {
            legendText.text = LegendText;
        }

        if (lightMeterLit == null || lightMeterLit.Length != roomBackgrounds.Length)
        {
            lightMeterLit = new Image[roomBackgrounds.Length];
        }

        if (lightMeterDark == null || lightMeterDark.Length != roomBackgrounds.Length)
        {
            lightMeterDark = new Image[roomBackgrounds.Length];
        }

        if (breachFills == null || breachFills.Length != roomBackgrounds.Length)
        {
            breachFills = new Image[roomBackgrounds.Length];
        }

        if (breachHalos == null || breachHalos.Length != roomBackgrounds.Length)
        {
            breachHalos = new Image[roomBackgrounds.Length];
        }

        DefenseFront layout = new DefenseFront();
        for (int id = 0; id < roomBackgrounds.Length; id++)
        {
            Image node = roomBackgrounds[id];
            if (node == null)
            {
                continue;
            }

            if (breachFills[id] == null)
            {
                Transform existing = node.transform.Find("Burgundy Breach Fill");
                RectTransform fill = existing != null ? (RectTransform)existing
                    : Panel("Burgundy Breach Fill", node.transform, breachFillColor);
                Stretch(fill, Vector2.zero, new Vector2(1f, 0f));
                fill.SetAsFirstSibling();
                breachFills[id] = fill.GetComponent<Image>();
                breachFills[id].raycastTarget = false;
                breachFills[id].enabled = false;
            }
            if (breachHalos[id] == null)
            {
                Transform existing = node.transform.parent.Find("Breach Halo " + layout.GetRoom(id).Label);
                RectTransform halo = existing != null ? (RectTransform)existing
                    : Panel("Breach Halo " + layout.GetRoom(id).Label,
                        node.transform.parent, breachHaloColor);
                Vector2 center = GraphPosition(layout.GetRoom(id));
                Vector2 half = new Vector2(0.071f, 0.101f);
                Stretch(halo, center - half, center + half);
                if (selectedBorders != null && id < selectedBorders.Length && selectedBorders[id] != null)
                {
                    halo.SetSiblingIndex(selectedBorders[id].transform.GetSiblingIndex());
                }

                breachHalos[id] = halo.GetComponent<Image>();
                breachHalos[id].raycastTarget = false;
                breachHalos[id].enabled = false;
            }
            if (lightMeterLit[id] == null)
            {
                lightMeterLit[id] = LightMeterPart(node.transform, "Light Meter Lit", litMeterColor);
            }

            if (lightMeterDark[id] == null)
            {
                lightMeterDark[id] = LightMeterPart(node.transform, "Light Meter Dark", darkMeterColor);
            }
        }
    }

    // A thin vertical strip on the right edge of a room node. Reuses an
    // authored child with the same name when the map is already in the scene.
    private static Image LightMeterPart(Transform node, string name, Color color)
    {
        Transform existing = node.Find(name);
        RectTransform part = existing != null ? (RectTransform)existing : Panel(name, node, color);
        if (existing == null)
        {
            Stretch(part, new Vector2(1f - LightMeterWidth, 0f), Vector2.one);
        }

        Image image = part.GetComponent<Image>();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private void PulseDangerHalos()
    {
        if (breachHalos == null)
        {
            return;
        }

        Color color = breachHaloColor;
        color.a *= 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f) * Mathf.Sin(Time.unscaledTime * 5f);
        foreach (Image halo in breachHalos)
        {
            if (halo != null && halo.enabled)
            {
                halo.color = color;
            }
        }
    }

    private static Vector2 GraphPosition(DefenseFront.Room room)
        => new Vector2(0.5f + (room.Index - room.Line) * 0.143f, 0.12f + (room.Line - 1) * 0.25f);

    private static RectTransform Rect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }
    private static RectTransform Panel(string name, Transform parent, Color color)
    {
        RectTransform rect = Rect(name, parent);
        rect.gameObject.AddComponent<CanvasRenderer>();
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static Text Label(Transform parent, string text, int size)
    {
        RectTransform rect = Rect("Label", parent);
        Stretch(rect, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.98f));
        rect.gameObject.AddComponent<CanvasRenderer>();
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = GameTypography.Font;
        label.text = text;
        label.color = new Color(0.96f, 0.95f, 0.86f);
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = size;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 16;
        label.resizeTextMaxSize = size;
        label.raycastTarget = false;
        GameTypography.Apply(label);
        return label;
    }
    private void OnDestroy()
    {
        if (front != null)
        {
            front.Changed -= MarkDirty;
        }
    }
}
