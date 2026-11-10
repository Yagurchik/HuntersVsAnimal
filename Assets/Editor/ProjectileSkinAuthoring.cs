using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static VillageUIStyle;
using Object = UnityEngine.Object;

public static class ProjectileSkinAuthoring
{
    public static readonly string[] PrefabNames = { "BaseBullet", "hedgehog", "Rabbit", "HoneyBadger", "Boar", "Beaver", "Frog", "Squirrel" };
    public static readonly string[] ImageNames = { "firefly-aurora", "hedgehog-sunburst", "rabbit-candy", "honey-badger-ultraviolet", "boar-scarlet", "beaver-lagoon", "frog-tropical", "squirrel-sunset" };
    private static readonly string[] Names = { "Аврора", "Солнечный", "Карамельный", "Ультрафиолет", "Алый гром", "Лагуна", "Тропический", "Закатная" };
    private const string CataloguePath = "Assets/Resources/ProjectileSkins/Catalogue.asset";
    private const string ShopPath = "Assets/Resources/UI/ProjectileSkinShop.prefab";

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use an isolated project copy.");
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [MenuItem("Forest Animal War/UI/Добавить магазин скинов снарядов")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Leave Play Mode first.");
        Directory.CreateDirectory("Assets/Resources/ProjectileSkins");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var catalogue = AssetDatabase.LoadAssetAtPath<ProjectileSkinCatalog>(CataloguePath);
        if (catalogue == null)
        {
            catalogue = ScriptableObject.CreateInstance<ProjectileSkinCatalog>();
            AssetDatabase.CreateAsset(catalogue, CataloguePath);
        }
        catalogue.Skins = new ProjectileSkinCatalog.Skin[8];
        for (int i = 0; i < 8; i++)
        {
            string path = "Assets/AnimalsBullet/" + PrefabNames[i] + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new Exception("Missing projectile: " + path);
            Sprite original = prefab.GetComponent<SpriteRenderer>().sprite;
            string imagePath = "Assets/Art/ProjectileSkins/" + ImageNames[i] + ".png";
            var importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
            if (importer == null) throw new Exception("Missing skin: " + imagePath);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(original.pivot.x / original.rect.width,
                original.pivot.y / original.rect.height);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = original.pixelsPerUnit * width / original.rect.width;
            importer.SaveAndReimport();
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = contents.GetComponent<ProjectileSkinVisual>();
                if (visual == null) visual = contents.AddComponent<ProjectileSkinVisual>();
                var serialized = new SerializedObject(visual);
                serialized.FindProperty("projectileIndex").intValue = i;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            catalogue.Skins[i] = new ProjectileSkinCatalog.Skin {
                Id = ImageNames[i], Name = Names[i], ProjectileName = PauseDamageUI.AnimalNames[i],
                DiamondPrice = 30, Artwork = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath),
                OriginalIcon = original, ProjectilePrefab = prefab
            };
        }
        EditorUtility.SetDirty(catalogue);
        AssetDatabase.SaveAssets();
        BuildShop();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        var hub = Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null) throw new Exception("Menu hub is missing.");
        InstallIntoMenu(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    // Also used to update a scene that was already open when its asset changed on disk.
    // Never reload that scene: it may contain the user's unsaved edits.
    public static bool InstallIntoMenu(MenuHubController hub)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Author the shop outside Play Mode.");
        if (hub.tabs == null || hub.tabs.Length < 3 || hub.tabs[2].page == null)
            throw new Exception("Authored village page is missing.");
        Transform page = hub.tabs[2].page.transform;
        Transform field = page.Find("Forest Village Runtime/Viewport/Content/Village Field");
        if (field == null) throw new Exception("Authored village field is missing.");
        // A stale open scene must migrate all services together. Installing
        // only the chameleon leaves the old clearing and houses underneath.
        bool changed = VillageEntityWindowAuthoring.SynchronizeSceneWindows(hub);
        if (page.Find("Projectile Skin Shop") == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPath);
            if (prefab == null) throw new Exception("Authored skin shop prefab is missing.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, page);
            instance.name = "Projectile Skin Shop";
            changed = true;
        }
        changed |= VillageEntityWindowAuthoring.EnsureSkinWindow(field);
        if (changed) hub.RebindVillageForAuthoring();
        return changed;
    }

    private static void BuildShop()
    {
        var holder = new GameObject("Skin shop authoring", typeof(RectTransform));
        try
        {
            var panel = Modal(holder.transform, "Projectile Skin Shop", 1450, null, out GameObject overlay);
            var ui = overlay.AddComponent<ProjectileSkinShopUI>();
            ui.CloseButton = panel.Find("Close").GetComponent<Button>();
            Label("Eyebrow", panel, "МАСТЕРСКАЯ ОБЛИКОВ", 21, Gold, 32, 22, 800, 30, true);
            Label("Title", panel, "Яркие снаряды", 44, Cream, 32, 65, 860, 66, true);
            ui.Wallet = Label("Wallet", panel, "БРИЛЛИАНТЫ", 27, new Color(0.43f, 0.9f, 1f), 32, 146, 916, 42, true);
            Label("Hint", panel, "Новый цвет для любимого зверя. Характеристики сохраняются.", 23, Muted, 32, 202, 916, 60);
            var list = Rect("List", panel); Top(list, 32, 282, 916, 1068);
            list.gameObject.AddComponent<Image>().color = Background;
            var viewport = Rect("Viewport", list); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.sizeDelta = new Vector2(0, 1676);
            ui.List = list.gameObject.AddComponent<ScrollRect>();
            ui.List.viewport = viewport; ui.List.content = content;
            ui.List.horizontal = false; ui.List.movementType = ScrollRect.MovementType.Clamped;
            ui.List.scrollSensitivity = 55;
            ui.Cards = new ProjectileSkinShopUI.Card[8];
            for (int i = 0; i < 8; i++)
            {
                var card = PanelRect("Skin " + ImageNames[i], content, new Color(0.13f, 0.15f, 0.22f),
                    8 + i % 2 * 454, 8 + i / 2 * 414, 438, 402);
                var previewRect = Rect("Preview", card); Top(previewRect, 104, 7, 230, 208);
                var preview = previewRect.gameObject.AddComponent<Image>();
                preview.preserveAspect = true; preview.raycastTarget = false;
                preview.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ProjectileSkins/" + ImageNames[i] + ".png");
                var projectile = Label("Projectile", card, PauseDamageUI.AnimalNames[i], 25, Cream, 14, 209, 410, 36, true, TextAnchor.MiddleCenter);
                var name = Label("Name", card, Names[i], 26, new Color(0.84f, 0.65f, 1f), 14, 246, 410, 38, true, TextAnchor.MiddleCenter);
                var status = Label("Status", card, "", 19, Muted, 14, 285, 410, 28, false, TextAnchor.MiddleCenter);
                var action = Button("Acquire", card, "", 14, 319, 410, 43, null, 22);
                action.image.color = new Color(0.42f, 0.22f, 0.59f);
                var original = Button("Original", card, "ОБЫЧНЫЙ ОБЛИК", 14, 367, 410, 30, null, 17);
                ui.Cards[i] = new ProjectileSkinShopUI.Card { Preview = preview, ProjectileName = projectile,
                    SkinName = name, Status = status, Action = action, Original = original,
                    ActionLabel = action.GetComponentInChildren<Text>() };
            }
            ui.Feedback = Label("Feedback", panel, "Купленный облик остаётся навсегда.", 22, Gold, 32, 1370, 916, 56, false, TextAnchor.MiddleCenter);
            PrefabUtility.SaveAsPrefabAsset(overlay, ShopPath);
        }
        finally { Object.DestroyImmediate(holder); }
    }
}
