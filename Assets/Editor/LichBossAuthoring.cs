using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;
using AnimatorController = UnityEditor.Animations.AnimatorController;

public static class LichBossAuthoring
{
    public const string SkeletonPath = "Assets/Enemy/Brick.prefab";
    public const string PrefabPath = "Assets/Resources/Enemies/LichBoss.prefab";
    public const string ArtPath = "Assets/MonsterSprite/Lich";

    [MenuItem("Tools/Forest Animal War/Enemies/Prepare Lich Assets")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before preparing enemy prefabs.");
        AssetDatabase.Refresh();
        foreach (string path in Directory.GetFiles(ArtPath + "/Prepared", "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        Sprite[] frames = Enumerable.Range(17, 15).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(
            ArtPath + "/Prepared/frame_" + i.ToString("000") + ".png")).ToArray();
        if (frames.Any(frame => frame == null)) throw new InvalidOperationException("Missing prepared Lich frames.");
        AnimationClip live = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtPath + "/LichLive.anim");
        if (live == null) throw new InvalidOperationException("The user's LichLive clip is missing.");
        // Preserve a subsequently hand-edited animation. The original supplied
        // clip was empty; populate it once without changing its asset GUID.
        if (AnimationUtility.GetObjectReferenceCurveBindings(live).Length == 0
            && AnimationUtility.GetCurveBindings(live).Length == 0)
        {
            live.frameRate = 12f;
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / live.frameRate, value = frames[i % frames.Length] };
            AnimationUtility.SetObjectReferenceCurve(live,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var clipSettings = AnimationUtility.GetAnimationClipSettings(live);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(live, clipSettings);
            EditorUtility.SetDirty(live);
        }
        string controllerPath = ArtPath + "/Lich.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorState idle = controller.layers[0].stateMachine.AddState("LichLive");
            idle.motion = live;
            controller.layers[0].stateMachine.defaultState = idle;
            EditorUtility.SetDirty(controller);
        }
        PrepareSkeletonVisual();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            CreateBoss(frames[0], controller);
        ConfigureSummonedSkeletons();
        AssetDatabase.SaveAssets();
        Debug.Log("Lich assets ready: editable LichBoss prefab, LichLive clip, and skeleton hit reaction.");
    }

    private static void ConfigureSummonedSkeletons()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            LichSummoner summoner = root.GetComponent<LichSummoner>();
            if (summoner == null) summoner = root.AddComponent<LichSummoner>();
            var settings = new SerializedObject(summoner);
            settings.FindProperty("skeletonPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
            settings.FindProperty("summonedSkeletonTint").colorValue =
                new Color(0.78f, 1f, 0.72f, 1f);
            settings.FindProperty("maximumLivingSummons").intValue = 6;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void PrepareSkeletonVisual()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SkeletonPath);
        try
        {
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                visual.gameObject.layer = root.layer;
                SpriteRenderer source = root.GetComponent<SpriteRenderer>();
                if (source != null)
                {
                    ComponentUtility.CopyComponent(source);
                    ComponentUtility.PasteComponentAsNew(visual.gameObject);
                    Object.DestroyImmediate(source);
                }
                Animator animator = root.GetComponent<Animator>();
                if (animator != null)
                {
                    ComponentUtility.CopyComponent(animator);
                    ComponentUtility.PasteComponentAsNew(visual.gameObject);
                    Object.DestroyImmediate(animator);
                }
            }
            SetObject(root.GetComponent<Brick>(), "spriteAnimator", visual.GetComponent<Animator>());
            ConfigureReaction(root, visual, null);
            PrefabUtility.SaveAsPrefabAsset(root, SkeletonPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void CreateBoss(Sprite sprite, RuntimeAnimatorController controller)
    {
        Directory.CreateDirectory("Assets/Resources/Enemies");
        AssetDatabase.Refresh();
        var root = new GameObject("LichBoss");
        root.SetActive(false);
        try
        {
            GameObject skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
            root.layer = skeleton.layer;
            root.tag = "Enemy";
            var visual = new GameObject("Visual");
            visual.layer = root.layer;
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            SpriteRenderer skeletonRenderer = skeleton.GetComponentInChildren<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = skeletonRenderer.sharedMaterial;
            renderer.sortingLayerID = skeletonRenderer.sortingLayerID;
            renderer.sortingOrder = skeletonRenderer.sortingOrder;
            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            visual.AddComponent<EnemyVisualHover>();
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var collider = root.AddComponent<PolygonCollider2D>();
            collider.pathCount = 1;
            collider.SetPath(0, BossOctagonGeometry.CreatePoints(new Vector2(1.9f, 1.9f)));
            collider.sharedMaterial = skeleton.GetComponent<Collider2D>().sharedMaterial;
            Brick brick = root.AddComponent<Brick>();
            GameObject existingBoss = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Brick previousBalance = existingBoss != null ? existingBoss.GetComponent<Brick>() : null;
            brick.hp = previousBalance != null ? previousBalance.hp
                : 4440f;
            brick.dmg = skeleton.GetComponent<Brick>().dmg;
            var settings = new SerializedObject(brick);
            settings.FindProperty("footprint").vector2IntValue = new Vector2Int(2, 2);
            settings.FindProperty("experienceReward").intValue = 80;
            settings.FindProperty("bossHealth").boolValue = true;
            settings.FindProperty("spriteAnimator").objectReferenceValue = animator;
            settings.FindProperty("stepJumpHeight").floatValue = 0f;
            settings.FindProperty("stepStretch").floatValue = 0f;
            settings.FindProperty("landingSquash").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            ConfigureReaction(root, visual.transform,
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "/Prepared/LichDamage.png"));
            LichSummoner summoner = root.AddComponent<LichSummoner>();
            SetObject(summoner, "skeletonPrefab", skeleton);
            var summonSettings = new SerializedObject(summoner);
            summonSettings.FindProperty("maximumLivingSummons").intValue = 6;
            summonSettings.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void ConfigureReaction(GameObject root, Transform visual, Sprite damage)
    {
        EnemyHitReaction reaction = root.GetComponent<EnemyHitReaction>();
        if (reaction == null) reaction = root.AddComponent<EnemyHitReaction>();
        SetObject(reaction, "visualRoot", visual);
        SetObject(reaction, "spriteRenderer", visual.GetComponent<SpriteRenderer>());
        if (damage != null) SetObject(reaction, "damageSprite", damage);
    }

    private static void SetObject(Object target, string name, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(name).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
