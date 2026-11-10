using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class SquirrelProjectileAuthoring
{
    private const string Art = "Assets/MonsterSprite/ProjectileArt/Squirrel/";
    private const string Prefabs = "Assets/AnimalsBullet/";

    [MenuItem("Forest Animal War/Projectiles/Bake squirrel")]
    public static void Bake()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "BaseBullet.prefab");
        Vector3 ordinarySpriteSize = source.GetComponent<SpriteRenderer>().sprite.bounds.size;
        ImportSingle(Art + "squirrel.png", Mathf.Max(ordinarySpriteSize.x, ordinarySpriteSize.y));
        ImportSingle(Art + "acorn.png");
        ImportVines();
        var vines = new GameObject("Squirrel Vine Binding");
        try
        {
            var binding = vines.AddComponent<SquirrelVineBinding>();
            var visual = new GameObject("Animated Vines"); visual.transform.SetParent(vines.transform, false);
            var sprite = visual.AddComponent<SpriteRenderer>(); sprite.sortingOrder = 86;
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Art + "vine-binding-sheet.png")
                .OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (frames.Length != 8) throw new Exception("Vine sheet must contain eight authored frames.");
            sprite.sprite = frames[0];
            var data = new SerializedObject(binding);
            data.FindProperty("artwork").objectReferenceValue = sprite;
            var sequence = data.FindProperty("frames"); sequence.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) sequence.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(vines, Prefabs + "SquirrelVineBinding.prefab");
        }
        finally { Object.DestroyImmediate(vines); }

        var seedObject = new GameObject("Squirrel Acorn");
        try
        {
            var seed = seedObject.AddComponent<SquirrelAcorn>();
            var visual = new GameObject("Acorn"); visual.transform.SetParent(seedObject.transform, false);
            var sprite = visual.AddComponent<SpriteRenderer>(); sprite.sortingOrder = 87;
            sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "acorn.png");
            var burstObject = new GameObject("Acorn Fire Burst"); burstObject.transform.SetParent(seedObject.transform, false);
            var fire = burstObject.AddComponent<ParticleSystem>();
            fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fire.main; main.loop = false; main.playOnAwake = false; main.duration = 0.45f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.28f);
            main.startColor = new Color(1f, 0.65f, 0.2f, 1f); main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = fire.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = fire.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.1f;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            var colors = fire.colorOverLifetime; colors.enabled = true; colors.color = fade;
            var fireRenderer = fire.GetComponent<ParticleSystemRenderer>();
            fireRenderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            fireRenderer.sortingOrder = 88; fireRenderer.maxParticleSize = 0.06f;
            var ringObject = new GameObject("Acorn Explosion"); ringObject.transform.SetParent(seedObject.transform, false);
            var ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
            ring.widthMultiplier = 0.075f; ring.sortingOrder = 88;
            ring.numCornerVertices = 3; ring.numCapVertices = 3;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
            }
            ring.enabled = false;
            var data = new SerializedObject(seed);
            data.FindProperty("artwork").objectReferenceValue = sprite;
            data.FindProperty("fireBurst").objectReferenceValue = fire;
            data.FindProperty("blastRing").objectReferenceValue = ring;
            data.FindProperty("bindingPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SquirrelVineBinding>(Prefabs + "SquirrelVineBinding.prefab");
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(seedObject, Prefabs + "SquirrelAcorn.prefab");
        }
        finally { Object.DestroyImmediate(seedObject); }

        SquirrelEffectsAuthoring.Bake();
        var projectile = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            PrefabUtility.UnpackPrefabInstance(projectile, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            projectile.name = "Squirrel"; projectile.transform.position = Vector3.zero;
            foreach (FireflyImpactEffect fire in projectile.GetComponentsInChildren<FireflyImpactEffect>()) Object.DestroyImmediate(fire);
            Transform circle = projectile.transform.Find("Circle"); if (circle != null) Object.DestroyImmediate(circle.gameObject);
            var sprite = projectile.GetComponent<SpriteRenderer>();
            sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "squirrel.png"); sprite.color = Color.white;
            sprite.sortingOrder = 80;
            projectile.GetComponent<Animal>().dmg = source.GetComponent<Animal>().dmg;
            var squirrel = projectile.AddComponent<SquirrelProjectile>();
            var data = new SerializedObject(squirrel);
            data.FindProperty("acornPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SquirrelAcorn>(Prefabs + "SquirrelAcorn.prefab");
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(projectile, Prefabs + "Squirrel.prefab");
        }
        finally { Object.DestroyImmediate(projectile); }

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        AddChoice(Object.FindObjectOfType<ProjectileLoadoutUI>(true));
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("SQUIRREL_AUTHORING_PASSED");
    }

    private static void ImportSingle(string path, float worldSize = 1f)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        importer.spritePixelsPerUnit = Mathf.Max(width, height) / Mathf.Max(0.01f, worldSize);
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void ImportVines()
    {
        string path = Art + "vine-binding-sheet.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int cellWidth = width / 4, cellHeight = height / 2;
        importer.spritePixelsPerUnit = cellWidth;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var frames = new SpriteMetaData[8];
        for (int i = 0; i < frames.Length; i++) frames[i] = new SpriteMetaData {
            name = "Vines_" + i, alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f),
            rect = new Rect(i % 4 * cellWidth, height - (i / 4 + 1) * cellHeight, cellWidth, cellHeight)
        };
        importer.spritesheet = frames; importer.SaveAndReimport();
    }

    private static void AddChoice(ProjectileLoadoutUI dialog)
    {
        if (dialog == null) throw new Exception("Authored loadout menu is missing.");
        var data = new SerializedObject(dialog); var choices = data.FindProperty("choices");
        if (choices.arraySize == 7)
        {
            var previous = choices.GetArrayElementAtIndex(6);
            var button = (Button)previous.FindPropertyRelative("button").objectReferenceValue;
            var copy = Object.Instantiate(button.gameObject, button.transform.parent); copy.name = "Squirrel Choice";
            choices.arraySize = AnimalProjectileQueue.AnimalTypeCount;
            var choice = choices.GetArrayElementAtIndex(AnimalProjectileQueue.SquirrelIndex);
            foreach (string field in new[] { "button", "background", "icon", "label", "status" })
            {
                var original = (Component)previous.FindPropertyRelative(field).objectReferenceValue;
                string path = AnimationUtility.CalculateTransformPath(original.transform, button.transform);
                Transform target = string.IsNullOrEmpty(path) ? copy.transform : copy.transform.Find(path);
                choice.FindPropertyRelative(field).objectReferenceValue = target.GetComponent(original.GetType());
            }
        }
        if (choices.arraySize != 8) throw new Exception("Expected eight loadout choices.");
        for (int i = 0; i < choices.arraySize; i++)
        {
            var choice = choices.GetArrayElementAtIndex(i);
            var rect = ((Button)choice.FindPropertyRelative("button").objectReferenceValue).GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(i % 2 == 0 ? -203 : 203, 166 - i / 2 * 144);
            rect.sizeDelta = new Vector2(388, 132);
            if (i != AnimalProjectileQueue.SquirrelIndex) continue;
            ((Text)choice.FindPropertyRelative("label").objectReferenceValue).text = "Белка";
            ((Image)choice.FindPropertyRelative("icon").objectReferenceValue).sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "squirrel.png");
            ((Text)choice.FindPropertyRelative("status").objectReferenceValue).text = "ВЫБРАТЬ";
        }
        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(dialog);
    }
}
