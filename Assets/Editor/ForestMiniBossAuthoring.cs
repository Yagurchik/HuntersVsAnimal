using System;
using UnityEditor;
using UnityEngine;

public static class ForestMiniBossAuthoring
{
    private const string Template = "Assets/Resources/Enemies/DrownedMiniBoss.prefab";
    private const string Art = "Assets/MonsterSprite/ForestMiniBosses/";
    private const string Enemy = "Assets/Resources/Enemies/";

    [MenuItem("Forest Animal War/Enemies/Bake forest mini-boss prefabs")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before baking prefabs.");

        Sprite beeSprite = ImportSprite("HiveBee", 1254);
        Sprite hiveSprite = ImportSprite("HiveHollow", 600);
        Sprite hiveDamage = ImportSprite("HiveHollowDamage", 600);
        Sprite judgeSprite = ImportSprite("GreenJudge", 600);
        Sprite judgeDamage = ImportSprite("GreenJudgeDamage", 600);

        GameObject bee = BakeEnemy("HiveBee", beeSprite, beeSprite, Vector2Int.one,
            100f, 0, false, "hive_bee", "Пчела из дупла",
            "Слабый призыв Улья-дупла. Исчезает после одного-двух попаданий и не даёт опыт.",
            new Color(0.95f, 0.70f, 0.23f), false);
        BakeEnemy("HiveHollowMiniBoss", hiveSprite, hiveDamage, new Vector2Int(2, 2),
            3000f, 60, true, "hive_hollow_mini_boss", "Улей-дупло",
            "Лесной мини-босс 2×2. Каждое третье прямое попадание выпускает двух слабых пчёл; одновременно их не больше двух.",
            new Color(0.94f, 0.65f, 0.20f), false, bee);
        BakeEnemy("GreenJudgeMiniBoss", judgeSprite, judgeDamage, new Vector2Int(2, 2),
            2800f, 60, true, "green_judge_mini_boss", "Зелёный судья",
            "Лесной мини-босс 2×2. Каждый ход открывает глаз с другой стороны. Прямой удар по стороне светящегося глаза наносит на 50% больше урона.",
            new Color(0.61f, 0.84f, 0.33f), true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Forest mini-boss prefabs baked: Hive Hollow, Green Judge, Hive Bee.");
    }

    private static Sprite ImportSprite(string name, float pixelsPerUnit)
    {
        string path = Art + name + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing art: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + path);
        return sprite;
    }

    private static GameObject BakeEnemy(string name, Sprite sprite, Sprite damageSprite,
        Vector2Int footprint, float health, int reward, bool bossHealth,
        string id, string title, string summary, Color accent, bool judge,
        GameObject beePrefab = null)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Template);
        try
        {
            root.name = name;
            Brick brick = root.GetComponent<Brick>();
            Set(brick, "hp", property => property.floatValue = health);
            Set(brick, "bossHealth", property => property.boolValue = bossHealth);
            Set(brick, "experienceReward", property => property.intValue = reward);
            Set(brick, "footprint", property => property.vector2IntValue = footprint);
            if (!bossHealth)
                Set(brick, "leaveFireflyBonfire", property => property.boolValue = true);

            SpriteRenderer face = root.GetComponentInChildren<SpriteRenderer>(true);
            if (face == null) throw new InvalidOperationException("Template has no SpriteRenderer.");
            face.sprite = sprite;
            face.transform.localScale = new Vector3(1.05f, 1.05f, 1f);
            Set(root.GetComponent<EnemyHitReaction>(), "damageSprite",
                property => property.objectReferenceValue = damageSprite);
            root.GetComponent<EnemyFirstEncounterProfile>().Configure(id, title, summary, sprite, accent);

            if (footprint == Vector2Int.one)
            {
                PolygonCollider2D collider = root.GetComponent<PolygonCollider2D>();
                collider.SetPath(0, new[] {
                    new Vector2(-0.38f, -0.45f), new Vector2(0.38f, -0.45f),
                    new Vector2(0.45f, -0.38f), new Vector2(0.45f, 0.38f),
                    new Vector2(0.38f, 0.45f), new Vector2(-0.38f, 0.45f),
                    new Vector2(-0.45f, 0.38f), new Vector2(-0.45f, -0.38f)
                });
            }
            else
            {
                ForestMiniBossAbility ability = root.AddComponent<ForestMiniBossAbility>();
                Set(ability, "kind", property => property.enumValueIndex = judge ? 1 : 0);
                Set(ability, "beePrefab", property => property.objectReferenceValue = beePrefab);
                Set(ability, "faceRenderer", property => property.objectReferenceValue = face);
            }

            string path = Enemy + name + ".prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null) throw new InvalidOperationException("Prefab save failed: " + path);
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Set(UnityEngine.Object target, string name, Action<SerializedProperty> assign)
    {
        if (target == null) throw new InvalidOperationException("Missing component for " + name);
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException("Missing property: " + name);
        assign(property);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
