using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit editor command only. The saved hierarchy is never rebuilt on load or in Play mode.
public static class MenuHubBuilder
{
    private const string MenuPath = "Assets/Scenes/UIscene.unity";
    private const string DataPath = "Assets/UI/MenuData";
    private const string PrefabPath = "Assets/UI/MenuPrefabs";
    private static readonly Color Background = Hex("101F1B");
    private static readonly Color Panel = Hex("20372D");
    private static readonly Color Muted = Hex("A1B6A4");
    private static readonly Color Gold = Hex("F3CB78");
    private static readonly Color Cream = Hex("F4EDD8");
    private static readonly Color Green = Hex("446A45");
    private static Font Font => GameTypography.Font;
    private static Sprite Round => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    private static Sprite Circle => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    [MenuItem("Forest Animal War/UI/Install editable menu")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before installing UI.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MenuPath) throw new InvalidOperationException("Open Assets/Scenes/UIscene.unity first. Other scenes are not modified.");
        var existing = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
        if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
        EnsureFolder(DataPath);
        EnsureFolder(PrefabPath);
        var eventDefinitions = CreateEvents();
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
            if (canvas.isRootCanvas && canvas.gameObject.scene == scene)
            {
                Undo.RecordObject(canvas.gameObject, "Preserve legacy menu");
                canvas.gameObject.SetActive(false);
            }

        var root = new GameObject("Forest Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MenuHubController));
        Undo.RegisterCreatedObjectUndo(root, "Create editable forest menu");
        var canvasComponent = root.GetComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasComponent.sortingOrder = 60;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var hub = root.GetComponent<MenuHubController>();
        var backdrop = Image("Backdrop", root.transform, Background); Stretch(backdrop);
        var safe = Rect("Safe Area", root.transform); Stretch(safe); safe.gameObject.AddComponent<MenuSafeArea>();
        var frame = Rect("Portrait Frame", safe); Stretch(frame);
        frame.gameObject.AddComponent<MenuDesignFrame>();
        var inner = Image("Frame Background", frame, Background); Stretch(inner);
        BuildHeader(frame, hub);
        var pages = Rect("Pages", frame);
        Stretch(pages); pages.offsetMin = new Vector2(0, 172); pages.offsetMax = new Vector2(0, -210);
        hub.tabs = new MenuHubController.Tab[4];
        string[] titles = { "Кампания", "Артефакты", "Деревня", "События" };
        string[] ids = { "Campaign", "Hero", "Village", "Events" };
        var contents = new RectTransform[4];
        for (int i = 0; i < hub.tabs.Length; i++)
        {
            var page = Rect(ids[i] + " Page", pages); Stretch(page);
            contents[i] = ScrollPage(page, i == 0 ? 1600 : i == 1 ? 2050
                : i == 2 ? 1900 : 1490);
            hub.tabs[i] = new MenuHubController.Tab { title = titles[i], page = page.gameObject };
        }
        BuildCampaign(contents[0], hub);
        BuildEvents(contents[3], hub, eventDefinitions);
        BuildNavigation(frame, hub);
        BuildDialogs(frame, hub);
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        hub.PrepareVillageAndHero();
        hub.SelectTab(0);
        hub.Refresh();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log("Editable Forest Menu saved in " + MenuPath + ". Select Forest Menu to preview pages and dialogs.");
    }

    private static void BuildHeader(RectTransform frame, MenuHubController hub)
    {
        var header = Image("Header", frame, Hex("152A22")); Top(header, 0, 0, 1080, 200);
        Label("Brand", header, "FOREST ANIMAL WAR", 34, Gold, 44, 24, 650, 52, true);
        Label("Subtitle", header, "Л Е С Н О Й   Л А Г Е Р Ь", 21, Muted, 46, 79, 620, 35);
        var settings = Button("Settings", header, "···", 48, Panel, 910, 28, 126, 86);
        UnityEventTools.AddPersistentListener(settings.onClick, hub.ShowSettings);
        EnsureResourceBar(hub);
        var line = Image("Gold Divider", header, Hex("496044")); Top(line, 44, 198, 992, 2);
    }

    public static void EnsureResourceBar(MenuHubController hub)
    {
        Transform header = hub.transform.Find("Safe Area/Portrait Frame/Header");
        if (header == null) throw new InvalidOperationException("Forest menu header is missing.");
        Transform oldWallet = header.Find("Wallet");
        if (oldWallet != null) UnityEngine.Object.DestroyImmediate(oldWallet.gameObject);
        Transform oldTag = header.Find("Tag");
        if (oldTag != null) UnityEngine.Object.DestroyImmediate(oldTag.gameObject);

        Transform bar = header.Find("Resource Bar");
        if (bar == null)
        {
            RectTransform root = Rect("Resource Bar", header);
            Top(root, 44, 129, 992, 58);
            bar = root;
            string[] ids = { "Acorns", "Gold", "Seeds", "Diamonds" };
            string[] labels = { "ЖЁЛУДИ", "ЗОЛОТО", "СЕМЕНА", "БРИЛЛИАНТЫ" };
            for (int i = 0; i < ids.Length; i++)
            {
                RectTransform badge = Rect(ids[i], bar);
                Top(badge, i * 252, 0, 236, 58);
                var background = badge.gameObject.AddComponent<Image>();
                background.color = Panel;
                background.raycastTarget = true;
                var button = badge.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                UnityEventTools.AddPersistentListener(button.onClick, hub.ShowResources);
                RectTransform iconRect = Rect("Icon", badge);
                Top(iconRect, 10, 7, 44, 44);
                var icon = iconRect.gameObject.AddComponent<ResourceIconGraphic>();
                icon.Kind = (ResourceIconKind)i;
                icon.raycastTarget = false;
                Label("Name", badge, labels[i], 17, Muted, 64, 3, 162, 23, true);
                Label("Amount", badge, "0", 24, Cream, 64, 23, 162, 31, true);
            }
        }
        hub.currencyText = bar.Find("Acorns/Amount").GetComponent<Text>();
        hub.goldText = bar.Find("Gold/Amount").GetComponent<Text>();
        hub.seedsText = bar.Find("Seeds/Amount").GetComponent<Text>();
        hub.diamondsText = bar.Find("Diamonds/Amount").GetComponent<Text>();
        EditorUtility.SetDirty(hub);
    }

    private static void BuildCampaign(RectTransform content, MenuHubController hub)
    {
        Label("Eyebrow", content, "КАМПАНИЯ", 23, Gold, 44, 18, 900, 40, true);
        Label("Title", content, "Защити свой лес", 56, Cream, 44, 60, 992, 78, true);
        Label("Description", content, "Четыре линии. Один последний рубеж.", 28, Muted, 44, 145, 992, 45);
        var hero = Image("Forest Hero", content, Panel); Top(hero, 44, 224, 992, 548);
        hero.gameObject.AddComponent<RectMask2D>();
        var art = new GameObject("Forest Illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        art.transform.SetParent(hero, false); Stretch((RectTransform)art.transform);
        var raw = art.GetComponent<RawImage>();
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/ChatGPT Image 30 авг. 2026 г., 17_47_00.png");
        raw.uvRect = new Rect(0, 0.355f, 1, 0.31f); raw.raycastTarget = false;
        var caption = Image("Hero Caption", hero, new Color(0.025f, 0.075f, 0.055f, 0.91f)); Top(caption, 0, 419, 992, 129);
        hub.locationText = Label("Hero Title", caption, "СУМЕРЕЧНАЯ ЧАЩА", 32, Gold,
            96, 12, 800, 51, true, TextAnchor.MiddleCenter);
        Label("Hero Detail", caption, "1 → 3 → 5 → разные внешние пути  ·  5 уровней", 25,
            Cream, 96, 69, 800, 42, false, TextAnchor.MiddleCenter);
        hub.previousLocationButton = Button("Previous Location", caption, "‹", 42,
            Hex("263D33"), 20, 28, 58, 72);
        hub.nextLocationButton = Button("Next Location", caption, "›", 42,
            Hex("263D33"), 914, 28, 58, 72);
        UnityEventTools.AddPersistentListener(hub.previousLocationButton.onClick, hub.ShowPreviousLocation);
        UnityEventTools.AddPersistentListener(hub.nextLocationButton.onClick, hub.ShowNextLocation);
        MenuEncounterPreviewAuthoring.EnsureLocationEncounterSlots(hub, hero);
        hub.campaignText = Label("Selected Level", content, "СУМЕРЕЧНАЯ ЧАЩА  ·  УРОВЕНЬ 01 / 05", 25, Gold, 44, 816, 992, 46, true);
        var levels = new List<MenuLevelCard>();
        for (int i = 0; i < LevelProgression.LevelsPerLocation; i++)
        {
            var button = Button("Level " + (i + 1).ToString("00"), content, "", 30, Panel, 44 + i * 201, 886, 188, 119);
            var card = button.gameObject.AddComponent<MenuLevelCard>();
            card.hub = hub; card.levelNumber = i + 1; card.background = button.image;
            card.numberText = Label("Number", button.transform, (i + 1).ToString("00"), 38, Cream, 0, 12, 188, 60, true, TextAnchor.MiddleCenter);
            card.stateText = Label("State", button.transform, "ЗАКРЫТ", 17, Cream, 0, 77, 188, 28, false, TextAnchor.MiddleCenter);
            UnityEventTools.AddPersistentListener(button.onClick, card.SelectLevel);
            levels.Add(card);
        }
        hub.levels = levels.ToArray();
        var play = Button("Play Selected Level", content, "В БОЙ  ·  УРОВЕНЬ 1", 39, Green, 44, 1060, 992, 114);
        hub.playText = play.GetComponentInChildren<Text>();
        UnityEventTools.AddPersistentListener(play.onClick, hub.PlaySelectedLevel);
        Label("Loadout Hint", content, "Старт: 3 светлячка. Новые звери — за опыт в бою.", 25, Muted, 44, 1201, 992, 72, false, TextAnchor.MiddleCenter);
        BuildSurvivalCard(content, hub);
        SaveTemplate(levels[0].gameObject, "LevelCard");
    }

    private static void BuildSurvivalCard(RectTransform content, MenuHubController hub)
    {
        var panel = Image("Survival Mode Card", content, Hex("263D33"));
        Top(panel, 44, 1292, 992, 235);
        var edge = Image("Accent", panel, Gold); Top(edge, 0, 0, 7, 235);
        Label("Eyebrow", panel, "БЕСКОНЕЧНЫЙ РЕЖИМ", 20, Gold, 30, 20, 560, 34, true);
        Label("Title", panel, "Выживание", 42, Cream, 30, 55, 560, 58, true);
        Label("Description", panel, "12 фронтов вокруг одной баррикады.\nФронты открываются, свет гаснет.", 23, Muted, 30, 119, 560, 82);
        hub.survivalBestText = Label("Best Result", panel, "РЕКОРД  ·  ЕЩЁ НЕ УСТАНОВЛЕН",
            21, Gold, 614, 30, 346, 48, true, TextAnchor.MiddleCenter);
        var play = Button("Play Survival", panel, "НАЧАТЬ ВЫЖИВАНИЕ", 25, Green,
            614, 92, 346, 102);
        hub.survivalPlayText = play.GetComponentInChildren<Text>();
        UnityEventTools.AddPersistentListener(play.onClick, hub.PlaySurvival);
        content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(content.sizeDelta.y, 1570f));
    }

    [MenuItem("Forest Animal War/UI/Add survival mode to editable menu")]
    public static void InstallSurvivalControls()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before installing UI.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MenuPath)
            throw new InvalidOperationException("Open Assets/Scenes/UIscene.unity first.");
        MenuHubController hub = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null || hub.tabs == null || hub.tabs.Length == 0 || hub.tabs[0].page == null)
            throw new InvalidOperationException("Install the editable menu first.");
        Transform contentTransform = hub.tabs[0].page.transform.Find("Viewport/Content");
        if (contentTransform == null)
            throw new InvalidOperationException("Campaign scroll content was not found.");
        RectTransform content = (RectTransform)contentTransform;
        Transform existing = content.Find("Survival Mode Card");
        if (existing == null)
        {
            BuildSurvivalCard(content, hub);
            Undo.RegisterCreatedObjectUndo(content.Find("Survival Mode Card").gameObject,
                "Add survival mode card");
        }
        else
        {
            hub.survivalBestText = existing.Find("Best Result")?.GetComponent<Text>();
            hub.survivalPlayText = existing.Find("Play Survival/Label")?.GetComponent<Text>();
        }
        if (hub.survivalBestText != null)
            hub.survivalBestText.text = "РЕКОРД  ·  ЕЩЁ НЕ УСТАНОВЛЕН";
        if (hub.survivalPlayText != null)
            hub.survivalPlayText.text = "НАЧАТЬ ВЫЖИВАНИЕ";
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = existing != null ? existing.gameObject
            : content.Find("Survival Mode Card").gameObject;
    }

    private static void BuildEvents(RectTransform content, MenuHubController hub, MenuEventDefinition[] data)
    {
        Label("Eyebrow", content, "СОБЫТИЯ ЛЕСА", 23, Gold, 44, 18, 992, 40, true);
        Label("Title", content, "Путь защитника", 56, Cream, 44, 67, 992, 76, true);
        Label("Description", content, "Испытания без срока. Каждая победа приближает награду.", 26, Muted, 44, 153, 992, 76);
        hub.victoriesText = Label("Victories", content, "ПОБЕДЫ: 0", 25, Gold, 44, 235, 992, 44, true);
        var list = new List<MenuEventCard>();
        for (int i = 0; i < data.Length; i++)
        {
            var panel = Image("Event " + data[i].saveId, content, Panel); Top(panel, 44, 316 + i * 344, 992, 317);
            var view = panel.gameObject.AddComponent<MenuEventCard>(); view.hub = hub; view.definition = data[i];
            Label("Milestone", panel, "0" + (i + 1), 51, Gold, 29, 20, 103, 87, true);
            view.titleText = Label("Title", panel, data[i].displayName, 36, Cream, 145, 26, 819, 63, true);
            view.descriptionText = Label("Description", panel, data[i].description, 25, Muted, 145, 99, 819, 64);
            view.progressText = Label("Progress", panel, "ПОБЕДЫ: 0 / " + data[i].requiredVictories, 22, Muted, 29, 181, 500, 35);
            var track = Image("Progress Track", panel, Background); Top(track, 29, 224, 500, 9);
            var fill = Image("Progress Fill", track, Gold); Stretch(fill);
            view.progressFill = fill.GetComponent<Image>(); view.progressFill.type = UnityEngine.UI.Image.Type.Filled; view.progressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            view.rewardText = Label("Reward", panel, "+" + data[i].acornReward + " ЖЕЛУДЕЙ", 23, Gold, 29, 251, 500, 39, true);
            view.claimButton = Button("Claim Reward", panel, "В ПРОЦЕССЕ", 24, Green, 585, 208, 378, 78);
            view.actionText = view.claimButton.GetComponentInChildren<Text>();
            UnityEventTools.AddPersistentListener(view.claimButton.onClick, view.Claim);
            list.Add(view);
            if (i == 0) SaveTemplate(panel.gameObject, "EventCard");
        }
        hub.events = list.ToArray();
        Label("Note", content, "Награду каждого испытания можно забрать один раз.\nСчётчик учитывает победы после появления событий.", 25, Muted, 44, 1370, 992, 82, false, TextAnchor.MiddleCenter);
    }

    private static void BuildNavigation(RectTransform parent, MenuHubController hub)
    {
        var bar = Image("Bottom Navigation", parent, Hex("11261F"));
        bar.anchorMin = bar.anchorMax = new Vector2(0, 0); bar.pivot = Vector2.zero; bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(1080, 154);
        for (int i = 0; i < hub.tabs.Length; i++)
            BuildNavigationTab(bar, hub, i);
    }

    private static void BuildNavigationTab(RectTransform bar, MenuHubController hub, int i)
    {
        string[] glyphs = { "◆", "●", "▣", "✦" };
        float width = 1080f / hub.tabs.Length;
        var button = Button(hub.tabs[i].title + " Tab", bar, "", 22, Background,
            i * width, 0, width, 154);
        Label("Icon", button.transform, glyphs[i], 42, Gold, 0, 19, width, 60,
            false, TextAnchor.MiddleCenter);
        hub.tabs[i].buttonLabel = Label("Label", button.transform, hub.tabs[i].title,
            24, Muted, 0, 92, width, 39, true, TextAnchor.MiddleCenter);
        hub.tabs[i].buttonBackground = button.image;
        var mark = Image("Selected Mark", button.transform, Gold);
        Top(mark, (width - 112) / 2f, 0, 112, 5);
        hub.tabs[i].selectedMark = mark.gameObject;
        UnityEventTools.AddIntPersistentListener(button.onClick, hub.SelectTab, i);
        if (i == 3)
        {
            var badge = Image("Reward Ready", button.transform, Hex("F0BD5D"));
            Top(badge, width - 54, 20, 19, 19);
            badge.GetComponent<Image>().sprite = Circle;
            hub.eventBadge = badge.gameObject;
        }
    }

    [MenuItem("Forest Animal War/UI/Bake developer-mode toggle into UIscene")]
    public static void BakeDeveloperModeToggleBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        MenuHubController hub = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null || hub.settingsModal == null)
            throw new InvalidOperationException("The editable settings dialog is missing from UIscene.");
        Transform panel = hub.settingsModal.transform.Find("Panel");
        if (panel == null) throw new InvalidOperationException("Settings panel is missing.");
        EnsureDeveloperModeToggle(panel, hub);
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Editable developer-mode toggle saved to " + MenuPath);
    }

    private static void EnsureDeveloperModeToggle(Transform settingsPanel, MenuHubController hub)
    {
        Transform existing = settingsPanel.Find("Toggle Developer Mode");
        Button toggle = existing != null ? existing.GetComponent<Button>()
            : Button("Toggle Developer Mode", settingsPanel,
                "РЕЖИМ РАЗРАБОТЧИКА: ВЫКЛ", 29, Green, 38, 330, 869, 88);
        if (toggle == null) throw new InvalidOperationException("Developer-mode button is missing.");
        if (existing == null) UnityEventTools.AddPersistentListener(toggle.onClick, hub.ToggleDeveloperMode);
        hub.developerModeButtonText = toggle.GetComponentInChildren<Text>(true);
        Transform help = settingsPanel.Find("Help");
        if (help != null)
        {
            Top((RectTransform)help, 38, 445, 869, 105);
            Text helpText = help.GetComponent<Text>();
            if (helpText != null)
                helpText.text = "Тестовые забеги открывают все комнаты и уровни.\nПрогресс и награды в них не сохраняются.";
        }
    }

    private static void BuildDialogs(RectTransform parent, MenuHubController hub)
    {
        var dialogs = Rect("Dialogs", parent); Stretch(dialogs);
        var info = Modal("Information Dialog", dialogs, hub, 945, 680, out var infoPanel); hub.informationModal = info.gameObject;
        hub.informationTitle = Label("Title", infoPanel, "Награда получена", 46, Gold, 38, 87, 869, 80, true);
        hub.informationBody = Label("Body", infoPanel, "Описание награды или подсказки", 32, Cream, 38, 205, 869, 289);
        var close = Button("Continue", infoPanel, "ПОНЯТНО", 31, Green, 38, 552, 869, 90);
        UnityEventTools.AddPersistentListener(close.onClick, hub.CloseDialogs);
        var settings = Modal("Settings Dialog", dialogs, hub, 945, 715, out var settingsPanel); hub.settingsModal = settings.gameObject;
        Label("Title", settingsPanel, "Настройки лагеря", 46, Gold, 38, 87, 869, 80, true);
        var audio = Button("Toggle Audio", settingsPanel, "ЗВУК: ВКЛ", 32, Green, 38, 220, 869, 94);
        hub.soundButtonText = audio.GetComponentInChildren<Text>(); UnityEventTools.AddPersistentListener(audio.onClick, hub.ToggleSound);
        Label("Help", settingsPanel, "Развивай деревню, надевай артефакты и защищай лес.\n\nАртефакты хранятся в рюкзаке героя. Бонусы дают только надетые предметы.", 29, Muted, 38, 354, 869, 195);
        EnsureDeveloperModeToggle(settingsPanel, hub);
        var done = Button("Close Settings", settingsPanel, "ВЕРНУТЬСЯ", 31, Green, 38, 584, 869, 89);
        UnityEventTools.AddPersistentListener(done.onClick, hub.CloseDialogs);
    }

    private static RectTransform Modal(string name, Transform parent, MenuHubController hub, float width, float height, out RectTransform panel)
    {
        var overlay = Image(name, parent, new Color(0, 0.02f, 0.01f, 0.88f)); Stretch(overlay);
        overlay.GetComponent<Image>().raycastTarget = true;
        panel = Image("Panel", overlay, Panel);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f); panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(width, height);
        panel.GetComponent<Image>().raycastTarget = true;
        var close = Button("Close", panel, "×", 42, Background, width - 93, 20, 72, 72);
        UnityEventTools.AddPersistentListener(close.onClick, hub.CloseDialogs);
        return overlay;
    }

    private static RectTransform ScrollPage(RectTransform page, float height)
    {
        var scroll = page.gameObject.AddComponent<ScrollRect>();
        var viewport = Image("Viewport", page, Color.clear); Stretch(viewport);
        viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1); content.sizeDelta = new Vector2(0, height);
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
        return content;
    }

    private static void DrawAnimal(Transform parent, int index, Vector2 center, Color color)
    {
        var badge = Image("Portrait Badge", parent, Background); Top(badge, center.x - 78, center.y - 78, 156, 156); badge.GetComponent<Image>().sprite = Circle;
        if (index == 0)
        {
            Oval(badge, "Wing Left", new Vector2(35, 50), new Vector2(58, 31), Hex("C5D9A2"), -28);
            Oval(badge, "Wing Right", new Vector2(102, 50), new Vector2(58, 31), Hex("C5D9A2"), 28);
            Oval(badge, "Glow", new Vector2(78, 88), new Vector2(70, 82), new Color(color.r, color.g, color.b, .2f));
            Oval(badge, "Body", new Vector2(78, 84), new Vector2(33, 64), color);
            Oval(badge, "Head", new Vector2(78, 47), new Vector2(34, 34), Hex("C8AF69"));
        }
        else if (index == 1)
        {
            for (int k = 0; k < 7; k++)
            {
                float angle = k * 25 - 70;
                var spike = Image("Quill " + k, badge, color);
                Top(spike, 68 + Mathf.Sin(angle * Mathf.Deg2Rad) * 41, 70 - Mathf.Cos(angle * Mathf.Deg2Rad) * 40, 25, 44);
                spike.localRotation = Quaternion.Euler(0, 0, -angle);
            }
            Oval(badge, "Body", new Vector2(78, 92), new Vector2(96, 76), color);
            Oval(badge, "Face", new Vector2(110, 98), new Vector2(59, 47), Hex("DAD4B5"));
            Oval(badge, "Eye", new Vector2(118, 90), new Vector2(10, 10), Background);
        }
        else if (index == 2)
        {
            Oval(badge, "Ear Left", new Vector2(56, 41), new Vector2(31, 80), color, 12);
            Oval(badge, "Ear Right", new Vector2(99, 41), new Vector2(31, 80), color, -12);
            Oval(badge, "Face", new Vector2(78, 100), new Vector2(102, 89), color);
            Oval(badge, "Eye Left", new Vector2(56, 94), new Vector2(11, 13), Background);
            Oval(badge, "Eye Right", new Vector2(100, 94), new Vector2(11, 13), Background);
            Oval(badge, "Nose", new Vector2(78, 115), new Vector2(12, 9), Hex("D894A5"));
        }
        else if (index == 3)
        {
            Oval(badge, "Ear Left", new Vector2(40, 52), new Vector2(31, 31), Hex("4A463D"));
            Oval(badge, "Ear Right", new Vector2(116, 52), new Vector2(31, 31), Hex("4A463D"));
            Oval(badge, "Body", new Vector2(78, 88), new Vector2(112, 103), Hex("282722"));
            Oval(badge, "Mantle", new Vector2(78, 59), new Vector2(104, 53), Hex("D8CBA9"));
            Oval(badge, "Eye Left", new Vector2(55, 88), new Vector2(13, 13), Hex("D49A4A"));
            Oval(badge, "Eye Right", new Vector2(101, 88), new Vector2(13, 13), Hex("D49A4A"));
            Oval(badge, "Muzzle", new Vector2(78, 111), new Vector2(34, 22), Hex("4A463D"));
            Oval(badge, "Nose", new Vector2(78, 108), new Vector2(12, 9), Background);
        }
        else
        {
            Oval(badge, "Ear Left", new Vector2(43, 51), new Vector2(31, 38), Hex("5B3825"), -24);
            Oval(badge, "Ear Right", new Vector2(113, 51), new Vector2(31, 38), Hex("5B3825"), 24);
            Oval(badge, "Body", new Vector2(78, 86), new Vector2(120, 105), Hex("5A3420"));
            Oval(badge, "Eye Left", new Vector2(55, 84), new Vector2(14, 14), Hex("FFC42A"));
            Oval(badge, "Eye Right", new Vector2(101, 84), new Vector2(14, 14), Hex("FFC42A"));
            Oval(badge, "Snout", new Vector2(78, 109), new Vector2(56, 37), Hex("A86448"));
            Oval(badge, "Tusk Left", new Vector2(47, 110), new Vector2(16, 29), Hex("F6E5BD"), 25);
            Oval(badge, "Tusk Right", new Vector2(109, 110), new Vector2(16, 29), Hex("F6E5BD"), -25);
        }
    }
    private static void Oval(Transform parent, string name, Vector2 center, Vector2 size, Color color, float angle = 0)
    {
        var image = Image(name, parent, color); Top(image, center.x - size.x / 2, center.y - size.y / 2, size.x, size.y); image.GetComponent<Image>().sprite = Circle; image.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private static MenuAnimalDefinition[] CreateDefinitions()
    {
        string[] names = { "Светлячок", "Ёж", "Кролик", "Медоед", "Кабан" };
        string[] roles = { "БАЗОВЫЙ СНАРЯД", "УРОН ПО ОБЛАСТИ", "РАЗМНОЖЕНИЕ", "ЦЕПНОЙ ОХОТНИК", "НЕУДЕРЖИМЫЙ ТАРАН" };
        string[] descriptions = { "Три светлячка открывают каждый забег. Рикошетят и наносят урон при столкновениях.", "При контакте с врагом выпускает расширяющуюся область взрыва.", "Первое размножение происходит после двух ударов. После каждого рождения порог растёт на один до десяти, а каждый кролик живёт двадцать ударов.", "Прыгает на самого раненого врага в радиусе 1,2 клетки. Убийство удлиняет цепь и усиливает её урон на 25%.", "Бежит сквозь врагов и отражается от стен. На каждом отрезке каждый враг получает урон один раз." };
        string[] ids = { "Firefly", "Hedgehog", "Rabbit", "HoneyBadger", "Boar" };
        Color[] colors = { Gold, Hex("6FCDB6"), Hex("C7B0E5"), Hex("D49A4A"), Hex("FFB11F") };
        var result = new MenuAnimalDefinition[5];
        for (int i = 0; i < result.Length; i++)
        {
            string path = DataPath + "/" + ids[i] + ".asset";
            result[i] = AssetDatabase.LoadAssetAtPath<MenuAnimalDefinition>(path);
            if (result[i] != null) continue;
            var data = ScriptableObject.CreateInstance<MenuAnimalDefinition>();
            data.animalIndex = i; data.displayName = names[i]; data.role = roles[i]; data.description = descriptions[i]; data.accent = colors[i];
            AssetDatabase.CreateAsset(data, path); result[i] = data;
        }
        return result;
    }
    private static MenuEventDefinition[] CreateEvents()
    {
        string[] titles = { "Первый рубеж", "Верный защитник", "Легенда леса" };
        int[] wins = { 1, 3, 10 }; int[] rewards = { 5, 15, 40 };
        var result = new MenuEventDefinition[3];
        for (int i = 0; i < 3; i++)
        {
            string id = "forest_victories_" + wins[i]; string path = DataPath + "/" + id + ".asset";
            result[i] = AssetDatabase.LoadAssetAtPath<MenuEventDefinition>(path); if (result[i] != null) continue;
            var data = ScriptableObject.CreateInstance<MenuEventDefinition>();
            data.saveId = id; data.displayName = titles[i]; data.requiredVictories = wins[i]; data.acornReward = rewards[i];
            data.description = "Победи в " + wins[i] + " забегах. Можно проходить любимый уровень повторно.";
            AssetDatabase.CreateAsset(data, path); result[i] = data;
        }
        return result;
    }
    private static void SaveTemplate(GameObject source, string name)
    {
        string path = PrefabPath + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var copy = UnityEngine.Object.Instantiate(source);
        copy.name = name;
        if (copy.TryGetComponent<MenuLevelCard>(out var level)) level.hub = null;
        if (copy.TryGetComponent<MenuEventCard>(out var mission)) mission.hub = null;
        PrefabUtility.SaveAsPrefabAsset(copy, path);
        UnityEngine.Object.DestroyImmediate(copy);
    }
    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    public static RectTransform Image(string name, Transform parent, Color color)
    {
        var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.sprite = Round; image.type = UnityEngine.UI.Image.Type.Sliced; image.raycastTarget = false; return rect;
    }
    public static Text Label(string name, Transform parent, string value, int size, Color color, float x, float y, float w, float h, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
    {
        var rect = Rect(name, parent); Top(rect, x, y, w, h); var text = rect.gameObject.AddComponent<Text>(); text.font = Font; text.text = value; text.fontSize = size; text.color = color; text.alignment = align; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.raycastTarget = false; text.verticalOverflow = VerticalWrapMode.Truncate; GameTypography.Apply(text); return text;
    }
    public static Button Button(string name, Transform parent, string value, int size, Color color, float x, float y, float w, float h)
    {
        var rect = Image(name, parent, color); Top(rect, x, y, w, h); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.image.raycastTarget = true;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors; colors.disabledColor = new Color(.55f, .6f, .55f, .65f); colors.pressedColor = new Color(.7f, .75f, .65f); colors.highlightedColor = new Color(1f, 1f, .9f); button.colors = colors;
        var label = Label("Label", rect, value, size, Cream, 12, 5, w - 24, h - 10, true, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(12, 5); label.rectTransform.offsetMax = new Vector2(-12, -5);
        return button;
    }
    public static void Top(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
    }
    public static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
}
