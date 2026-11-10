using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FirstLevelTutorialAuthoring
{
    private const string GameplayScene = "Assets/Asset.unity";

    [MenuItem("Forest Animal War/UI/Собрать обучение первого уровня", false, 14)]
    public static void Install()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Обучение можно собирать только вне Play Mode.");
        if (SceneManager.GetActiveScene().path != GameplayScene)
            EditorSceneManager.OpenScene(GameplayScene);

        GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>(true);
        if (manager == null)
            throw new InvalidOperationException("В игровой сцене не найден GameManager.");

        FirstLevelTutorial tutorial = manager.GetComponent<FirstLevelTutorial>();
        if (tutorial == null)
            tutorial = Undo.AddComponent<FirstLevelTutorial>(manager.gameObject);

        tutorial.BuildEditableUI();
        tutorial.PreviewInEditor();
        EditorUtility.SetDirty(tutorial);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = tutorial.TutorialCanvas.gameObject;
        Debug.Log("Editable first-level tutorial installed in Assets/Asset.unity.");
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        try
        {
            EditorSceneManager.OpenScene(GameplayScene);
            Install();
            Debug.Log("First-level tutorial scene authoring completed.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
