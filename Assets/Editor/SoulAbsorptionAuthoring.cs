using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SoulAbsorptionAuthoring
{
    private const string Folder = "Assets/Resources/Effects";
    private const string TexturePath = Folder + "/SoulGlow.png";
    private const string MaterialPath = Folder + "/SoulGlow.mat";
    private const string PrefabPath = Folder + "/SoulAbsorption.prefab";

    [MenuItem("Forest Animal War/Effects/Bake soul absorption effect")]
    public static void Bake()
    {
        Directory.CreateDirectory(Folder);
        Texture2D glow = EnsureGlowTexture();
        Material material = EnsureMaterial(glow);
        GameObject root = new GameObject("Soul Absorption");
        try
        {
            ParticleSystem wisps = root.AddComponent<ParticleSystem>();
            ConfigureWisps(wisps, material);
            GameObject arrivalObject = new GameObject("Soul Arrival Burst");
            arrivalObject.transform.SetParent(root.transform, false);
            ParticleSystem arrival = arrivalObject.AddComponent<ParticleSystem>();
            ConfigureArrival(arrival, material);

            SoulAbsorptionEffect effect = root.AddComponent<SoulAbsorptionEffect>();
            SerializedObject serialized = new SerializedObject(effect);
            serialized.FindProperty("wisps").objectReferenceValue = wisps;
            serialized.FindProperty("arrivalBurst").objectReferenceValue = arrival;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Soul absorption effect authored at " + PrefabPath);
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use Bake from the editor menu outside batch mode.");
        Bake();
    }

    private static Texture2D EnsureGlowTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (existing != null) return existing;
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.6f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false);
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TexturePath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    private static Material EnsureMaterial(Texture2D glow)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new InvalidOperationException("Sprites/Default shader is missing.");
        Material material = new Material(shader) { name = "Soul Glow", mainTexture = glow };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static void ConfigureWisps(ParticleSystem system, Material material)
    {
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.52f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.64f, 1f, 0.94f, 0.9f), new Color(0.85f, 0.74f, 1f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 48f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = Fade();
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.28f, 1f),
                new Keyframe(1f, 0f)));
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = RoomDarkness.WorldOverlayOrder + 25;
    }

    private static void ConfigureArrival(ParticleSystem system, Material material)
    {
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.55f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.44f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 1f, 0.96f, 0.9f), new Color(0.86f, 0.76f, 1f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.09f;
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = Fade();
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = RoomDarkness.WorldOverlayOrder + 26;
    }

    private static ParticleSystem.MinMaxGradient Fade()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.68f, 0.86f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.17f),
                new GradientAlphaKey(0.68f, 0.65f),
                new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
