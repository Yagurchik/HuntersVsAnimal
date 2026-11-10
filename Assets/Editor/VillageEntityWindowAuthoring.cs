using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static VillageUIStyle;
using Object = UnityEngine.Object;

// Explicit authoring only. The saved scene owns the four character windows.
public static class VillageEntityWindowAuthoring
{
    public const string MenuPath = "Assets/Scenes/UIscene.unity";
    public const string ArtFolder = "Assets/Art/VillageEntities";
    public const string PrefabFolder = "Assets/Resources/Village";
    private const float Width = 992f;
    private static readonly Color FrameColor = Hex("392A1D");
    private static readonly Color InnerColor = Hex("1B2D22");
    private static readonly Color PortraitColor = Hex("405039");

    public static readonly string[] InstanceNames = {
        "GreatTreeBuilding", "Open Skin Shop", "ForestShopBuilding", "Open Bestiary"
    };
    public static readonly string[] PrefabNames = {
        "GreatTreeBuilding", "ChameleonWindow", "ForestShopBuilding", "OwlWindow"
    };
    public static readonly string[] SpriteNames = {
        "eternal-tree-v1", "chameleon-merchant-v1", "raven-merchant-v1", "owl-keeper-v1"
    };

    public static float WindowY(int index) => index == 0 ? 0f
        : ForestVillageUI.TreeWindowHeight + ForestVillageUI.EntityWindowGap
            + (index - 1) * (ForestVillageUI.MerchantWindowHeight + ForestVillageUI.EntityWindowGap);

    public static void EnsurePrefabs()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Author entity windows outside Play mode.");
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        for (int i = 0; i < SpriteNames.Length; i++)
        {
            string path = ArtFolder + "/" + SpriteNames[i] + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing entity portrait: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            BuildWindow(i, AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }
        AssetDatabase.SaveAssets();
    }

    private static void BuildWindow(int index, Sprite portrait)
    {
        string path = PrefabFolder + "/" + PrefabNames[index] + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(path)
            : new GameObject(PrefabNames[index], typeof(RectTransform));
        try
        {
            root.name = PrefabNames[index];
            root.layer = 5;
            for (int child = root.transform.childCount - 1; child >= 0; child--)
                Object.DestroyImmediate(root.transform.GetChild(child).gameObject);
            bool tree = index == 0;
            float height = tree ? ForestVillageUI.TreeWindowHeight : ForestVillageUI.MerchantWindowHeight;
            RectTransform rect = (RectTransform)root.transform;
            Top(rect, 0f, WindowY(index), Width, height);
            Image frame = root.GetComponent<Image>();
            if (frame == null) frame = root.AddComponent<Image>();
            frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/MenuStyle/WoodcutPanel.png");
            frame.type = Image.Type.Sliced;
            frame.color = FrameColor;
            frame.raycastTarget = true;
            Button open = root.GetComponent<Button>();
            if (open == null) open = root.AddComponent<Button>();
            open.targetGraphic = frame;
            open.navigation = new Navigation { mode = Navigation.Mode.None };
            open.onClick = new Button.ButtonClickedEvent();
            ColorBlock colors = open.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.10f, 1.04f);
            colors.pressedColor = new Color(0.82f, 0.87f, 0.80f);
            colors.fadeDuration = 0.10f;
            open.colors = colors;

            RectTransform inset = PanelRect("Window Surface", rect, InnerColor,
                7f, 7f, Width - 14f, height - 14f);
            Image insetImage = inset.GetComponent<Image>();
            insetImage.sprite = frame.sprite;
            insetImage.type = Image.Type.Sliced;
            PanelRect("Portrait Backing", rect, PortraitColor,
                15f, 15f, tree ? Width - 30f : 278f, tree ? 338f : height - 30f);
            RectTransform artRect = Rect("Character", rect);
            Top(artRect, tree ? 18f : 23f, tree ? 5f : 16f,
                tree ? Width - 36f : 262f, tree ? 350f : height - 32f);
            // Image.preserveAspect aligns inside its rect using the pivot.
            // Keep the same rectangle while centering every portrait in it.
            artRect.pivot = new Vector2(0.5f, 0.5f);
            artRect.anchoredPosition += new Vector2(artRect.sizeDelta.x * 0.5f, -artRect.sizeDelta.y * 0.5f);
            Image art = artRect.gameObject.AddComponent<Image>();
            art.sprite = portrait;
            art.preserveAspect = true;
            art.raycastTarget = false;

            string[] roles = { "", "ХАМЕЛЕОН", "ВОРОН", "СОВА" };
            string[] titles = { "ВЕЧНОЕ ДРЕВО", "Скины снарядов", "ЛЕСНАЯ ЛАВКА", "Бестиарий" };
            string[] descriptions = { "Сбережения всей деревни", "Новый облик для твоих зверей",
                "Артефакты и доход золотом", "Лес, противники и его защитники" };
            string[] actions = { "ОТКРЫТЬ", "ВЫБРАТЬ ОБЛИК", "ОТКРЫТЬ ЛАВКУ", "ОТКРЫТЬ КНИГУ" };
            float textX = tree ? 32f : 330f;
            float textWidth = tree ? 660f : 626f;
            if (!tree) Label("Entity Name", rect, roles[index], 22, Gold, textX, 25f, textWidth, 34f, true);
            Text title = Label("Title", rect, titles[index], tree ? 43 : 40, Cream,
                textX, tree ? 365f : 64f, textWidth, tree ? 48f : 60f, true);
            Label("Description", rect, descriptions[index], tree ? 22 : 24, Muted,
                textX, tree ? 416f : 136f, textWidth, tree ? 32f : 48f);
            RectTransform action = PanelRect("Action", rect, Green,
                tree ? 726f : textX, tree ? 382f : 209f, tree ? 232f : textWidth, 61f);
            Image actionImage = action.GetComponent<Image>();
            actionImage.sprite = frame.sprite;
            actionImage.type = Image.Type.Sliced;
            Text actionText = Label("Label", action, actions[index], 25, Cream,
                8f, 6f, action.sizeDelta.x - 16f, 49f, true, TextAnchor.MiddleCenter);
            if (index == 0 || index == 2)
            {
                VillageBuildingView view = root.GetComponent<VillageBuildingView>();
                if (view == null) view = root.AddComponent<VillageBuildingView>();
                view.kind = tree ? VillageBuildingKind.GreatTree : VillageBuildingKind.ForestShop;
                view.artwork = art;
                view.openButton = open;
                view.nameLabel = title;
                view.levelLabel = actionText;
                view.Refresh();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (existing != null) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static bool EnsureWindow(Transform field, int index)
    {
        string path = PrefabFolder + "/" + PrefabNames[index] + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { EnsurePrefabs(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); }
        Transform current = field.Find(InstanceNames[index]);
        bool changed = false;
        if (current != null && (current.Find("Character") == null
            || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject) != path))
        {
            Object.DestroyImmediate(current.gameObject);
            current = null;
            changed = true;
        }
        if (current == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, field);
            instance.name = InstanceNames[index];
            current = instance.transform;
            changed = true;
        }
        RectTransform rect = (RectTransform)current;
        float height = index == 0 ? ForestVillageUI.TreeWindowHeight : ForestVillageUI.MerchantWindowHeight;
        changed |= rect.sizeDelta != new Vector2(Width, height)
            || rect.anchoredPosition != new Vector2(0f, -WindowY(index));
        Top(rect, 0f, WindowY(index), Width, height);
        current.SetSiblingIndex(index);
        return changed;
    }

    public static bool EnsureSkinWindow(Transform field) => EnsureWindow(field, 1);
    public static bool EnsureBestiaryWindow(Transform field) => EnsureWindow(field, 3);

    public static bool HasCurrentLayout(MenuHubController hub)
    {
        if (hub == null || hub.tabs == null || hub.tabs.Length < 3 || hub.tabs[2].page == null) return false;
        Transform page = hub.tabs[2].page.transform;
        if (page.Cast<Transform>().Count(child => child.name == "Forest Village Runtime") != 1) return false;
        Transform field = page.Find("Forest Village Runtime/Viewport/Content/Village Field");
        VillageEntityLayout state = field != null ? field.GetComponent<VillageEntityLayout>() : null;
        if (state == null || state.Version != VillageEntityLayout.CurrentVersion || field.childCount != 4) return false;
        if (field.GetComponentsInChildren<RawImage>(true).Length != 0) return false;
        foreach (string name in InstanceNames)
        {
            Transform window = field.Find(name);
            Image character = window != null ? window.Find("Character")?.GetComponent<Image>() : null;
            if (window == null || field.Cast<Transform>().Count(child => child.name == name) != 1
                || window.GetComponent<Button>() == null || window.GetComponent<Image>() == null
                || character == null || character.sprite == null) return false;
        }
        return true;
    }

    public static bool SynchronizeSceneWindows(MenuHubController hub)
    {
        if (HasCurrentLayout(hub)) return false;
        if (Application.isPlaying) throw new InvalidOperationException("Migrate village scenes outside Play mode.");
        Undo.RegisterFullObjectHierarchyUndo(hub.tabs[2].page, "Migrate complete village layout");
        InstallIntoMenu(hub);
        return true;
    }

    public static void InstallIntoMenu(MenuHubController hub)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Author the village outside Play mode.");
        if (hub == null || hub.tabs == null || hub.tabs.Length < 3) throw new InvalidOperationException("Menu hub is missing.");
        // Automatic scene sync uses the authored assets as they are. Rebuild
        // portraits/prefabs only on an explicit bake or when assets are absent.
        if (PrefabNames.Any(name => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + name + ".prefab") == null))
            EnsurePrefabs();
        Transform page = hub.tabs[2].page.transform;
        Transform runtime = page.Find("Forest Village Runtime");
        foreach (Transform duplicate in page.Cast<Transform>()
            .Where(child => child.name == "Forest Village Runtime" && child != runtime).ToArray())
            Object.DestroyImmediate(duplicate.gameObject);
        Transform content = page.Find("Forest Village Runtime/Viewport/Content");
        if (content == null) throw new InvalidOperationException("The authored village page is missing.");
        Transform field = content.Find("Village Field");
        if (field == null) throw new InvalidOperationException("The authored village field is missing.");
        foreach (Transform child in field.Cast<Transform>().ToArray())
            if (!InstanceNames.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
        foreach (string name in InstanceNames)
        {
            Transform[] matches = field.Cast<Transform>().Where(child => child.name == name).ToArray();
            Transform keep = matches.FirstOrDefault(child => child.Find("Character") != null
                && child.GetComponent<Button>() != null && child.GetComponent<Image>() != null)
                ?? matches.FirstOrDefault();
            foreach (Transform duplicate in matches)
                if (duplicate != keep) Object.DestroyImmediate(duplicate.gameObject);
        }
        Top((RectTransform)field, 44f, 0f, Width, ForestVillageUI.EntityFieldHeight);
        for (int index = 0; index < 4; index++) EnsureWindow(field, index);
        foreach (string name in new[] { "Eyebrow", "Title" })
            if (content.Find(name) != null) Object.DestroyImmediate(content.Find(name).gameObject);
        Transform wallet = content.Find("Resources");
        if (wallet != null) wallet.gameObject.SetActive(false);
        RectTransform collection = content.Find("Collection") as RectTransform;
        if (collection != null)
        {
            Top(collection, 44f, ForestVillageUI.EntityFieldHeight + 18f, Width, 64f);
            Text label = collection.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 23;
        }
        ((RectTransform)content).sizeDelta = new Vector2(0f, ForestVillageUI.EntityContentHeight);
        ((RectTransform)content).anchoredPosition = Vector2.zero;
        ScrollRect scroll = page.Find("Forest Village Runtime").GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = false;
        scroll.velocity = Vector2.zero;
        VillageEntityLayout state = field.GetComponent<VillageEntityLayout>();
        if (state == null) state = field.gameObject.AddComponent<VillageEntityLayout>();
        state.RecordCurrentVersion();
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(scroll);
        EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
    }

    [MenuItem("Forest Animal War/UI/Сохранить окна жителей деревни")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before baking the village.");
        if (EditorSceneManager.GetActiveScene().path != MenuPath) EditorSceneManager.OpenScene(MenuPath);
        EnsurePrefabs();
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        InstallIntoMenu(hub);
        hub.RebindVillageForAuthoring();
        hub.CloseDialogs();
        hub.SelectTab(hub.initialTab);
        EditorSceneManager.SaveScene(hub.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = hub.tabs[2].page;
    }

    private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }
}
