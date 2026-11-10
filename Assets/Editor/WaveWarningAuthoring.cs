using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WaveWarningAuthoring
{
    private const string GameplayScene = "Assets/Asset.unity";

    [MenuItem("Forest Animal War/UI/Создать предупреждение о волнах")]
    public static void Install()
    {
        if (Application.isPlaying) return;
        if (SceneManager.GetActiveScene().path != GameplayScene)
            EditorSceneManager.OpenScene(GameplayScene);

        DefenseFrontUI front = Object.FindObjectOfType<DefenseFrontUI>(true);
        Transform safeArea = front != null ? front.transform.Find("Safe Area") : null;
        if (safeArea == null)
        {
            Debug.LogError("Defense Front HUD/Safe Area is missing from the gameplay scene.");
            return;
        }
        Transform existing = safeArea.Find("Wave Warning");
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject root = new GameObject("Wave Warning", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(WaveWarningUI));
        root.layer = 5;
        root.transform.SetParent(safeArea, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.10f, 0.812f);
        rect.anchorMax = new Vector2(0.90f, 0.858f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image panel = root.GetComponent<Image>();
        panel.color = new Color(0.22f, 0.08f, 0.055f, 0.95f);
        panel.raycastTarget = false;
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject textObject = new GameObject("Wave Message", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text));
        textObject.layer = 5;
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 2f);
        textRect.offsetMax = new Vector2(-12f, -2f);
        Text message = textObject.GetComponent<Text>();
        message.text = "ЛИНИЯ 1  ·  ВОЛНА 1/5 ПРИБЛИЖАЕТСЯ";
        message.font = GameTypography.Font;
        message.fontSize = 26;
        message.fontStyle = FontStyle.Bold;
        message.alignment = TextAnchor.MiddleCenter;
        message.color = new Color(1f, 0.88f, 0.54f);
        message.raycastTarget = false;
        GameTypography.Apply(message);
        root.GetComponent<WaveWarningUI>().Configure(group, message);
        // The map panel must stay above the warning when opened.
        Transform map = safeArea.Find("Defense Map");
        if (map != null) map.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("Editable wave warning added to the gameplay HUD.");
    }
}
