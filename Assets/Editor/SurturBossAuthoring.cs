using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BoneDragonAuthoring
{
    public const string ArtPath = "Assets/MonsterSprite/DragonBosses/BoneElf.png";
    public const string FoldedWingsPath = "Assets/MonsterSprite/DragonBosses/BoneElfFoldedWings.png";
    public const string BossPath = "Assets/Resources/Enemies/BoneDragonBoss.prefab";
    public const string MiniBossPath = "Assets/Resources/Enemies/BoneDragonMiniBoss.prefab";

    [MenuItem("Tools/Forest Animal War/Enemies/Prepare Bone Elf")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        TextureImporter importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("BoneElf.png is missing.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 289.216f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(textureSettings);
        importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        Sprite foldedWings = AssetDatabase.LoadAssetAtPath<Sprite>(FoldedWingsPath);
        GameObject skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(LichBossAuthoring.SkeletonPath);
        if (sprite == null || foldedWings == null || skeleton == null)
            throw new InvalidOperationException("Boss dependencies are missing.");
        CreatePrefab(BossPath, "BoneDragonBoss", sprite, foldedWings, skeleton, false);
        CreatePrefab(MiniBossPath, "BoneDragonMiniBoss", sprite, foldedWings, skeleton, true);
        AssetDatabase.SaveAssets();
        Debug.Log("Bone Elf ready: level 5 room 4-4 and Gloomy Hollow mini-boss prefabs.");
    }

    public static void InstallBatch()
    {
        try { Install(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void CreatePrefab(string path, string objectName, Sprite sprite,
        Sprite foldedWings, GameObject skeleton, bool mini)
    {
        var root = new GameObject(objectName);
        root.SetActive(false);
        try
        {
            root.tag = "Enemy";
            root.layer = skeleton.layer;
            GameObject visual = new GameObject("Visual");
            visual.layer = root.layer;
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            SpriteRenderer skeletonRenderer = skeleton.GetComponentInChildren<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = skeletonRenderer.sharedMaterial;
            renderer.sortingLayerID = skeletonRenderer.sortingLayerID;
            renderer.sortingOrder = skeletonRenderer.sortingOrder;
            Vector2 targetSize = new Vector2(3.18f, 3.18f);
            visual.transform.localScale = new Vector3(targetSize.x / sprite.bounds.size.x,
                targetSize.y / sprite.bounds.size.y, 1f);
            GameObject wings = new GameObject("FoldedWings");
            wings.layer = root.layer;
            wings.transform.SetParent(visual.transform, false);
            SpriteRenderer wingRenderer = wings.AddComponent<SpriteRenderer>();
            wingRenderer.sprite = foldedWings;
            wingRenderer.sharedMaterial = renderer.sharedMaterial;
            wingRenderer.sortingLayerID = renderer.sortingLayerID;
            wingRenderer.sortingOrder = renderer.sortingOrder - 1;
            visual.AddComponent<EnemyVisualHover>();

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            PolygonCollider2D collider = root.AddComponent<PolygonCollider2D>();
            collider.pathCount = 1;
            collider.SetPath(0, BossOctagonGeometry.CreatePoints(new Vector2(2.8f, 2.8f)));
            Collider2D sourceCollider = skeleton.GetComponent<Collider2D>();
            if (sourceCollider != null) collider.sharedMaterial = sourceCollider.sharedMaterial;

            Brick brick = root.AddComponent<Brick>();
            Brick baseBrick = skeleton.GetComponent<Brick>();
            GameObject existingBoss = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Brick previousBalance = existingBoss != null ? existingBoss.GetComponent<Brick>() : null;
            brick.hp = previousBalance != null ? previousBalance.hp
                : (mini ? 5550f : 11100f);
            brick.dmg = baseBrick.dmg;
            var brickData = new SerializedObject(brick);
            brickData.FindProperty("footprint").vector2IntValue = new Vector2Int(3, 3);
            SerializedProperty occupied = brickData.FindProperty("occupiedCellOffsets");
            List<Vector2Int> cells = Solid(3, 3);
            occupied.arraySize = cells.Count;
            for (int i = 0; i < cells.Count; i++)
                occupied.GetArrayElementAtIndex(i).vector2IntValue = cells[i];
            brickData.FindProperty("experienceReward").intValue = mini ? 100 : 200;
            brickData.FindProperty("bossHealth").boolValue = true;
            brickData.FindProperty("stepJumpHeight").floatValue = 0f;
            brickData.FindProperty("stepStretch").floatValue = 0f;
            brickData.FindProperty("landingSquash").floatValue = 0f;
            brickData.ApplyModifiedPropertiesWithoutUndo();

            EnemyHitReaction reaction = root.GetComponent<EnemyHitReaction>();
            var reactionData = new SerializedObject(reaction);
            reactionData.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            reactionData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            reactionData.FindProperty("damageSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Resources/Enemies/BoneDragon/BoneDragonDamage.png");
            reactionData.ApplyModifiedPropertiesWithoutUndo();

            BoneDragonSpriteAnimator spriteAnimator = root.AddComponent<BoneDragonSpriteAnimator>();
            var animatorData = new SerializedObject(spriteAnimator);
            animatorData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            animatorData.FindProperty("hitReaction").objectReferenceValue = reaction;
            animatorData.ApplyModifiedPropertiesWithoutUndo();

            BossEnemyTurnController controller = root.AddComponent<BossEnemyTurnController>();
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("movementCellsPerTurn").intValue = 2;
            controllerData.FindProperty("summonEveryTurns").intValue = 3;
            controllerData.FindProperty("summonOnFirstTurn").boolValue = false;
            controllerData.FindProperty("skeletonPrefab").objectReferenceValue = skeleton;
            controllerData.FindProperty("summonedSkeletonTint").colorValue = Color.white;
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            EnemyFirstEncounterProfile profile = root.AddComponent<EnemyFirstEncounterProfile>();
            profile.Configure(mini ? "bone_dragon_miniboss" : "bone_dragon_boss",
                mini ? "Костяной эльф — мини-босс" : "Костяной эльф",
                "Движется на две клетки, сам рассеивает тьму и раз в три хода выстраивает перед собой линию скелетов.",
                sprite, new Color(0.55f, 0.9f, 0.16f, 1f));

            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static List<Vector2Int> Solid(int width, int height)
    {
        var result = new List<Vector2Int>(width * height);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) result.Add(new Vector2Int(x, y));
        return result;
    }

}
