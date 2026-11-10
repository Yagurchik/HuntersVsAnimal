using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GhostEnemyAuthoring
{
    public const string ArtPath = "Assets/MonsterSprite/Ghost/GhostReferenceCutout.png";
    public const string PrefabPath = "Assets/Resources/Enemies/Ghost.prefab";
    private const string BaseEnemyPath = "Assets/Enemy/Brick.prefab";

    [MenuItem("Tools/Forest Animal War/Enemies/Prepare Ghost")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before preparing the ghost.");
        AssetDatabase.Refresh();
        PrepareTexture();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        GameObject baseEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemyPath);
        if (sprite == null || baseEnemy == null)
            throw new InvalidOperationException("Ghost art or base enemy prefab is missing.");

        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(baseEnemy);
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            root.name = "Ghost";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Transform visual = root.transform.Find("Visual");
            if (visual == null) throw new InvalidOperationException("Base enemy has no Visual child.");
            EnemyAppearanceVariants baseAppearances = visual.GetComponent<EnemyAppearanceVariants>();
            if (baseAppearances != null) Object.DestroyImmediate(baseAppearances);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            // The visible head covers its full logical 1x1 cell.
            visual.localScale = new Vector3(1.281743f, 1.245973f, 1f);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, 0.9f);
            Animator animator = visual.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);
            EnemyVisualHover ordinaryHover = visual.GetComponent<EnemyVisualHover>();
            if (ordinaryHover != null) Object.DestroyImmediate(ordinaryHover);

            Brick brick = root.GetComponent<Brick>();
            brick.hp = baseEnemy.GetComponent<Brick>().hp * 0.8f;
            brick.dmg = 1f;
            SerializedObject brickData = new SerializedObject(brick);
            brickData.FindProperty("healthCoefficient").floatValue = 0.8f;
            brickData.FindProperty("bossHealth").boolValue = false;
            brickData.FindProperty("experienceReward").intValue = 10;
            brickData.FindProperty("footprint").vector2IntValue = Vector2Int.one;
            brickData.FindProperty("spriteAnimator").objectReferenceValue = null;
            brickData.FindProperty("stepJumpHeight").floatValue = 0f;
            brickData.FindProperty("stepStretch").floatValue = 0f;
            brickData.FindProperty("landingSquash").floatValue = 0f;
            brickData.ApplyModifiedPropertiesWithoutUndo();

            GhostVisualAnimator ghostAnimation =
                visual.gameObject.AddComponent<GhostVisualAnimator>();
            SerializedObject animationData = new SerializedObject(ghostAnimation);
            animationData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            animationData.ApplyModifiedPropertiesWithoutUndo();
            GhostPhasing phasing = root.AddComponent<GhostPhasing>();
            SerializedObject phaseData = new SerializedObject(phasing);
            BoxCollider2D bodyCollider = root.GetComponent<BoxCollider2D>();
            if (bodyCollider != null) bodyCollider.edgeRadius = 0.12f;
            phaseData.FindProperty("bodyCollider").objectReferenceValue = bodyCollider;
            phaseData.FindProperty("visual").objectReferenceValue = ghostAnimation;
            phaseData.FindProperty("passThroughChance").floatValue = 0.2f;
            phaseData.ApplyModifiedPropertiesWithoutUndo();

            EnemyHitReaction reaction = root.GetComponent<EnemyHitReaction>();
            if (reaction != null)
            {
                SerializedObject reactionData = new SerializedObject(reaction);
                reactionData.FindProperty("visualRoot").objectReferenceValue = visual;
                reactionData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
                reactionData.FindProperty("damageSprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/MonsterSprite/Ghost/GhostDamage.png");
                reactionData.ApplyModifiedPropertiesWithoutUndo();
            }

            EnemyFirstEncounterProfile profile = root.GetComponent<EnemyFirstEncounterProfile>();
            if (profile == null) profile = root.AddComponent<EnemyFirstEncounterProfile>();
            profile.Configure("ghost", "Призрак",
                "Имеет 80% здоровья обычного врага. Каждый снаряд с шансом 20% проходит сквозь него; уже наложенный огонь он не избегает.",
                sprite, new Color(0.6f, 0.85f, 0.78f, 1f));

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Ghost prefab prepared with 80% ordinary HP, 20% projectile phasing and editable floating animation.");
    }

    private static void PrepareTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing ghost texture: " + ArtPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1254f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
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
