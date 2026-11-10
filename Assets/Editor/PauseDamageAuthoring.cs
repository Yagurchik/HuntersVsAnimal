using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds an editable asset in the editor. Gameplay only fills its saved rows.
public static class PauseDamageAuthoring
{
    public const string PrefabPath = "Assets/Resources/UI/PauseDamagePanel.prefab";
    public static readonly string[] IconPaths = {
        "Assets/MonsterSprite/ProjectileArt/firefly-v2-simple.png",
        "Assets/MonsterSprite/ProjectileArt/hedgehog-v2-simple.png",
        "Assets/MonsterSprite/ProjectileArt/rabbit-v2-simple.png",
        "Assets/MonsterSprite/ProjectileArt/HoneyBadger/honey-badger-v2-simple.png",
        "Assets/MonsterSprite/ProjectileArt/Boar/boar-projectile.png",
        "Assets/MonsterSprite/ProjectileArt/Beaver/beaver-projectile-topdown.png",
        "Assets/MonsterSprite/ProjectileArt/Frog/frog-ui.png",
        "Assets/MonsterSprite/ProjectileArt/Squirrel/squirrel.png"
    };
    private static readonly Color Gold = new Color(0.69f, 0.57f, 0.30f);
    private static readonly Color Cream = new Color(0.95f, 0.91f, 0.76f);
    private static readonly Color Muted = new Color(0.66f, 0.72f, 0.59f);
    private static readonly Color Forest = new Color(0.065f, 0.12f, 0.09f);

    [MenuItem("Forest Animal War/UI/Создать окно паузы с уроном")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play before editing UI.");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        var root = new GameObject("Pause Damage Panel", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(PauseDamageUI));
        try
        {
            root.layer = 5;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 400;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            root.GetComponent<Image>().color = new Color(0.015f, 0.028f, 0.025f, 0.94f);
            PauseDamageUI ui = root.GetComponent<PauseDamageUI>();
            var card = Rect("Pause Card", root.transform);
            card.sizeDelta = new Vector2(980, 1460);
            Paint(card, Gold);
            var body = Rect("Forest Panel", card); Stretch(body, 4, 4, 4, 4); Paint(body, Forest);
            var accent = Rect("Header Accent", body); Top(accent, 36, 40, -1, 4, 36); Paint(accent, Gold);
            Label("Pause Title", body, "ПАУЗА", 72, Cream, TextAnchor.MiddleCenter, 24, 52, -1, 96, 24);
            Label("Stats Title", body, "УРОН ЗА УРОВЕНЬ", 32, Muted, TextAnchor.MiddleCenter, 36, 160, -1, 48, 36);
            Label("Total Caption", body, "Всего нанесено", 26, Muted, TextAnchor.MiddleCenter, 36, 226, -1, 40, 36);
            Text total = Label("Total Damage", body, "0", 58, Cream, TextAnchor.MiddleCenter, 36, 264, -1, 76, 36);
            Text hint = Label("Damage Hint", body, "Нажми на снаряд — посмотреть улучшения", 21, Muted,
                TextAnchor.MiddleCenter, 36, 346, -1, 48, 36);
            var divider = Rect("Header Divider", body); Top(divider, 40, 398, -1, 2, 40); Paint(divider, Gold * new Color(1, 1, 1, 0.5f));

            var list = Rect("Damage List", body); Stretch(list, 36, 422, 36, 254);
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 60; scroll.inertia = true;
            var viewport = Rect("Viewport", list); Stretch(viewport, 0, 0, 18, 0);
            Paint(viewport, Color.clear).raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Rows", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 1158);
            scroll.viewport = viewport; scroll.content = content;
            var bar = Rect("Scroll Track", list); bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1, 0.5f); bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(8, 0);
            Paint(bar, new Color(0.15f, 0.22f, 0.16f));
            var slider = Rect("Sliding Area", bar); Stretch(slider, 0, 0, 0, 0);
            var handle = Rect("Handle", slider); Stretch(handle, 0, 0, 0, 0); Image handleImage = Paint(handle, Gold);
            handleImage.raycastTarget = true;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage; scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var rows = new PauseDamageUI.DamageRow[AnimalProjectileQueue.AnimalTypeCount];
            var icons = new Sprite[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(IconPaths[i]);
                if (icons[i] == null) throw new Exception("Missing pause icon: " + IconPaths[i]);
                var row = Rect("Damage Row " + (i + 1), content); Top(row, 0, i * 146, -1, 136, 0);
                Image rowImage = Paint(row, new Color(0.105f, 0.18f, 0.13f)); rowImage.raycastTarget = true;
                Button rowButton = row.gameObject.AddComponent<Button>(); rowButton.targetGraphic = rowImage;
                ColorBlock rowColors = rowButton.colors; rowColors.highlightedColor = new Color(1.18f, 1.18f, 1.08f);
                rowColors.pressedColor = new Color(0.8f, 0.8f, 0.7f); rowButton.colors = rowColors;
                rowButton.navigation = new Navigation { mode = Navigation.Mode.None };
                var iconRect = Rect("Animal Icon", row); Top(iconRect, 14, 30, 72, 72);
                Image icon = Paint(iconRect, Color.white); icon.sprite = icons[i]; icon.preserveAspect = true;
                Text name = Label("Animal Name", row, PauseDamageUI.AnimalNames[i], 34, Cream,
                    TextAnchor.MiddleLeft, 104, 16, -1, 52, 230);
                Text damage = Label("Damage Value", row, "0", 34, Cream,
                    TextAnchor.MiddleRight, 104, 16, -1, 52, 22);
                // Reserve the right side of the name for damage; damage has its own right-aligned box.
                damage.rectTransform.anchorMin = new Vector2(1, 1); damage.rectTransform.anchorMax = Vector2.one;
                damage.rectTransform.pivot = Vector2.one; damage.rectTransform.sizeDelta = new Vector2(220, 52);
                damage.rectTransform.anchoredPosition = new Vector2(-22, -16);
                var track = Rect("Damage Track", row); Top(track, 104, 84, -1, 22, 112);
                Paint(track, new Color(0.025f, 0.065f, 0.045f));
                var fillRect = Rect("Damage Fill", track); Stretch(fillRect, 0, 0, 0, 0);
                fillRect.anchorMax = new Vector2(0, 1);
                Image fill = Paint(fillRect, CombatDamagePalette.ColorFor(CombatDamagePalette.FromAnimalIndex(i)));
                Text percentage = Label("Share", row, "0%", 24, Muted, TextAnchor.MiddleRight, 104, 74, -1, 42, 22);
                rows[i] = new PauseDamageUI.DamageRow { root = row, icon = icon, name = name,
                    damage = damage, percentage = percentage, fill = fill, button = rowButton };
            }

            var footerDivider = Rect("Footer Divider", body); Bottom(footerDivider, 40, 232, -1, 2, 40);
            Paint(footerDivider, Gold * new Color(1, 1, 1, 0.5f));
            Button resume = ActionButton("Continue", body, "ПРОДОЛЖИТЬ", new Color(0.27f, 0.39f, 0.18f), 42, ui.Continue);
            Bottom(resume.GetComponent<RectTransform>(), 40, 120, -1, 92, 40);
            Button restart = ActionButton("Restart", body, "ЗАНОВО", new Color(0.16f, 0.23f, 0.16f), 32, ui.Restart);
            var restartRect = restart.GetComponent<RectTransform>(); Bottom(restartRect, 40, 30, -1, 72, 0);
            restartRect.anchorMax = new Vector2(0.5f, 0); restartRect.offsetMax = new Vector2(-10, 102);
            Button menu = ActionButton("Main Menu", body, "В МЕНЮ", new Color(0.16f, 0.23f, 0.16f), 32, ui.MainMenu);
            var menuRect = menu.GetComponent<RectTransform>(); Bottom(menuRect, 0, 30, -1, 72, 40);
            menuRect.anchorMin = new Vector2(0.5f, 0); menuRect.offsetMin = new Vector2(10, 30);

            PauseProjectileUpgradesUI projectileUpgrades = BuildProjectileUpgrades(root.transform);

            var data = new SerializedObject(ui);
            data.FindProperty("card").objectReferenceValue = card;
            data.FindProperty("content").objectReferenceValue = content;
            data.FindProperty("scroll").objectReferenceValue = scroll;
            data.FindProperty("totalDamage").objectReferenceValue = total;
            data.FindProperty("hint").objectReferenceValue = hint;
            data.FindProperty("projectileUpgrades").objectReferenceValue = projectileUpgrades;
            var savedIcons = data.FindProperty("fallbackIcons"); savedIcons.arraySize = icons.Length;
            var savedRows = data.FindProperty("rows"); savedRows.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                savedIcons.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
                var saved = savedRows.GetArrayElementAtIndex(i); var row = rows[i];
                saved.FindPropertyRelative("root").objectReferenceValue = row.root;
                saved.FindPropertyRelative("icon").objectReferenceValue = row.icon;
                saved.FindPropertyRelative("name").objectReferenceValue = row.name;
                saved.FindPropertyRelative("damage").objectReferenceValue = row.damage;
                saved.FindPropertyRelative("percentage").objectReferenceValue = row.percentage;
                saved.FindPropertyRelative("fill").objectReferenceValue = row.fill;
                saved.FindPropertyRelative("button").objectReferenceValue = row.button;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static PauseProjectileUpgradesUI BuildProjectileUpgrades(Transform parent)
    {
        var root = Rect("Projectile Upgrades", parent); Stretch(root, 0, 0, 0, 0);
        var ui = root.gameObject.AddComponent<PauseProjectileUpgradesUI>();
        var backdrop = Rect("Backdrop", root); Stretch(backdrop, 0, 0, 0, 0);
        Image shade = Paint(backdrop, new Color(0.015f, 0.028f, 0.025f, 0.9f)); shade.raycastTarget = true;
        var dismiss = backdrop.gameObject.AddComponent<Button>(); dismiss.targetGraphic = shade;
        dismiss.transition = Selectable.Transition.None; dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
        UnityEventTools.AddPersistentListener(dismiss.onClick, ui.Close);
        var card = Rect("Upgrades Card", root); card.sizeDelta = new Vector2(950, 1124);
        Paint(card, Gold).raycastTarget = true;
        var body = Rect("Forest Panel", card); Stretch(body, 4, 4, 4, 4); Paint(body, Forest).raycastTarget = true;
        Label("Title", body, "ВЫБРАННЫЕ УЛУЧШЕНИЯ", 30, Muted, TextAnchor.MiddleCenter, 32, 22, -1, 48, 32);
        var iconRect = Rect("Animal Icon", body); Top(iconRect, 36, 92, 104, 104);
        Image icon = Paint(iconRect, Color.white); icon.preserveAspect = true;
        Text name = Label("Animal Name", body, "Светлячок", 48, Cream, TextAnchor.MiddleLeft, 168, 92, -1, 64, 36);
        Text state = Label("State", body, "Снарядов за залп: 3", 25, Muted, TextAnchor.MiddleLeft, 168, 160, -1, 48, 36);
        var divider = Rect("Header Divider", body); Top(divider, 36, 226, -1, 2, 36); Paint(divider, Gold);

        var list = Rect("Upgrade List", body); Stretch(list, 36, 250, 36, 124);
        var scroll = list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60f;
        var viewport = Rect("Viewport", list); Stretch(viewport, 0, 0, 16, 0);
        Paint(viewport, Color.clear).raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Rows", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
        scroll.viewport = viewport; scroll.content = content;
        Text empty = Label("Empty", viewport, "Улучшения пока не выбраны", 28, Muted, TextAnchor.MiddleCenter, 16, 0, -1, 80, 16);
        Stretch(empty.rectTransform, 16, 0, 16, 0); empty.horizontalOverflow = HorizontalWrapMode.Wrap;
        var track = Rect("Scroll Track", list); track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1, 0.5f); track.sizeDelta = new Vector2(8, 0); Paint(track, new Color(0.15f, 0.22f, 0.16f));
        var slider = Rect("Sliding Area", track); Stretch(slider, 0, 0, 0, 0);
        var handle = Rect("Handle", slider); Stretch(handle, 0, 0, 0, 0); Image handleImage = Paint(handle, Gold);
        handleImage.raycastTarget = true;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage; scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        var rows = new PauseProjectileUpgradesUI.UpgradeRow[6];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = Rect("Upgrade Row " + (i + 1), content); Top(row, 0, i * 190, -1, 180, 0);
            Paint(row, new Color(0.105f, 0.18f, 0.13f));
            var accent = Rect("Accent", row); Top(accent, 0, 0, 4, 180); Paint(accent, Gold);
            Text title = Label("Name", row, "Улучшение", 32, Cream, TextAnchor.MiddleLeft, 24, 14, -1, 48, 150);
            Text rank = Label("Rank", row, "1 / 3", 25, Muted, TextAnchor.MiddleRight, 0, 14, 120, 48);
            rank.rectTransform.anchorMin = rank.rectTransform.anchorMax = Vector2.one;
            rank.rectTransform.pivot = Vector2.one; rank.rectTransform.anchoredPosition = new Vector2(-22, -14);
            Text description = Label("Description", row, "Эффект улучшения", 25, Cream, TextAnchor.UpperLeft, 24, 68, -1, 100, 24);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            rows[i] = new PauseProjectileUpgradesUI.UpgradeRow { root = row, title = title, description = description, rank = rank };
        }
        Button close = ActionButton("Close", body, "К СТАТИСТИКЕ", new Color(0.27f, 0.39f, 0.18f), 30, ui.Close);
        Bottom(close.GetComponent<RectTransform>(), 36, 24, -1, 72, 36);
        var data = new SerializedObject(ui);
        data.FindProperty("card").objectReferenceValue = card; data.FindProperty("icon").objectReferenceValue = icon;
        data.FindProperty("projectileName").objectReferenceValue = name; data.FindProperty("state").objectReferenceValue = state;
        data.FindProperty("empty").objectReferenceValue = empty; data.FindProperty("content").objectReferenceValue = content;
        data.FindProperty("scroll").objectReferenceValue = scroll; data.FindProperty("closeButton").objectReferenceValue = close;
        var savedRows = data.FindProperty("rows"); savedRows.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            var saved = savedRows.GetArrayElementAtIndex(i);
            saved.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
            saved.FindPropertyRelative("title").objectReferenceValue = rows[i].title;
            saved.FindPropertyRelative("description").objectReferenceValue = rows[i].description;
            saved.FindPropertyRelative("rank").objectReferenceValue = rows[i].rank;
        }
        data.ApplyModifiedPropertiesWithoutUndo(); root.gameObject.SetActive(false); return ui;
    }

    [MenuItem("Forest Animal War/UI/Подключить паузу в игровой сцене")]
    public static void Install()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != GameUIPreviewWindow.GameplayPath)
            throw new InvalidOperationException("Open the gameplay scene outside Play.");
        var transition = Object.FindObjectOfType<LevelTransition>(true);
        if (transition == null) throw new Exception("Gameplay LevelTransition is missing.");
        if (transition.PausePanel != null && transition.PausePanel.GetComponent<PauseDamageUI>() != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Bake(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
        Undo.RecordObject(transition, "Connect damage pause");
        if (transition.PausePanel != null) transition.PausePanel.SetActive(false);
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(panel, "Add damage pause");
        transition.PausePanel = panel; EditorUtility.SetDirty(transition);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var item = new GameObject(name, typeof(RectTransform)); item.layer = 5;
        item.transform.SetParent(parent, false); return item.GetComponent<RectTransform>();
    }
    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
    private static void Top(RectTransform rect, float left, float top, float width, float height, float right = 0)
    {
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(width < 0 ? 1 : 0, 1);
        rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width < 0 ? -left - right : width, height);
    }
    private static void Bottom(RectTransform rect, float left, float bottom, float width, float height, float right = 0)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(width < 0 ? 1 : 0, 0);
        rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(left, bottom);
        rect.sizeDelta = new Vector2(width < 0 ? -left - right : width, height);
    }
    private static Image Paint(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private static Text Label(string name, Transform parent, string value, int size, Color color,
        TextAnchor alignment, float left, float top, float width, float height, float right = 0)
    {
        var rect = Rect(name, parent); Top(rect, left, top, width, height, right);
        Text text = rect.gameObject.AddComponent<Text>(); text.font = GameTypography.Font;
        text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false; return text;
    }
    private static Button ActionButton(string name, Transform parent, string value, Color color, int size, UnityAction action)
    {
        var rect = Rect(name, parent); Image image = Paint(rect, color); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        ColorBlock colors = button.colors; colors.highlightedColor = new Color(1.18f, 1.18f, 1.08f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.7f); button.colors = colors;
        var label = Label("Label", rect, value, size, Cream, TextAnchor.MiddleCenter, 8, 0, -1, 72, 8);
        Stretch(label.rectTransform, 8, 0, 8, 0);
        UnityEventTools.AddPersistentListener(button.onClick, action); return button;
    }
}
