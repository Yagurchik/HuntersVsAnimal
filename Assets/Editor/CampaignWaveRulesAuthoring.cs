using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CampaignWaveRulesAuthoring
{
    private const string GameplayScene = "Assets/Asset.unity";

    [MenuItem("Forest Animal War/Balance/Применить правила пяти волн", false, 21)]
    public static void Install()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Баланс волн можно менять только вне Play Mode.");
        if (SceneManager.GetActiveScene().path != GameplayScene)
            EditorSceneManager.OpenScene(GameplayScene);

        LevelProgression progression = UnityEngine.Object.FindObjectOfType<LevelProgression>(true);
        GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>(true);
        if (progression == null || manager == null)
            throw new InvalidOperationException("Gameplay scene is missing progression or game manager.");

        progression.ApplyCampaignWaveRules();
        SerializedObject progressionData = new SerializedObject(progression);
        progressionData.FindProperty("enemyHealthLevelSpan").floatValue = LevelProgression.DefaultEnemyHealthLevelSpan;
        progressionData.FindProperty("enemyHealthLevelExponent").floatValue = LevelProgression.DefaultEnemyHealthLevelExponent;
        progressionData.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(progression);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = progression.gameObject;
        Debug.Log("Five-wave budgets and the diminishing enemy health curve were applied.");
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        try
        {
            Install();
            Debug.Log("Campaign wave rules authoring completed.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
