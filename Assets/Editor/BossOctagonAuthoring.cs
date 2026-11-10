using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BossOctagonAuthoring
{
    public const string MirageArt = "Assets/MonsterSprite/Mirage/Mirage.png";
    public const string MiragePrefab = "Assets/Resources/Enemies/MirageBoss.prefab";
    private static readonly string[] ExistingBosses = {
        "Assets/Resources/Enemies/LichBoss.prefab",
        "Assets/Resources/Enemies/BoneDragonBoss.prefab",
        "Assets/Resources/Enemies/BoneDragonMiniBoss.prefab"
    };

    [MenuItem("Tools/Forest Animal War/Enemies/Bake octagonal bosses and Mirage")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before editing boss prefabs.");
        AssetDatabase.Refresh();
        foreach (string path in ExistingBosses) ConvertExistingBoss(path);
        EnsureMiragePrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("Boss octagons baked: Lich and both Bone Dragons. Mirage keeps an editable square mirror collider.");
    }

    private static void ConvertExistingBoss(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing boss prefab: " + path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            BoxCollider2D oldBox = root.GetComponent<BoxCollider2D>();
            PolygonCollider2D octagon = root.GetComponent<PolygonCollider2D>();
            if (oldBox == null && octagon == null)
                throw new InvalidOperationException("Boss has no physical collider: " + path);
            Vector2 size = oldBox != null ? oldBox.size : octagon.bounds.size;
            if (size.x <= 0f || size.y <= 0f)
                size = path.Contains("Lich") ? new Vector2(1.9f, 1.9f) : new Vector2(2.8f, 2.8f);
            if (octagon == null) octagon = root.AddComponent<PolygonCollider2D>();
            octagon.pathCount = 1;
            octagon.SetPath(0, BossOctagonGeometry.CreatePoints(size));
            if (oldBox != null)
            {
                octagon.offset = oldBox.offset;
                octagon.sharedMaterial = oldBox.sharedMaterial;
                octagon.isTrigger = oldBox.isTrigger;
                Object.DestroyImmediate(oldBox);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void EnsureMiragePrefab()
    {
        TextureImporter importer = AssetImporter.GetAtPath(MirageArt) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing Mirage art: " + MirageArt);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 256f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MirageArt);
        GameObject skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(LichBossAuthoring.SkeletonPath);
        if (sprite == null || skeleton == null)
            throw new InvalidOperationException("Mirage art or ordinary enemy template did not import.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MiragePrefab) != null)
        {
            // Re-running the bake must also repair an older, octagonal Mirage.
            GameObject existing = PrefabUtility.LoadPrefabContents(MiragePrefab);
            try
            {
                PolygonCollider2D oldOctagon = existing.GetComponent<PolygonCollider2D>();
                BoxCollider2D mirrorBody = existing.GetComponent<BoxCollider2D>();
                if (mirrorBody == null) mirrorBody = existing.AddComponent<BoxCollider2D>();
                mirrorBody.size = new Vector2(2.8f, 2.8f);
                mirrorBody.edgeRadius = 0f;
                if (oldOctagon != null)
                {
                    mirrorBody.offset = oldOctagon.offset;
                    mirrorBody.sharedMaterial = oldOctagon.sharedMaterial;
                    mirrorBody.isTrigger = oldOctagon.isTrigger;
                    Object.DestroyImmediate(oldOctagon);
                }
                if (existing.GetComponent<MirageCornerMirror>() == null)
                    existing.AddComponent<MirageCornerMirror>();
                PrefabUtility.SaveAsPrefabAsset(existing, MiragePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(existing); }
            return;
        }

        GameObject root = new GameObject("MirageBoss");
        root.SetActive(false);
        try
        {
            root.tag = "Enemy";
            root.layer = skeleton.layer;
            GameObject visual = new GameObject("Visual");
            visual.layer = root.layer;
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            SpriteRenderer templateRenderer = skeleton.GetComponentInChildren<SpriteRenderer>(true);
            renderer.sprite = sprite;
            renderer.sharedMaterial = templateRenderer.sharedMaterial;
            renderer.sortingLayerID = templateRenderer.sortingLayerID;
            renderer.sortingOrder = templateRenderer.sortingOrder;
            // The visible head slightly overhangs the 2.8-unit physics shape.
            const float artCanvasSize = 3.3f;
            visual.transform.localScale = new Vector3(artCanvasSize / sprite.bounds.size.x,
                artCanvasSize / sprite.bounds.size.y, 1f);
            visual.AddComponent<EnemyVisualHover>();

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2.8f, 2.8f);
            Collider2D templateCollider = skeleton.GetComponent<Collider2D>();
            if (templateCollider != null) collider.sharedMaterial = templateCollider.sharedMaterial;

            Brick brick = root.AddComponent<Brick>();
            Brick baseBrick = skeleton.GetComponent<Brick>();
            GameObject miniBoss = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Enemies/BoneDragonMiniBoss.prefab");
            Brick miniBossStats = miniBoss != null ? miniBoss.GetComponent<Brick>() : null;
            brick.hp = miniBossStats != null ? miniBossStats.hp : 5550f;
            brick.dmg = baseBrick.dmg;
            SerializedObject data = new SerializedObject(brick);
            data.FindProperty("footprint").vector2IntValue = new Vector2Int(3, 3);
            SerializedProperty cells = data.FindProperty("occupiedCellOffsets");
            cells.arraySize = 9;
            for (int i = 0; i < 9; i++)
                cells.GetArrayElementAtIndex(i).vector2IntValue = new Vector2Int(i % 3, i / 3);
            data.FindProperty("bossHealth").boolValue = true;
            data.FindProperty("experienceReward").intValue = 100;
            data.FindProperty("stepJumpHeight").floatValue = 0f;
            data.FindProperty("stepStretch").floatValue = 0f;
            data.FindProperty("landingSquash").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();

            EnemyHitReaction reaction = root.GetComponent<EnemyHitReaction>();
            SerializedObject reactionData = new SerializedObject(reaction);
            reactionData.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            reactionData.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            reactionData.FindProperty("damageSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/MonsterSprite/Mirage/MirageDamage.png");
            reactionData.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<MirageCornerMirror>();
            EnemyFirstEncounterProfile encounter = root.AddComponent<EnemyFirstEncounterProfile>();
            encounter.Configure("mirage_boss", "Мираж",
                "Многоликая проклятая голова. Отражает выстрелы обратно по траектории прилёта.",
                sprite, new Color(0.47f, 0.85f, 0.93f));

            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, MiragePrefab);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
