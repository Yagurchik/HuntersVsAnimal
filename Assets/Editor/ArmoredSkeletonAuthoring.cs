using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ArmoredSkeletonAuthoring
{
    public const string ArtPath = "Assets/MonsterSprite/ArmoredSkeleton/ArmoredSkeleton.png";
    public const string PrefabPath = "Assets/Resources/Enemies/ArmoredSkeleton.prefab";
    private const string BaseEnemyPath = "Assets/Enemy/Brick.prefab";

    [MenuItem("Tools/Forest Animal War/Enemies/Prepare Armored Skeleton")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before preparing the armored skeleton.");
        AssetDatabase.Refresh();
        PrepareTexture();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        GameObject baseEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemyPath);
        if (sprite == null || baseEnemy == null)
            throw new InvalidOperationException("Armored skeleton artwork or base enemy prefab is missing.");

        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(baseEnemy);
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            root.name = "ArmoredSkeleton";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Transform visual = root.transform.Find("Visual");
            if (visual == null) throw new InvalidOperationException("Base enemy has no Visual child.");
            EnemyAppearanceVariants baseAppearances = visual.GetComponent<EnemyAppearanceVariants>();
            if (baseAppearances != null) Object.DestroyImmediate(baseAppearances);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one * 1.1956f;
            Animator animator = visual.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);

            Brick brick = root.GetComponent<Brick>();
            brick.hp = baseEnemy.GetComponent<Brick>().hp;
            brick.dmg = 1f;
            SerializedObject brickData = new SerializedObject(brick);
            brickData.FindProperty("experienceReward").intValue = 20;
            GameObject previousPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            brickData.FindProperty("healthCoefficient").floatValue = previousPrefab != null
                ? previousPrefab.GetComponent<Brick>().HealthCoefficient : 1f;
            brickData.FindProperty("bossHealth").boolValue = false;
            brickData.FindProperty("footprint").vector2IntValue = Vector2Int.one;
            brickData.FindProperty("spriteAnimator").objectReferenceValue = null;
            brickData.FindProperty("stepJumpHeight").floatValue = 0f;
            brickData.FindProperty("stepStretch").floatValue = 0f;
            brickData.FindProperty("landingSquash").floatValue = 0f;
            brickData.ApplyModifiedPropertiesWithoutUndo();

            Collider2D collider = root.GetComponent<Collider2D>();
            ArmoredSkeletonArmor armor = root.GetComponent<ArmoredSkeletonArmor>();
            if (armor == null) armor = root.AddComponent<ArmoredSkeletonArmor>();
            SerializedObject armorData = new SerializedObject(armor);
            armorData.FindProperty("topDamageMultiplier").floatValue = 0.5f;
            armorData.FindProperty("protectedZoneStart").floatValue = 0.5f;
            armorData.FindProperty("armorCollider").objectReferenceValue = collider;
            armorData.ApplyModifiedPropertiesWithoutUndo();

            EnemyHitReaction reaction = root.GetComponent<EnemyHitReaction>();
            if (reaction != null)
            {
                SerializedObject reactionData = new SerializedObject(reaction);
                reactionData.FindProperty("visualRoot").objectReferenceValue = visual;
                reactionData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
                reactionData.FindProperty("damageSprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/MonsterSprite/ArmoredSkeleton/ArmoredSkeletonDamage.png");
                reactionData.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        ConfigureEncounterRules();
        AssetDatabase.SaveAssets();
        Debug.Log("Armored skeleton prefab ready: upper direct hits retain 50% damage.");
    }

    private static void ConfigureEncounterRules()
    {
        if (SceneManager.GetActiveScene().path != GameUIPreviewWindow.GameplayPath)
            EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        if (progression == null)
            throw new InvalidOperationException("Gameplay scene has no LevelProgression.");
        SerializedObject data = new SerializedObject(progression);
        SerializedProperty rules = data.FindProperty("armoredSkeletonSpawnRules");
        bool hasGloomyHollowRule = false;
        for (int i = 0; i < rules.arraySize; i++)
        {
            SerializedProperty rule = rules.GetArrayElementAtIndex(i);
            if (rule.FindPropertyRelative("firstLevel").intValue <= LevelProgression.LevelsPerLocation + 1
                && rule.FindPropertyRelative("lastLevel").intValue >= LevelProgression.MaxLevel
                && rule.FindPropertyRelative("defenseLine").intValue == 0
                && rule.FindPropertyRelative("roomIndex").intValue == 0
                && rule.FindPropertyRelative("armoredSkeletonsPerWave").intValue > 0)
            {
                hasGloomyHollowRule = true;
                break;
            }
        }
        if (!hasGloomyHollowRule)
        {
            int index = rules.arraySize;
            rules.InsertArrayElementAtIndex(index);
            SerializedProperty rule = rules.GetArrayElementAtIndex(index);
            rule.FindPropertyRelative("firstLevel").intValue = LevelProgression.LevelsPerLocation + 1;
            rule.FindPropertyRelative("lastLevel").intValue = LevelProgression.MaxLevel;
            rule.FindPropertyRelative("defenseLine").intValue = 0;
            rule.FindPropertyRelative("roomIndex").intValue = 0;
            rule.FindPropertyRelative("armoredSkeletonsPerWave").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(progression);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static void PrepareTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture: " + ArtPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1254f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 512;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
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
}
