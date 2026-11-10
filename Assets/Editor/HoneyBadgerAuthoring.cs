using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HoneyBadgerAuthoring
{
    public const string GameplayScene = "Assets/Asset.unity";
    public const string MenuScene = "Assets/Scenes/UIscene.unity";
    public const string BaseProjectilePath = "Assets/AnimalsBullet/BaseBullet.prefab";
    public const string PrefabPath = "Assets/AnimalsBullet/HoneyBadger.prefab";
    public const string CombatSpritePath = "Assets/MonsterSprite/ProjectileArt/HoneyBadger/honey-badger-v2-simple.png";
    public const string PortraitPath = "Assets/MonsterSprite/ProjectileArt/HoneyBadger/honey-badger-v1.png";
    public const string DustBrawlArtPath = "Assets/MonsterSprite/ProjectileArt/HoneyBadger/honey-badger-dust-brawl.png";
    public const string MenuDefinitionPath = "Assets/UI/MenuData/HoneyBadger.asset";
    // The generated PNG has a 606 px opaque silhouette inside a larger transparent canvas.
    // At 606 PPU that visible silhouette is one local unit wide, matching the prefab's
    // diameter-1 CircleCollider2D without changing its gameplay size.
    private const float CombatSpritePixelsPerUnit = 606f;

    [MenuItem("Tools/Forest Animal War/Projectiles/Install Honey Badger")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before installing the honey badger.");

        ConfigureSprite(CombatSpritePath, CombatSpritePixelsPerUnit, 256);
        ConfigureSprite(PortraitPath, 512f, 2048);
        ConfigureSprite(DustBrawlArtPath, 1000f, 512);
        GameObject prefab = BuildPrefab();
        BuildMenuDefinition();

        EditorSceneManager.OpenScene(GameplayScene);
        AnimalProjectileQueue queue = GameUIPreviewWindow.Find<AnimalProjectileQueue>();
        ExperienceAndUpgradeUI upgradeUi = GameUIPreviewWindow.Find<ExperienceAndUpgradeUI>();
        if (queue == null || upgradeUi == null)
            throw new InvalidOperationException("Gameplay scene is missing the projectile queue or upgrade UI.");

        AddOrRefreshQueueEntry(queue, prefab);
        AddUpgradePortrait(upgradeUi);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Honey badger projectile, queue slot and upgrade artwork installed.");
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        try
        {
            Install();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static GameObject BuildPrefab()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CombatSpritePath);
        if (sprite == null) throw new InvalidOperationException("Missing honey badger combat sprite.");

        GameObject root = PrefabUtility.LoadPrefabContents(BaseProjectilePath);
        try
        {
            root.name = "HoneyBadger";
            root.tag = "Animal";
            root.layer = LayerMask.NameToLayer("bullet");

            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.enabled = true;

            FireflyImpactEffect firefly = root.GetComponent<FireflyImpactEffect>();
            if (firefly != null) UnityEngine.Object.DestroyImmediate(firefly);
            HoneyBadgerProjectile honeyBadger = root.GetComponent<HoneyBadgerProjectile>();
            if (honeyBadger == null) honeyBadger = root.AddComponent<HoneyBadgerProjectile>();
            SerializedObject honeyBadgerData = new SerializedObject(honeyBadger);
            honeyBadgerData.FindProperty("maximumAttacks").intValue = 5;
            honeyBadgerData.FindProperty("maximumConsecutiveAttacksPerTarget").intValue = 1;
            honeyBadgerData.FindProperty("attacksAddedPerKill").intValue = 3;
            honeyBadgerData.FindProperty("chainJumpRadius").floatValue = 1.2f;
            honeyBadgerData.FindProperty("damageIncreasePerKill").floatValue = 0.25f;
            honeyBadgerData.FindProperty("chainTravelSpeed").floatValue = 24f;
            honeyBadgerData.FindProperty("pauseBetweenAttacks").floatValue = 0.08f;
            honeyBadgerData.FindProperty("poisonCloudDamageMultiplier").floatValue = 0.25f;
            honeyBadgerData.FindProperty("poisonCloudRadius").floatValue = 1.5f;
            honeyBadgerData.FindProperty("poisonCloudDurationTurns").intValue = 3;
            honeyBadgerData.FindProperty("poisonPuffSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(BoarAndBadgerAuthoring.BoarDustArtPath);
            honeyBadgerData.FindProperty("attackPoisonCloudChance").floatValue = 0.05f;
            honeyBadgerData.FindProperty("ultimateDamageBonus").floatValue = 10f;
            honeyBadgerData.FindProperty("dustBrawlSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(DustBrawlArtPath);
            honeyBadgerData.FindProperty("dustBrawlWorldSize").floatValue = 1.35f;
            honeyBadgerData.FindProperty("dustBrawlSpinSpeed").floatValue = 95f;
            honeyBadgerData.ApplyModifiedPropertiesWithoutUndo();
            if (root.GetComponent<ProjectileFlightVisual>() == null)
                root.AddComponent<ProjectileFlightVisual>();

            Transform inheritedAura = root.transform.Find("Circle");
            if (inheritedAura != null)
            {
                SpriteRenderer auraRenderer = inheritedAura.GetComponent<SpriteRenderer>();
                if (auraRenderer != null) auraRenderer.enabled = false;
            }

            Animal animal = root.GetComponent<Animal>();
            if (animal == null) animal = root.AddComponent<Animal>();
            SerializedObject animalData = new SerializedObject(animal);
            animalData.FindProperty("dmg").floatValue = 55f;
            animalData.FindProperty("hp").floatValue = 240f;
            animalData.ApplyModifiedPropertiesWithoutUndo();

            CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
            if (collider == null) collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.isTrigger = false;
            root.transform.localScale = Vector3.one * 0.6f;

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AddOrRefreshQueueEntry(AnimalProjectileQueue queue, GameObject prefab)
    {
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(CombatSpritePath);
        SerializedObject queueData = new SerializedObject(queue);
        SerializedProperty entries = queueData.FindProperty("animals");
        int index = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty candidate = entries.GetArrayElementAtIndex(i);
            GameObject candidatePrefab = candidate.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
            if (candidatePrefab == prefab || candidate.FindPropertyRelative("displayName").stringValue == "Honey Badger")
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
        }

        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("displayName").stringValue = "Honey Badger";
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("icon").objectReferenceValue = icon;
        entry.FindPropertyRelative("unlocked").boolValue = false;
        entry.FindPropertyRelative("ammoPerTurn").intValue = 1;
        entry.FindPropertyRelative("areaExplosion").boolValue = false;
        entry.FindPropertyRelative("explosionRadius").floatValue = 1.2f;
        entry.FindPropertyRelative("explosionDuration").floatValue = 0.18f;
        entry.FindPropertyRelative("explosionDamageMultiplier").floatValue = 0.25f;
        entry.FindPropertyRelative("replicatesOnEnemyHits").boolValue = false;
        entry.FindPropertyRelative("enemyHitsToReplicate").intValue = 5;
        queueData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(queue);
    }

    private static void AddUpgradePortrait(ExperienceAndUpgradeUI ui)
    {
        Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
        SerializedObject uiData = new SerializedObject(ui);
        SerializedProperty portraits = uiData.FindProperty("animalPortraits");
        if (portraits.arraySize < 4) portraits.arraySize = 4;
        portraits.GetArrayElementAtIndex(3).objectReferenceValue = portrait;
        uiData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
    }

    private static void BuildMenuDefinition()
    {
        MenuAnimalDefinition definition = AssetDatabase.LoadAssetAtPath<MenuAnimalDefinition>(MenuDefinitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<MenuAnimalDefinition>();
            AssetDatabase.CreateAsset(definition, MenuDefinitionPath);
        }
        definition.animalIndex = 3;
        definition.displayName = "Медоед";
        definition.role = "ЦЕПНОЙ ОХОТНИК";
        definition.description = "Прыгает на самого раненого врага в радиусе 1,2 клетки. Убийство удлиняет цепь и усиливает её урон на 25%.";
        definition.accent = new Color(0.83f, 0.58f, 0.28f, 1f);
        definition.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
        EditorUtility.SetDirty(definition);
    }

    private static void ConfigureSprite(string path, float pixelsPerUnit, int maximumSize)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Not a texture: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = maximumSize;
        importer.SaveAndReimport();
    }
}
