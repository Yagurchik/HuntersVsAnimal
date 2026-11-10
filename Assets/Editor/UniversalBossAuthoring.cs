using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class UniversalBossAuthoring
{
    private const string Template = "Assets/Resources/Enemies/DrownedMiniBoss.prefab";
    private const string Art = "Assets/MonsterSprite/UniversalBosses/";
    private const string Enemy = "Assets/Resources/Enemies/";

    [MenuItem("Forest Animal War/Enemies/Bake universal boss prefabs")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before baking prefabs.");
        Sprite marker = EnsureMarker();
        Sprite impostor = ImportSprite("Impostor");
        Sprite impostorDamage = ImportSprite("ImpostorDamage");
        GameObject decoy = BakeEnemy("ImpostorDecoy", impostor, impostorDamage,
            "impostor_boss", "Самозванец", "Хрупкая обманка Самозванца.",
            new Color(0.75f, 0.9f, 0.38f), null, marker, true);

        BakeEnemy("BellRingerBoss", ImportSprite("BellRinger"), ImportSprite("BellRingerDamage"),
            "bell_ringer_boss", "Колокольщик",
            "Универсальный босс 2×2. Если его ударили и он пережил залп, все обычные враги в комнате сразу делают дополнительный шаг. Смерть босса отменяет этот шаг.",
            new Color(0.94f, 0.70f, 0.27f), UniversalBossAbility.AbilityKind.BellRinger,
            marker);
        BakeEnemy("KnotBinderBoss", ImportSprite("KnotBinder"), ImportSprite("KnotBinderDamage"),
            "knot_binder_boss", "Узловик",
            "Универсальный босс 2×2. Перенаправляет по 20% прямого урона двум ближайшим обычным союзникам. Без союзников получает полный урон.",
            new Color(0.52f, 0.86f, 0.30f), UniversalBossAbility.AbilityKind.KnotBinder,
            marker);
        BakeEnemy("VolleyEaterBoss", ImportSprite("VolleyEater"), ImportSprite("VolleyEaterDamage"),
            "volley_eater_boss", "Пожиратель залпа",
            "Универсальный босс 2×2. Полностью съедает первый залп, который в него попал, и лечится на 3% максимального HP за каждый проглоченный снаряд. Следующие залпы наносят обычный урон.",
            new Color(0.67f, 0.89f, 0.29f), UniversalBossAbility.AbilityKind.VolleyEater,
            marker);
        BakeEnemy("ThreeSealIdolBoss", ImportSprite("ThreeSealIdol"), ImportSprite("ThreeSealIdolDamage"),
            "three_seal_idol_boss", "Трёхпечатный идол",
            "Универсальный босс 2×2. Три печати поглощают по одному прямому попаданию. После их разрушения прямой урон по идолу увеличивается на 30%.",
            new Color(0.87f, 0.76f, 0.34f), UniversalBossAbility.AbilityKind.ThreeSealIdol,
            marker);
        BakeEnemy("ImpostorBoss", impostor, impostorDamage,
            "impostor_boss", "Самозванец",
            "Универсальный босс 2×2. Ниже половины HP создаёт две хрупкие головы-обманки. Настоящий отличается светящимся глазом.",
            new Color(0.72f, 0.89f, 0.32f), UniversalBossAbility.AbilityKind.Impostor,
            marker, false, decoy);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Five editable universal boss prefabs and an impostor decoy baked.");
    }

    public static void RunBatch()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static GameObject BakeEnemy(string name, Sprite sprite, Sprite damageSprite,
        string id, string title, string summary, Color accent,
        UniversalBossAbility.AbilityKind? kind, Sprite marker,
        bool decoy = false, GameObject decoyPrefab = null)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Template);
        try
        {
            root.name = name;
            Brick brick = root.GetComponent<Brick>();
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy + name + ".prefab");
            Brick previousBalance = existing != null ? existing.GetComponent<Brick>() : null;
            Set(brick, "hp", property => property.floatValue = previousBalance != null
                ? previousBalance.hp : decoy ? 1f : 2600f);
            Set(brick, "bossHealth", property => property.boolValue = !decoy);
            Set(brick, "experienceReward", property => property.intValue = decoy ? 0 : 60);
            Set(brick, "footprint", property => property.vector2IntValue = decoy
                ? Vector2Int.one : new Vector2Int(2, 2));
            Set(brick, "leaveFireflyBonfire", property => property.boolValue = true);

            SpriteRenderer face = root.transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            if (face == null) throw new InvalidOperationException("Template has no Visual/SpriteRenderer.");
            face.sprite = sprite;
            face.transform.localScale = Vector3.one * (decoy ? 0.5f : 1.05f);
            Set(root.GetComponent<EnemyHitReaction>(), "damageSprite",
                property => property.objectReferenceValue = damageSprite);
            root.GetComponent<EnemyFirstEncounterProfile>().Configure(id, title, summary, sprite, accent);

            if (decoy)
            {
                root.GetComponent<PolygonCollider2D>().SetPath(0, new[] {
                    new Vector2(-0.38f, -0.45f), new Vector2(0.38f, -0.45f),
                    new Vector2(0.45f, -0.38f), new Vector2(0.45f, 0.38f),
                    new Vector2(0.38f, 0.45f), new Vector2(-0.38f, 0.45f),
                    new Vector2(-0.45f, 0.38f), new Vector2(-0.45f, -0.38f)
                });
            }
            else
            {
                UniversalBossAbility ability = root.AddComponent<UniversalBossAbility>();
                Set(ability, "kind", property => property.enumValueIndex = (int)kind.Value);
                Set(ability, "decoyPrefab", property => property.objectReferenceValue = decoyPrefab);
                if (kind == UniversalBossAbility.AbilityKind.ThreeSealIdol)
                {
                    SpriteRenderer[] seals = {
                        AddMarker(face.transform, marker, "Seal Left", new Vector3(-0.49f, 0.48f, -0.02f)),
                        AddMarker(face.transform, marker, "Seal Centre", new Vector3(0f, 0.57f, -0.02f)),
                        AddMarker(face.transform, marker, "Seal Right", new Vector3(0.49f, 0.48f, -0.02f))
                    };
                    Set(ability, "sealRenderers", property => {
                        property.arraySize = seals.Length;
                        for (int i = 0; i < seals.Length; i++)
                            property.GetArrayElementAtIndex(i).objectReferenceValue = seals[i];
                    });
                }
                if (kind == UniversalBossAbility.AbilityKind.KnotBinder)
                {
                    LineRenderer[] lines = new LineRenderer[2];
                    for (int i = 0; i < lines.Length; i++)
                    {
                        GameObject link = new GameObject("Visible Link " + (i + 1));
                        link.transform.SetParent(root.transform, false);
                        LineRenderer line = link.AddComponent<LineRenderer>();
                        line.sharedMaterial = EnsureLinkMaterial();
                        line.useWorldSpace = true;
                        line.positionCount = 2;
                        line.startWidth = line.endWidth = 0.055f;
                        line.startColor = line.endColor = new Color(0.62f, 0.98f, 0.19f, 0.85f);
                        line.sortingLayerID = face.sortingLayerID;
                        line.sortingOrder = face.sortingOrder - 1;
                        line.enabled = false;
                        lines[i] = line;
                    }
                    Set(ability, "linkLines", property => {
                        property.arraySize = lines.Length;
                        for (int i = 0; i < lines.Length; i++)
                            property.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
                    });
                }
                if (kind == UniversalBossAbility.AbilityKind.Impostor)
                    AddMarker(face.transform, marker, "True Eye", new Vector3(-0.33f, 0.24f, -0.02f));
            }

            string path = Enemy + name + ".prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null) throw new InvalidOperationException("Prefab save failed: " + path);
            return saved;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static SpriteRenderer AddMarker(Transform parent, Sprite marker, string name, Vector3 position)
    {
        GameObject mark = new GameObject(name);
        mark.transform.SetParent(parent, false);
        mark.transform.localPosition = position;
        SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
        renderer.sprite = marker;
        SpriteRenderer face = parent.GetComponent<SpriteRenderer>();
        renderer.sortingLayerID = face.sortingLayerID;
        renderer.sortingOrder = face.sortingOrder + 1;
        return renderer;
    }

    private static Sprite ImportSprite(string name)
    {
        string path = Art + name + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing art: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 600f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + path);
        return sprite;
    }

    private static Sprite EnsureMarker()
    {
        const string path = Art + "BossMarker.png";
        if (!File.Exists(path))
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Abs(x - 31.5f) + Mathf.Abs(y - 31.5f);
                texture.SetPixel(x, y, distance > 30f ? Color.clear
                    : distance > 24f ? new Color(0.04f, 0.09f, 0.03f, 1f)
                    : new Color(0.66f, 0.98f, 0.13f, 1f));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 256f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Material EnsureLinkMaterial()
    {
        const string path = Art + "KnotLink.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Sprites/Default"));
        AssetDatabase.CreateAsset(material, path);
        return material;
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
