using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SquirrelEffectsAuthoring
{
    public const string ExplosionArt = "Assets/MonsterSprite/ProjectileArt/Squirrel/acorn-explosion-sheet.png";
    public const string AcornPath = "Assets/AnimalsBullet/SquirrelAcorn.prefab";
    public const string BonfirePath = "Assets/Resources/Effects/Firefly/Bonfire.prefab";

    [MenuItem("Forest Animal War/Art/Обновить взрыв жёлудя и костёр")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new Exception("Stop Play before authoring effects.");
        ImportExplosion();
        var frames = AssetDatabase.LoadAllAssetsAtPath(ExplosionArt).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        if (frames.Length != 16) throw new Exception("Acorn explosion requires sixteen frames.");
        BakeAcorn(AcornPath, frames);
        const string artifactAcorn = "Assets/Resources/Effects/Artifacts/Acorn.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(artifactAcorn) != null) BakeAcorn(artifactAcorn, frames);
        BakeBonfire(); AssetDatabase.SaveAssets();
    }

    private static void BakeAcorn(string path, Sprite[] frames)
    {
        GameObject seed = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var acorn = seed.GetComponent<SquirrelAcorn>();
            Transform effect = seed.transform.Find("Acorn Fire Animation");
            if (effect == null)
            {
                effect = new GameObject("Acorn Fire Animation", typeof(SpriteRenderer)).transform;
                effect.SetParent(seed.transform, false);
            }
            var artwork = effect.GetComponent<SpriteRenderer>();
            artwork.sprite = frames[0]; artwork.color = Color.white; artwork.sortingOrder = 89;
            artwork.enabled = false; effect.localPosition = Vector3.zero; effect.localScale = Vector3.one;
            var data = new SerializedObject(acorn);
            data.FindProperty("blastArtwork").objectReferenceValue = artwork;
            data.FindProperty("blastDuration").floatValue = 0.45f;
            var sequence = data.FindProperty("blastFrames"); sequence.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) sequence.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            var particles = (ParticleSystem)data.FindProperty("fireBurst").objectReferenceValue;
            if (particles != null)
            {
                var main = particles.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
                main.maxParticles = 18;
                var emission = particles.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
                particles.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.06f;
            }
            PrefabUtility.SaveAsPrefabAsset(seed, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(seed); }
    }

    private static void ImportExplosion()
    {
        AssetDatabase.ImportAsset(ExplosionArt, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(ExplosionArt);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int cellWidth = width / 4, cellHeight = height / 4;
        importer.spritePixelsPerUnit = cellWidth / 2.4f;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var frames = new SpriteMetaData[16];
        for (int i = 0; i < frames.Length; i++) frames[i] = new SpriteMetaData {
            name = "AcornExplosion_" + i.ToString("00"), alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f), rect = new Rect(i % 4 * cellWidth, height - (i / 4 + 1) * cellHeight, cellWidth, cellHeight)
        };
        importer.spritesheet = frames; importer.SaveAndReimport();
    }

    private static void BakeBonfire()
    {
        var root = new GameObject("Bonfire", typeof(EnemyIllumination), typeof(FireflyBonfire));
        try
        {
            var visual = new GameObject("Flames", typeof(ParticleSystem)); visual.transform.SetParent(root.transform, false);
            var fire = visual.GetComponent<ParticleSystem>();
            fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fire.main; main.loop = true; main.playOnAwake = false; main.duration = 2.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startColor = Color.white; main.maxParticles = 32; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = fire.emission; emission.rateOverTime = 12;
            var shape = fire.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.18f;
            var velocity = fire.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.22f, 0.46f);
            velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.75f, 0.48f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.16f),
                    new GradientAlphaKey(0.85f, 0.58f), new GradientAlphaKey(0, 1) });
            var colors = fire.colorOverLifetime; colors.enabled = true; colors.color = fade;
            var size = fire.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.32f, 1.15f), new Keyframe(1, 0.16f)));
            var noise = fire.noise; noise.enabled = true; noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.06f; noise.frequency = 0.8f;
            var renderer = fire.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = RoomDarkness.WorldOverlayOrder + 20; renderer.maxParticleSize = 0.08f;
            var data = new SerializedObject(root.GetComponent<FireflyBonfire>());
            data.FindProperty("flames").objectReferenceValue = fire;
            data.FindProperty("fadeDuration").floatValue = 3;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BonfirePath);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
