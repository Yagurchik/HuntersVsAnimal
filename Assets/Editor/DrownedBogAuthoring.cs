using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class DrownedBogAuthoring
{
    public const string KingArt = "Assets/MonsterSprite/DrownedBog/DrownedKing.png";
    public const string LandscapeArt = "Assets/LocationArt/DrownedBog/DrownedBogMenu.png";
    public const string KingPrefab = "Assets/Resources/Enemies/DrownedKing.prefab";
    public const string HeirPrefab = "Assets/Resources/Enemies/DrownedHeir.prefab";

    [MenuItem("Tools/Forest Animal War/Locations/Bake Drowned Bog")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before baking Drowned Bog.");
        AssetDatabase.Refresh();
        PrepareSprite(KingArt, 384f);
        PrepareSprite(LandscapeArt, 256f);
        Sprite head = AssetDatabase.LoadAssetAtPath<Sprite>(KingArt);
        Sprite landscape = AssetDatabase.LoadAssetAtPath<Sprite>(LandscapeArt);
        Texture2D menuTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LandscapeArt);
        if (head == null || landscape == null || menuTexture == null)
            throw new InvalidOperationException("Drowned Bog artwork did not import.");

        CreateBossPrefab(HeirPrefab, "DrownedHeir", head, new Vector2Int(2, 2),
            1387.5f, 35, false, null);
        GameObject heir = AssetDatabase.LoadAssetAtPath<GameObject>(HeirPrefab);
        CreateBossPrefab(KingPrefab, "DrownedKing", head, new Vector2Int(3, 3),
            5550f, 100, true, heir);
        BakeMenu(menuTexture);
        BakeGameplay(landscape);
        AssetDatabase.SaveAssets();
        Debug.Log("Drowned Bog baked into editable scenes and prefabs: five levels, visual fog, king and three heirs.");
    }

    public static void BakeBatch()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void PrepareSprite(string path, float pixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing artwork: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void CreateBossPrefab(string path, string name, Sprite sprite,
        Vector2Int footprint, float health, int experience, bool king, GameObject heir)
    {
        GameObject ordinary = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Enemy/Brick.prefab");
        if (ordinary == null) throw new InvalidOperationException("Base enemy prefab is missing.");
        GameObject root = new GameObject(name);
        root.SetActive(false);
        try
        {
            root.tag = "Enemy";
            root.layer = ordinary.layer;
            GameObject visual = new GameObject("Visual");
            visual.layer = root.layer;
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            SpriteRenderer templateRenderer = ordinary.GetComponentInChildren<SpriteRenderer>(true);
            renderer.sprite = sprite;
            renderer.sharedMaterial = templateRenderer.sharedMaterial;
            renderer.sortingLayerID = templateRenderer.sortingLayerID;
            renderer.sortingOrder = templateRenderer.sortingOrder;
            // Fit the visible alpha silhouette over the entire logical footprint.
            const float visibleWidthFraction = 1197f / 1254f;
            const float visibleHeightFraction = 1239f / 1254f;
            float targetSize = footprint.x * 1.04f;
            float scale = Mathf.Max(
                targetSize / (sprite.bounds.size.x * visibleWidthFraction),
                targetSize / (sprite.bounds.size.y * visibleHeightFraction));
            visual.transform.localScale = Vector3.one * scale;
            GhostVisualAnimator animation = visual.AddComponent<GhostVisualAnimator>();
            SerializedObject animationData = new SerializedObject(animation);
            animationData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            animationData.ApplyModifiedPropertiesWithoutUndo();

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            PolygonCollider2D collider = root.AddComponent<PolygonCollider2D>();
            collider.SetPath(0, BossOctagonGeometry.CreatePoints(
                new Vector2(footprint.x - 0.2f, footprint.y - 0.2f)));
            Collider2D ordinaryCollider = ordinary.GetComponent<Collider2D>();
            if (ordinaryCollider != null)
                collider.sharedMaterial = ordinaryCollider.sharedMaterial;

            Brick brick = root.AddComponent<Brick>();
            Brick ordinaryBrick = ordinary.GetComponent<Brick>();
            brick.hp = health;
            brick.dmg = ordinaryBrick.dmg;
            SerializedObject brickData = new SerializedObject(brick);
            brickData.FindProperty("footprint").vector2IntValue = footprint;
            brickData.FindProperty("bossHealth").boolValue = true;
            brickData.FindProperty("bossHealthFraction").floatValue = name == "DrownedHeir" ? 0.25f : 1f;
            brickData.FindProperty("experienceReward").intValue = experience;
            brickData.FindProperty("stepJumpHeight").floatValue = 0f;
            brickData.FindProperty("stepStretch").floatValue = 0f;
            brickData.FindProperty("landingSquash").floatValue = 0f;
            brickData.ApplyModifiedPropertiesWithoutUndo();

            EnemyHitReaction hit = root.GetComponent<EnemyHitReaction>();
            SerializedObject hitData = new SerializedObject(hit);
            hitData.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            hitData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            hitData.FindProperty("damageSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/MonsterSprite/DrownedBog/DrownedKingDamage.png");
            hitData.ApplyModifiedPropertiesWithoutUndo();

            GhostPhasing phase = root.AddComponent<GhostPhasing>();
            SerializedObject phaseData = new SerializedObject(phase);
            phaseData.FindProperty("bodyCollider").objectReferenceValue = collider;
            phaseData.FindProperty("visual").objectReferenceValue = animation;
            phaseData.FindProperty("passThroughChance").floatValue = 0.2f;
            phaseData.ApplyModifiedPropertiesWithoutUndo();

            if (king)
            {
                DrownedKingSplit split = root.AddComponent<DrownedKingSplit>();
                SerializedObject splitData = new SerializedObject(split);
                splitData.FindProperty("heirPrefab").objectReferenceValue = heir;
                splitData.ApplyModifiedPropertiesWithoutUndo();
            }
            EnemyFirstEncounterProfile profile = root.AddComponent<EnemyFirstEncounterProfile>();
            profile.Configure(king ? "drowned_king" : "drowned_heir",
                king ? "Царь утопленников" : "Осколок утопленника",
                king
                    ? "Пропускает 20% снарядов. После гибели распадается на три головы-осколка."
                    : "Сохраняет 20% шанс пропустить снаряд, но больше не делится.",
                sprite, new Color(0.64f, 0.79f, 0.65f));
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void BakeMenu(Texture2D landscape)
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity", OpenSceneMode.Single);
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null || hub.tabs == null || hub.tabs.Length == 0)
            throw new InvalidOperationException("Campaign menu controller is missing.");
        Transform hero = hub.tabs[0].page.transform.Find("Viewport/Content/Forest Hero");
        Transform forest = hero != null ? hero.Find("Forest Illustration") : null;
        if (forest == null) throw new InvalidOperationException("Forest illustration is missing.");
        Transform bog = hero.Find("Drowned Bog Illustration");
        if (bog == null)
        {
            GameObject image = new GameObject("Drowned Bog Illustration", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            bog = image.transform;
            bog.SetParent(hero, false);
        }
        RectTransform rect = (RectTransform)bog;
        RectTransform source = (RectTransform)forest;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta;
        rect.SetSiblingIndex(forest.GetSiblingIndex() + 1);
        RawImage raw = bog.GetComponent<RawImage>();
        raw.texture = landscape;
        raw.color = Color.white;
        raw.uvRect = new Rect(0f, 0f, 1f, 1f);
        raw.raycastTarget = false;
        hub.forestIllustration = forest.gameObject;
        hub.drownedBogIllustration = bog.gameObject;
        bog.gameObject.SetActive(false);
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BakeGameplay(Sprite landscape)
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Asset.unity", OpenSceneMode.Single);
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        if (progression == null) throw new InvalidOperationException("Gameplay progression is missing.");
        progression.SendMessage("NormalizeLevelData", SendMessageOptions.RequireReceiver);
        EditorUtility.SetDirty(progression);
        foreach (Stage stage in Object.FindObjectsOfType<Stage>(true))
        {
            DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
            if (visuals == null) visuals = stage.gameObject.AddComponent<DrownedBogVisuals>();
            Transform fog = stage.transform.Find("Drowned Bog Visual Fog");
            if (fog == null)
            {
                GameObject fogObject = new GameObject("Drowned Bog Visual Fog");
                fog = fogObject.transform;
                fog.SetParent(stage.transform, false);
            }
            SpriteRenderer renderer = fog.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = fog.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = landscape;
            renderer.sortingOrder = 20;
            renderer.color = new Color(0.75f, 0.82f, 0.7f, 0.14f);
            Vector2 size = stage.BattlefieldSize;
            fog.localPosition = new Vector3(0f, 0f, 0f);
            fog.localScale = new Vector3(size.x / landscape.bounds.size.x,
                size.y / landscape.bounds.size.y, 1f);
            SerializedObject visualData = new SerializedObject(visuals);
            visualData.FindProperty("fogArtwork").objectReferenceValue = renderer;
            visualData.ApplyModifiedPropertiesWithoutUndo();
            fog.gameObject.SetActive(false);
            EditorUtility.SetDirty(visuals);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
