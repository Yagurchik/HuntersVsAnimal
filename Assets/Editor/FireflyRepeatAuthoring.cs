using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FireflyRepeatAuthoring
{
    private static readonly string[] EnemyPrefabs = {
        "Assets/Enemy/Brick.prefab",
        "Assets/Resources/Enemies/LichBoss.prefab"
    };

    [MenuItem("Tools/Forest Animal War/Enemies/Prepare burn tint")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before updating enemy prefabs.");
        foreach (string path in EnemyPrefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<EnemyBurnTint>() == null) root.AddComponent<EnemyBurnTint>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Editable three-stack firefly tint added to enemy prefabs.");
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        Install();
        EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
        ProjectileComboAuthoring.RemoveBrokenOrDuplicateRoots();
        ProjectileComboUI combo = ProjectileComboUI.Ensure();
        combo.ApplyRecommendedLayout();
        combo.Preview(0, false);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Firefly repeat-fire update and editable combo HUD saved.");
        EditorApplication.Exit(0);
    }
}
