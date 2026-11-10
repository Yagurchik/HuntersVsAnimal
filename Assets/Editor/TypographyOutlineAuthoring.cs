using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TypographyOutlineAuthoring
{
    private static readonly string[] GameScenes = {
        "Assets/Scenes/UIscene.unity",
        "Assets/Asset.unity"
    };

    [MenuItem("Forest Animal War/UI/Bake black text outlines into game assets")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before baking typography.");
        int sceneLabels = 0;
        foreach (string path in GameScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                count += ApplyToHierarchy(root);
            sceneLabels += count;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Outlined " + count + " saved labels in " + path);
        }

        if (GameTypography.TmpFont != null && GameTypography.TmpFont.material != null)
            EditorUtility.SetDirty(GameTypography.TmpFont.material);

        int prefabLabels = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
            if (path.StartsWith("Assets/TextMesh Pro/", StringComparison.Ordinal)) continue;
            if (path.StartsWith("Assets/_Heathen Engineering/", StringComparison.Ordinal)) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = ApplyToHierarchy(root);
                if (count == 0) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                prefabLabels += count;
                Debug.Log("Outlined " + count + " prefab labels in " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Black typography outline bake passed: " + sceneLabels
            + " scene labels, " + prefabLabels + " prefab labels.");
    }

    private static int ApplyToHierarchy(GameObject root)
    {
        int count = 0;
        foreach (Text label in root.GetComponentsInChildren<Text>(true))
        {
            GameTypography.Apply(label);
            EditorUtility.SetDirty(label);
            EditorUtility.SetDirty(label.GetComponent<Outline>());
            count++;
        }
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            GameTypography.Apply(label);
            EditorUtility.SetDirty(label);
            count++;
        }
        return count;
    }
}
