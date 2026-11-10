using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static VillageUIStyle;
using Object = UnityEngine.Object;

public static class ArtifactShopAuthoring
{
    public const string PrefabPath = "Assets/Resources/UI/ArtifactShop.prefab";
    private const string AssetRoot = "Assets/Resources/Artifacts/";
    [Serializable] private class CatalogueData { public ItemData[] Items; }
    [Serializable] private class ItemData
    {
        public string Id, Name, Description, Mark, Category;
        public int GoldCost;
        public bool IsAssembled;
        public ArtifactIngredient[] Ingredients;
        public ArtifactBonus[] Bonuses;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use the isolated project.");
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [MenuItem("Forest Animal War/UI/Добавить каталог артефактов в лавку")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play before editing assets.");
        Directory.CreateDirectory(AssetRoot + "Definitions");
        Directory.CreateDirectory("Assets/Resources/UI");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var data = JsonUtility.FromJson<CatalogueData>(File.ReadAllText("Tools/ArtifactCatalogue.json"));
        if (data.Items.Length != 60) throw new Exception("Expected 30 ordinary and 30 assembled artifacts.");
        Sprite[] basic = ImportIcons(AssetRoot + "base-icons.png", "Basic");
        Sprite[] crafted = ImportIcons(AssetRoot + "crafted-icons.png", "Craft");
        var catalogue = AssetDatabase.LoadAssetAtPath<MetaArtifactCatalog>(AssetRoot + "ArtifactCatalog.asset");
        if (catalogue == null)
        {
            catalogue = ScriptableObject.CreateInstance<MetaArtifactCatalog>();
            AssetDatabase.CreateAsset(catalogue, AssetRoot + "ArtifactCatalog.asset");
        }
        catalogue.Items = new MetaArtifactDefinition[data.Items.Length];
        for (int i = 0; i < data.Items.Length; i++)
        {
            ItemData source = data.Items[i];
            string path = AssetRoot + "Definitions/" + source.Id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<MetaArtifactDefinition>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<MetaArtifactDefinition>(); AssetDatabase.CreateAsset(asset, path); }
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), asset);
            asset.Icon = i < 30 ? basic[i] : crafted[i - 30];
            catalogue.Items[i] = asset;
            EditorUtility.SetDirty(asset);
        }
        EditorUtility.SetDirty(catalogue);
        AssetDatabase.SaveAssets();
        ArtifactEquipmentBonuses.Invalidate();
        BuildShopPrefab();
        BuildLightningPrefab();
        BakeSharedEffects();

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        var hub = Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null) throw new Exception("Menu hub missing.");
        ExpandHero(hub.tabs[1].page.transform);
        Transform page = hub.tabs[2].page.transform;
        Transform previous = page.Find("Artifact Shop");
        if (previous != null) Object.DestroyImmediate(previous.gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, page);
        instance.name = "Artifact Shop"; instance.SetActive(false);
        var offers = page.Find("Building Menu/Panel/Shop Offers");
        if (offers == null) throw new Exception("Shop building menu missing.");
        foreach (Transform child in offers.Cast<Transform>().ToArray())
            if (child.name.StartsWith("Offer ") || child.name == "Pool Header" || child.name == "Open Catalog")
                Object.DestroyImmediate(child.gameObject);
        ((RectTransform)offers).sizeDelta = new Vector2(916, 570);
        Button("Open Catalog", offers, "ОТКРЫТЬ ЛАВКУ  ·  60 ПРЕДМЕТОВ", 0, 108, 916, 86, null, 28);
        offers.Find("Title").GetComponent<Text>().text = "АРТЕФАКТЫ ДЛЯ ТЫКВЫ";
        offers.Find("Hint").GetComponent<Text>().text = "Покупай обычные предметы и собирай из них реликвии.";
        Top((RectTransform)offers.Find("Spin Result"), 0, 230, 916, 116);
        Top((RectTransform)offers.Find("Spin Button"), 0, 366, 916, 78);
        Label("Roulette Hint", offers, "РУЛЕТКА: случайный обычный артефакт, которого ещё нет.", 21, Muted, 0, 466, 916, 64);
        hub.PrepareVillageAndHero(); hub.CloseDialogs(); hub.SelectTab(0); hub.Refresh();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    // Update data without rebuilding an already authored shop or scene.
    [MenuItem("Forest Animal War/UI/Обновить свойства артефактов")]
    public static void BakeCatalogue()
    {
        if (EditorApplication.isPlaying) throw new Exception("Leave Play Mode before updating artifacts.");
        var data = JsonUtility.FromJson<CatalogueData>(File.ReadAllText("Tools/ArtifactCatalogue.json"));
        var catalogue = AssetDatabase.LoadAssetAtPath<MetaArtifactCatalog>(AssetRoot + "ArtifactCatalog.asset");
        if (catalogue == null) throw new Exception("Bake the complete catalogue first.");
        if (catalogue.Items.Length != data.Items.Length) throw new Exception("Unexpected catalogue length.");
        for (int i = 0; i < data.Items.Length; i++)
        {
            var asset = catalogue.Items[i];
            if (asset == null || asset.Id != data.Items[i].Id) throw new Exception("Artifact order changed.");
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data.Items[i]), asset);
            EditorUtility.SetDirty(asset);
        }
        BakeSharedEffects();
        AssetDatabase.SaveAssets();
        ArtifactEquipmentBonuses.Invalidate();
    }

    private static void BakeSharedEffects()
    {
        Directory.CreateDirectory("Assets/Resources/Effects/Artifacts");
        AssetDatabase.Refresh();
        CopyEffectPrefab("Assets/AnimalsBullet/BeaverDam.prefab", "Assets/Resources/Effects/Artifacts/DefenseDam.prefab");
        CopyEffectPrefab("Assets/AnimalsBullet/SquirrelAcorn.prefab", "Assets/Resources/Effects/Artifacts/Acorn.prefab");
    }

    private static void CopyEffectPrefab(string source, string destination)
    {
        var contents = PrefabUtility.LoadPrefabContents(source);
        try { PrefabUtility.SaveAsPrefabAsset(contents, destination); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static Sprite[] ImportIcons(string path, string prefix)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048; importer.spritePixelsPerUnit = 100;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        var entries = new SpriteMetaData[30];
        for (int i = 0; i < 30; i++)
        {
            int x0 = Mathf.RoundToInt((i % 6) * width / 6f), x1 = Mathf.RoundToInt((i % 6 + 1) * width / 6f);
            int y1 = height - Mathf.RoundToInt((i / 6) * height / 5f);
            int y0 = height - Mathf.RoundToInt((i / 6 + 1) * height / 5f);
            entries[i] = new SpriteMetaData { name = prefix + i.ToString("00"), alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f), rect = new Rect(x0, y0, x1 - x0, y1 - y0) };
        }
        importer.spritesheet = entries; importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
    }

    private static Image Icon(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = Rect(name, parent); Top(rect, x, y, w, h);
        var image = rect.gameObject.AddComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }
    private static ArtifactShopUI.Card Card(string name, Transform parent, float x, float y, bool component)
    {
        float w = component ? 916 : 284, h = component ? 112 : 232;
        Button button = Button(name, parent, "", x, y, w, h, null);
        button.image.color = Background;
        Object.DestroyImmediate(button.transform.Find("Label").gameObject);
        var icon = Icon("Icon", button.transform, component ? 12 : 84, component ? 10 : 10, component ? 92 : 116, component ? 92 : 116);
        var itemName = Label("Name", button.transform, "", component ? 27 : 24, Cream,
            component ? 124 : 12, component ? 8 : 132, component ? 650 : 260, component ? 43 : 48, true,
            component ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter);
        var price = Label("Price", button.transform, "", 21, Gold, component ? 124 : 10, component ? 54 : 184,
            component ? 740 : 264, 30, true, component ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter);
        var owned = Label("Owned", button.transform, "", component ? 19 : 18, Muted,
            component ? 124 : 10, component ? 85 : 212, component ? 740 : 264, component ? 24 : 20, false,
            component ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter);
        return new ArtifactShopUI.Card { Button = button, Icon = icon, Name = itemName, Price = price, Owned = owned };
    }

    private static void BuildShopPrefab()
    {
        var holder = new GameObject("Authoring", typeof(RectTransform));
        try
        {
            var shade = PanelRect("Artifact Shop", holder.transform, new Color(0.015f, 0.025f, 0.02f, 0.96f), 0, 0, 1080, 1920);
            Stretch(shade); shade.GetComponent<Image>().raycastTarget = true; shade.gameObject.SetActive(false);
            var ui = shade.gameObject.AddComponent<ArtifactShopUI>();
            var panel = PanelRect("Panel", shade, Panel, 0, 0, 980, 1420);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f); panel.anchoredPosition = Vector2.zero;
            ui.CloseButton = Button("Close", panel, "×", 884, 20, 64, 64, null, 38);
            Label("Eyebrow", panel, "ЛЕСНАЯ ЛАВКА", 21, Gold, 32, 20, 830, 32, true);
            ui.Heading = Label("Heading", panel, "Обычные артефакты", 44, Cream, 32, 66, 880, 70, true);
            ui.Wallet = Label("Wallet", panel, "ЗОЛОТО", 24, Gold, 32, 144, 916, 38, true);
            var browse = Rect("Browse", panel); Top(browse, 32, 210, 916, 1180); ui.Browse = browse.gameObject;
            ui.OrdinaryTab = Button("Ordinary", browse, "ОБЫЧНЫЕ  ·  30", 0, 0, 444, 64, null, 26);
            ui.AssembledTab = Button("Assembled", browse, "СБОРНЫЕ  ·  30", 472, 0, 444, 64, null, 26);
            var searchRect = PanelRect("Search", browse, Background, 0, 88, 916, 64);
            searchRect.GetComponent<Image>().raycastTarget = true;
            ui.Search = searchRect.gameObject.AddComponent<InputField>();
            ui.Search.textComponent = Label("Text", searchRect, "", 25, Cream, 18, 7, 880, 50);
            ui.Search.placeholder = Label("Placeholder", searchRect, "Поиск: название, зверь или эффект…", 25, Muted, 18, 7, 880, 50);
            ui.Search.targetGraphic = searchRect.GetComponent<Image>();
            var list = PanelRect("List", browse, Background, 0, 178, 916, 916); list.GetComponent<Image>().raycastTarget = true;
            var viewport = Rect("Viewport", list); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.sizeDelta = new Vector2(0, 2480);
            ui.List = list.gameObject.AddComponent<ScrollRect>(); ui.List.content = content; ui.List.viewport = viewport;
            ui.List.horizontal = false; ui.List.movementType = ScrollRect.MovementType.Clamped; ui.List.scrollSensitivity = 60;
            ui.Cards = new ArtifactShopUI.Card[30];
            for (int i = 0; i < 30; i++) ui.Cards[i] = Card("Item " + i.ToString("00"), content, i % 3 * 316, i / 3 * 248, false);
            Label("Help", browse, "Выбери предмет. Его бонусы включаются во вкладке «Артефакты».", 22, Muted, 0, 1110, 916, 68);

            var detail = Rect("Detail", panel); Top(detail, 32, 210, 916, 1130); ui.Detail = detail.gameObject;
            ui.BackButton = Button("Back", detail, "← НАЗАД", 0, 0, 240, 56, null, 24);
            ui.DetailIcon = Icon("Icon", detail, 8, 85, 174, 174);
            ui.DetailName = Label("Name", detail, "", 39, Gold, 218, 82, 688, 122, true);
            ui.DetailOwned = Label("Owned", detail, "", 24, Muted, 218, 224, 688, 36);
            ui.DetailDescription = Label("Description", detail, "", 28, Cream, 4, 292, 908, 240);
            ui.DetailCost = Label("Cost", detail, "", 24, Gold, 4, 546, 908, 70, true);
            ui.RecipeHeading = Label("Recipe Heading", detail, "", 21, Muted, 4, 632, 908, 38, true);
            ui.Components = new ArtifactShopUI.Card[3];
            for (int i = 0; i < 3; i++) ui.Components[i] = Card("Component " + i, detail, 0, 684 + i * 122, true);
            // Keep the action fixed under the component list.
            ui.AcquireButton = Button("Acquire", panel, "", 32, 1270, 916, 72, null, 28);
            ui.AcquireLabel = ui.AcquireButton.GetComponentInChildren<Text>();
            ui.DetailHint = Label("Hint", detail, "", 22, Muted, 0, 1060, 916, 76);
            ui.Feedback = Label("Feedback", panel, "", 22, Gold, 32, 1352, 916, 48, true, TextAnchor.MiddleCenter);
            // The buy button is part of Detail so browsing never shows an obsolete action.
            ui.AcquireButton.transform.SetParent(detail, false);
            Top((RectTransform)ui.AcquireButton.transform, 0, 980, 916, 72);
            for (int i = 0; i < 3; i++) Top((RectTransform)ui.Components[i].Button.transform, 0, 618 + i * 116, 916, 108);
            Top((RectTransform)ui.RecipeHeading.transform, 4, 574, 908, 38);
            Top((RectTransform)ui.DetailCost.transform, 4, 490, 908, 70);
            Top((RectTransform)ui.DetailDescription.transform, 4, 270, 908, 216);
            // All shop cards carry their own authored controls. Runtime only changes values.
            ui.Detail.SetActive(false);
            EditorUtility.SetDirty(ui);
            PrefabUtility.SaveAsPrefabAsset(shade.gameObject, PrefabPath);
        }
        finally { Object.DestroyImmediate(holder); }
    }

    private static void BuildLightningPrefab()
    {
        const string folder = "Assets/Resources/Effects/Artifacts";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Lightning.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, folder + "/Lightning.mat");
        }
        var root = new GameObject("Artifact Lightning", typeof(LineRenderer), typeof(ArtifactLightningEffect));
        try
        {
            var line = root.GetComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = 6;
            line.widthMultiplier = 0.055f; line.numCornerVertices = 2; line.numCapVertices = 2;
            line.sharedMaterial = material; line.sortingOrder = RoomDarkness.WorldOverlayOrder + 28;
            line.startColor = line.endColor = new Color(0.45f, 0.87f, 1f);
            var serialized = new SerializedObject(root.GetComponent<ArtifactLightningEffect>());
            serialized.FindProperty("beam").objectReferenceValue = line;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, folder + "/Lightning.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void ExpandHero(Transform page)
    {
        Transform content = page.Find("Hero Equipment Runtime/Viewport/Content");
        if (content == null) throw new Exception("Editable hero UI missing.");
        Transform template = content.Find("Backpack Cell 1");
        for (int i = 0; i < 60; i++)
        {
            string name = "Backpack Cell " + (i + 1);
            Transform cell = content.Find(name);
            if (cell == null) { cell = Object.Instantiate(template, content); cell.name = name; }
            var rect = (RectTransform)cell;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1); rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(-378 + i % 4 * 252, -1064 - i / 4 * 244); rect.sizeDelta = new Vector2(228, 228);
            if (cell.Find("Icon") == null) Icon("Icon", cell, 46, 12, 136, 110);
            if (cell.Find("Quantity") == null) Label("Quantity", cell, "", 21, Gold, 160, 8, 60, 30, true, TextAnchor.UpperRight);
        }
        for (int i = 0; i < MetaProgression.EquipmentSlotCount; i++)
        {
            var slot = content.Find("Equipment Slot " + (i + 1));
            if (slot.Find("Icon") == null) Icon("Icon", slot, 30, 4, 84, 68);
        }
        Transform panel = page.Find("Artifact Details/Panel");
        ((RectTransform)panel).sizeDelta = new Vector2(980, 760);
        Top((RectTransform)panel.Find("Name"), 36, 80, 872, 104);
        Top((RectTransform)panel.Find("Effect"), 36, 205, 908, 275);
        panel.Find("Effect").GetComponent<Text>().fontSize = 28;
        Top((RectTransform)panel.Find("Action Hint"), 36, 494, 908, 110);
        Top((RectTransform)panel.Find("Equip"), 36, 648, 908, 72);
        Top((RectTransform)panel.Find("Unequip"), 36, 648, 908, 72);
    }
}
