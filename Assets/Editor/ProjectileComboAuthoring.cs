using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ProjectileComboAuthoring
{
    [MenuItem("Forest Animal War/UI/Добавить счётчик комбо")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || SceneManager.GetActiveScene().path != GameUIPreviewWindow.GameplayPath)
            throw new InvalidOperationException("Открой игровую сцену Assets/Asset.unity вне Play Mode.");
        RemoveBrokenOrDuplicateRoots();
        ProjectileComboUI existing = GameUIPreviewWindow.Find<ProjectileComboUI>();
        ProjectileComboUI ui = ProjectileComboUI.Ensure();
        ui.ApplyRecommendedLayout();
        if (existing == null) Undo.RegisterCreatedObjectUndo(ui.gameObject, "Add projectile combo HUD");
        else Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Preview projectile combo");
        ui.Preview(24, true);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Canvas.ForceUpdateCanvases();
        Selection.activeGameObject = ui.gameObject;
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
        RemoveBrokenOrDuplicateRoots();
        ProjectileComboUI.Ensure().Preview(0, false);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Editable Projectile Combo HUD saved in gameplay scene.");
    }

    public static void RemoveBrokenOrDuplicateRoots()
    {
        ProjectileComboUI keeper = GameUIPreviewWindow.Find<ProjectileComboUI>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "Projectile Combo HUD") continue;
            ProjectileComboUI combo = root.GetComponent<ProjectileComboUI>();
            if (combo == null || (keeper != null && combo != keeper)) Object.DestroyImmediate(root);
            else keeper = combo;
        }
    }
}
