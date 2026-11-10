using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class GameTypographyInstaller
{
    private const string SourceFontPath = "Assets/Resources/Fonts/Kurale-Regular.ttf";
    private const string TmpFontPath = "Assets/Resources/Fonts/Kurale SDF.asset";
    private static readonly string[] GameScenes = {
        "Assets/Scenes/UIscene.unity",
        "Assets/Asset.unity"
    };

    private const string RequiredCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
        "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" +
        "абвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
        "ІіЎў–—…№.,:;!?+-/%()[]{}<>·'\" ";

    [MenuItem("Forest Animal War/UI/Применить сказочный шрифт везде", false, 12)]
    public static void ApplyEverywhere()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before applying typography.");

        Font legacyFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (legacyFont == null) throw new FileNotFoundException("Missing game font", SourceFontPath);
        TMP_FontAsset tmpFont = EnsureTmpFont(legacyFont);

        string openScene = SceneManager.GetActiveScene().path;
        int legacyCount = 0;
        int tmpCount = 0;
        foreach (string scenePath in GameScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                ApplyToRoot(root, legacyFont, tmpFont, ref legacyCount, ref tmpCount);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        foreach (string prefabPath in FindGameUiPrefabs())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                int beforeLegacy = legacyCount;
                int beforeTmp = tmpCount;
                ApplyToRoot(root, legacyFont, tmpFont, ref legacyCount, ref tmpCount);
                if (legacyCount != beforeLegacy || tmpCount != beforeTmp)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        if (!Application.isBatchMode && !string.IsNullOrEmpty(openScene) && File.Exists(openScene))
            EditorSceneManager.OpenScene(openScene, OpenSceneMode.Single);
        Debug.Log($"Applied Kurale typography to {legacyCount} UI Text and {tmpCount} TMP labels.");
    }

    public static void RunBatch()
    {
        ApplyEverywhere();
        EditorApplication.Exit(0);
    }

    private static TMP_FontAsset EnsureTmpFont(Font sourceFont)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
        if (existing != null)
        {
            existing.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            existing.isMultiAtlasTexturesEnabled = true;
            return existing;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        if (fontAsset == null) throw new InvalidOperationException("Unable to create Kurale TMP font asset.");
        fontAsset.name = "Kurale SDF";
        AssetDatabase.CreateAsset(fontAsset, TmpFontPath);
        if (fontAsset.atlasTextures != null)
            foreach (Texture2D texture in fontAsset.atlasTextures)
                if (texture != null && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, fontAsset);
        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        fontAsset.TryAddCharacters(RequiredCharacters, out string missingCharacters);
        if (!string.IsNullOrEmpty(missingCharacters))
            Debug.LogWarning("Kurale is missing requested characters: " + missingCharacters);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    private static void ApplyToRoot(GameObject root, Font legacyFont, TMP_FontAsset tmpFont,
        ref int legacyCount, ref int tmpCount)
    {
        foreach (Text label in root.GetComponentsInChildren<Text>(true))
        {
            if (label.font == legacyFont) continue;
            label.font = legacyFont;
            EditorUtility.SetDirty(label);
            legacyCount++;
        }
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.GetComponentInParent<EnemyHealthBarView>(true) != null) continue;
            if (label.font == tmpFont) continue;
            label.font = tmpFont;
            EditorUtility.SetDirty(label);
            tmpCount++;
        }
    }

    private static IEnumerable<string> FindGameUiPrefabs()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !path.StartsWith("Assets/TextMesh Pro/", StringComparison.Ordinal)
                && !path.StartsWith("Assets/_Heathen Engineering/", StringComparison.Ordinal))
            .Where(path => PrefabContainsText(path));
    }

    private static bool PrefabContainsText(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab != null && (prefab.GetComponentInChildren<Text>(true) != null
            || prefab.GetComponentInChildren<TMP_Text>(true) != null);
    }
}
