using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit editor command that authors the clock siege director (used by the
// survival mode) onto GameManager. Nothing here runs on load or in Play mode.
public static class ClockModeInstaller
{
    private const string GameplayScenePath = "Assets/Asset.unity";

    [MenuItem("Forest Animal War/Gameplay/Add clock siege director to GameManager")]
    public static void InstallDirector()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before editing the scene.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameplayScenePath)
            throw new InvalidOperationException("Open Assets/Asset.unity first.");
        GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>(true);
        if (manager == null) throw new InvalidOperationException("GameManager was not found.");
        if (manager.GetComponent<ClockSiegeDirector>() == null)
            Undo.AddComponent<ClockSiegeDirector>(manager.gameObject);
        EditorUtility.SetDirty(manager.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = manager.gameObject;
    }
}
