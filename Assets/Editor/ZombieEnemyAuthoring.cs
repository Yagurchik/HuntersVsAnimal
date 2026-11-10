using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ZombieEnemyAuthoring
{
    public const string LivingArtPath = "Assets/MonsterSprite/Zombie/Zombie.png";
    public const string GraveArtPath = "Assets/MonsterSprite/Zombie/ZombieGrave.png";
    public const string PrefabPath = "Assets/Resources/Enemies/Zombie.prefab";
    private const string SkeletonPath = "Assets/Enemy/Brick.prefab";

    [MenuItem("Tools/Forest Animal War/Enemies/Prepare Zombie")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before preparing the zombie prefab.");
        AssetDatabase.Refresh();
        PrepareTexture(LivingArtPath);
        PrepareTexture(GraveArtPath);
        Sprite living = AssetDatabase.LoadAssetAtPath<Sprite>(LivingArtPath);
        Sprite grave = AssetDatabase.LoadAssetAtPath<Sprite>(GraveArtPath);
        GameObject skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
        if (living == null || grave == null || skeleton == null)
            throw new InvalidOperationException("Zombie artwork or the base enemy prefab is missing.");

        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(skeleton);
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            root.name = "Zombie";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Transform visual = root.transform.Find("Visual");
            if (visual == null) throw new InvalidOperationException("Base enemy has no editable Visual child.");
            EnemyAppearanceVariants baseAppearances = visual.GetComponent<EnemyAppearanceVariants>();
            if (baseAppearances != null) Object.DestroyImmediate(baseAppearances);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = living;
            Animator animator = visual.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);

            Brick brick = root.GetComponent<Brick>();
            brick.hp = skeleton.GetComponent<Brick>().hp;
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

            EnemyHitReaction reaction = root.GetComponent<EnemyHitReaction>();
            if (reaction != null)
            {
                SerializedObject reactionData = new SerializedObject(reaction);
                reactionData.FindProperty("visualRoot").objectReferenceValue = visual;
                reactionData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
                reactionData.FindProperty("damageSprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/MonsterSprite/Zombie/ZombieDamage.png");
                reactionData.ApplyModifiedPropertiesWithoutUndo();
            }

            ZombieResurrection resurrection = root.GetComponent<ZombieResurrection>();
            if (resurrection == null) resurrection = root.AddComponent<ZombieResurrection>();
            SerializedObject zombieData = new SerializedObject(resurrection);
            zombieData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            zombieData.FindProperty("livingSprite").objectReferenceValue = living;
            zombieData.FindProperty("graveSprite").objectReferenceValue = grave;
            zombieData.FindProperty("graveVisualScale").floatValue = 0.95f;
            zombieData.FindProperty("revivedHealthFraction").floatValue = 0.5f;
            zombieData.FindProperty("graveTurns").intValue = 1;
            zombieData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Zombie prefab ready: destructible full-health grave, one player turn to break it, then half-health resurrection.");
    }

    private static void PrepareTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1254f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 512;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Compressed;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        Install();
        EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        if (progression == null) throw new InvalidOperationException("Gameplay scene has no LevelProgression.");
        SerializedObject data = new SerializedObject(progression);
        SerializedProperty rules = data.FindProperty("zombieSpawnRules");
        if (rules.arraySize == 0)
        {
            rules.arraySize = 1;
            SerializedProperty rule = rules.GetArrayElementAtIndex(0);
            rule.FindPropertyRelative("firstLevel").intValue = 2;
            rule.FindPropertyRelative("lastLevel").intValue = LevelProgression.MaxLevel;
            rule.FindPropertyRelative("defenseLine").intValue = 0;
            rule.FindPropertyRelative("roomIndex").intValue = 0;
            rule.FindPropertyRelative("zombiesPerWave").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }
}
