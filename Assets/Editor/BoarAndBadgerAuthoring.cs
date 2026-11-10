using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BoarAndBadgerAuthoring
{
    public const string GameplayScene = "Assets/Asset.unity";
    public const string MenuScene = "Assets/Scenes/UIscene.unity";
    public const string BadgerPrefabPath = "Assets/AnimalsBullet/HoneyBadger.prefab";
    public const string BoarPrefabPath = "Assets/AnimalsBullet/Boar.prefab";
    public const string BoarArtPath = "Assets/MonsterSprite/ProjectileArt/Boar/boar-projectile.png";
    public const string BoarProjectileArtPath = "Assets/MonsterSprite/ProjectileArt/Boar/boar-projectile-topdown.png";
    public const string BoarDustArtPath = "Assets/MonsterSprite/ProjectileArt/Boar/boar-dust-puff.png";
    public const string BoarMenuPath = "Assets/UI/MenuData/Boar.asset";
    public const string BadgerMenuPath = "Assets/UI/MenuData/HoneyBadger.asset";

    [MenuItem("Tools/Forest Animal War/Projectiles/Install Boar And Rework Honey Badger")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode first.");
        ConfigureSprite();
        GameObject badger = ConfigureBadger();
        GameObject boar = BuildBoar(badger);

        EditorSceneManager.OpenScene(GameplayScene);
        AnimalProjectileQueue queue = GameUIPreviewWindow.Find<AnimalProjectileQueue>();
        ExperienceAndUpgradeUI upgrades = GameUIPreviewWindow.Find<ExperienceAndUpgradeUI>();
        if (queue == null || upgrades == null) throw new InvalidOperationException("Gameplay queue/UI missing.");
        ConfigureQueue(queue, badger, boar);
        ConfigureUpgradePortrait(upgrades);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();
        Debug.Log("Boar slot and chained Honey Badger installed.");
    }

    public static void InstallBatch()
    {
        try { Install(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static GameObject ConfigureBadger()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BadgerPrefabPath);
        try
        {
            BoarProjectile wrong = root.GetComponent<BoarProjectile>();
            if (wrong != null) Object.DestroyImmediate(wrong);
            HoneyBadgerProjectile badger = root.GetComponent<HoneyBadgerProjectile>();
            if (badger == null) badger = root.AddComponent<HoneyBadgerProjectile>();
            var data = new SerializedObject(badger);
            data.FindProperty("maximumAttacks").intValue = 5;
            data.FindProperty("maximumConsecutiveAttacksPerTarget").intValue = 1;
            data.FindProperty("attacksAddedPerKill").intValue = 3;
            data.FindProperty("chainJumpRadius").floatValue = 1.2f;
            data.FindProperty("damageIncreasePerKill").floatValue = 0.25f;
            data.FindProperty("chainTravelSpeed").floatValue = 24f;
            data.FindProperty("pauseBetweenAttacks").floatValue = 0.08f;
            data.FindProperty("poisonCloudDamageMultiplier").floatValue = 0.25f;
            data.FindProperty("poisonCloudRadius").floatValue = 1.5f;
            data.FindProperty("poisonCloudDurationTurns").intValue = 3;
            data.FindProperty("poisonPuffSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(BoarDustArtPath);
            data.FindProperty("attackPoisonCloudChance").floatValue = 0.05f;
            data.FindProperty("ultimateDamageBonus").floatValue = 10f;
            data.FindProperty("dustBrawlSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(HoneyBadgerAuthoring.DustBrawlArtPath);
            data.FindProperty("dustBrawlWorldSize").floatValue = 1.35f;
            data.FindProperty("dustBrawlSpinSpeed").floatValue = 95f;
            data.ApplyModifiedPropertiesWithoutUndo();
            root.transform.localScale = Vector3.one * 0.6f;
            return PrefabUtility.SaveAsPrefabAsset(root, BadgerPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static GameObject BuildBoar(GameObject badgerPrefab)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BoarProjectileArtPath);
        GameObject root = PrefabUtility.LoadPrefabContents(BadgerPrefabPath);
        try
        {
            root.name = "Boar";
            HoneyBadgerProjectile badger = root.GetComponent<HoneyBadgerProjectile>();
            if (badger != null) Object.DestroyImmediate(badger);
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = Color.white; renderer.drawMode = SpriteDrawMode.Simple;
            BoarProjectile boar = root.AddComponent<BoarProjectile>();
            var data = new SerializedObject(boar);
            data.FindProperty("launchSpeedMultiplier").floatValue = 1.5f;
            data.FindProperty("maximumWallMisses").intValue = 7;
            data.FindProperty("wallBouncePauseDuration").floatValue = 0.2f;
            data.FindProperty("stunChance").floatValue = 0.25f;
            data.FindProperty("bossStunChance").floatValue = 0.05f;
            data.FindProperty("wallDisplayScale").floatValue = 1f;
            data.FindProperty("visualScaleMultiplier").floatValue = 1f;
            data.FindProperty("wallShakeAngle").floatValue = 5f;
            data.FindProperty("wallShakeScale").floatValue = 0.035f;
            data.ApplyModifiedPropertiesWithoutUndo();
            BoarChargeTrail trail = root.GetComponent<BoarChargeTrail>();
            if (trail == null) trail = root.AddComponent<BoarChargeTrail>();
            var trailData = new SerializedObject(trail);
            trailData.FindProperty("dustSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(BoarDustArtPath);
            trailData.FindProperty("puffSpacing").floatValue = 0.22f;
            trailData.FindProperty("trailWidth").floatValue = 0.13f;
            trailData.FindProperty("puffWorldSize").floatValue = 0.32f;
            trailData.FindProperty("fadeDuration").floatValue = 1f;
            trailData.FindProperty("dustColor").colorValue = new Color(0.78f, 0.61f, 0.39f, 0.86f);
            trailData.ApplyModifiedPropertiesWithoutUndo();
            ProjectileFlightVisual flightVisual = root.GetComponent<ProjectileFlightVisual>();
            if (flightVisual != null)
            {
                var visualData = new SerializedObject(flightVisual);
                visualData.FindProperty("spinDegreesPerSecond").floatValue = 0f;
                visualData.ApplyModifiedPropertiesWithoutUndo();
            }
            CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
            if (collider != null) Object.DestroyImmediate(collider);
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            if (body != null) body.sharedMaterial = null;
            root.transform.localScale = Vector3.one * 0.9f;
            return PrefabUtility.SaveAsPrefabAsset(root, BoarPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureQueue(AnimalProjectileQueue queue, GameObject badger, GameObject boar)
    {
        Sprite badgerIcon = AssetDatabase.LoadAssetAtPath<Sprite>(HoneyBadgerAuthoring.CombatSpritePath);
        Sprite boarIcon = AssetDatabase.LoadAssetAtPath<Sprite>(BoarArtPath);
        var data = new SerializedObject(queue);
        SerializedProperty entries = data.FindProperty("animals");
        ConfigureEntry(FindOrCreate(entries, BadgerPrefabPath, "Honey Badger"), "Honey Badger", badger, badgerIcon);
        ConfigureEntry(FindOrCreate(entries, BoarPrefabPath, "Boar"), "Boar", boar, boarIcon);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(queue);
    }

    private static SerializedProperty FindOrCreate(SerializedProperty entries, string prefabPath, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("prefab").objectReferenceValue == prefab
                || entry.FindPropertyRelative("displayName").stringValue == name) return entry;
        }
        int index = entries.arraySize;
        entries.InsertArrayElementAtIndex(index);
        return entries.GetArrayElementAtIndex(index);
    }

    private static void ConfigureEntry(SerializedProperty entry, string name, GameObject prefab, Sprite icon)
    {
        entry.FindPropertyRelative("displayName").stringValue = name;
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("icon").objectReferenceValue = icon;
        entry.FindPropertyRelative("unlocked").boolValue = false;
        entry.FindPropertyRelative("ammoPerTurn").intValue = 1;
        entry.FindPropertyRelative("areaExplosion").boolValue = false;
        entry.FindPropertyRelative("replicatesOnEnemyHits").boolValue = false;
    }

    private static void ConfigureUpgradePortrait(ExperienceAndUpgradeUI ui)
    {
        var data = new SerializedObject(ui);
        SerializedProperty portraits = data.FindProperty("animalPortraits");
        if (portraits.arraySize < 5) portraits.arraySize = 5;
        portraits.GetArrayElementAtIndex(4).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(BoarArtPath);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
    }

    private static void ConfigureSprite()
    {
        ConfigureSpriteAt(BoarArtPath, 1536f);
        ConfigureSpriteAt(BoarProjectileArtPath, 1200f);
        ConfigureSpriteAt(BoarDustArtPath, 1536f);
    }

    private static void ConfigureSpriteAt(string path, float pixelsPerUnit)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Boar art missing: " + path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
    }
}
