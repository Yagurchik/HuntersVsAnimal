using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SurvivalModeInstaller
{
    private const string MenuScenePath = "Assets/Scenes/UIscene.unity";
    private const string GameplayScenePath = "Assets/Asset.unity";

    [MenuItem("Forest Animal War/UI/Add survival result to gameplay scene")]
    public static void InstallGameplayResult()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before installing UI.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameplayScenePath)
            throw new InvalidOperationException("Open Assets/Asset.unity first.");

        LevelTransition transition = UnityEngine.Object.FindObjectOfType<LevelTransition>(true);
        if (transition == null || transition.LosePanel == null)
            throw new InvalidOperationException("The gameplay Lose panel was not found.");

        Transform resultCard = transition.LosePanel.transform.Find("Survival Result");
        Text resultText;
        if (resultCard == null)
        {
            GameObject cardObject = new GameObject("Survival Result", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            cardObject.layer = transition.LosePanel.layer;
            resultCard = cardObject.transform;
            resultCard.SetParent(transition.LosePanel.transform, false);
            RectTransform card = (RectTransform)resultCard;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(0f, 165f);
            card.sizeDelta = new Vector2(760f, 116f);
            Image background = cardObject.GetComponent<Image>();
            background.color = new Color(0.045f, 0.105f, 0.08f, 0.92f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject("Label", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            textObject.layer = cardObject.layer;
            textObject.transform.SetParent(resultCard, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 8f);
            textRect.offsetMax = new Vector2(-18f, -8f);
            resultText = textObject.GetComponent<Text>();
            resultText.font = GameTypography.Font;
            resultText.text = "ВЫ ПРОДЕРЖАЛИСЬ: 0 ХОДОВ\nЛУЧШИЙ РЕЗУЛЬТАТ: 0";
            resultText.fontSize = 31;
            resultText.fontStyle = FontStyle.Bold;
            resultText.alignment = TextAnchor.MiddleCenter;
            resultText.color = new Color(0.96f, 0.8f, 0.47f, 1f);
            resultText.resizeTextForBestFit = true;
            resultText.resizeTextMinSize = 20;
            resultText.resizeTextMaxSize = 31;
            resultText.raycastTarget = false;
            Undo.RegisterCreatedObjectUndo(cardObject, "Add survival result UI");
        }
        else
        {
            resultText = resultCard.Find("Label")?.GetComponent<Text>();
            if (resultText == null)
                throw new InvalidOperationException("Survival Result exists without its Label.");
        }

        SerializedObject serializedTransition = new SerializedObject(transition);
        serializedTransition.FindProperty("survivalResultText").objectReferenceValue = resultText;
        serializedTransition.ApplyModifiedPropertiesWithoutUndo();
        resultCard.gameObject.SetActive(false);
        EditorUtility.SetDirty(transition);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = resultCard.gameObject;
    }

    // Intended for an isolated batch copy during development/validation.
    public static void InstallBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        try
        {
            EditorSceneManager.OpenScene(MenuScenePath);
            MenuHubBuilder.InstallSurvivalControls();
            EditorSceneManager.OpenScene(GameplayScenePath);
            InstallGameplayResult();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
