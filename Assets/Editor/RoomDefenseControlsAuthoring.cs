using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RoomDefenseControlsAuthoring
{
    private const string GameplayScene = "Assets/Asset.unity";
    private const string PanelPath = "Assets/UI/MenuStyle/WoodcutPanel.png";

    [MenuItem("Forest Animal War/UI/Разместить залп и защиту комнаты")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing the gameplay HUD.");
        if (SceneManager.GetActiveScene().path != GameplayScene)
            EditorSceneManager.OpenScene(GameplayScene);

        AnimalSelectionWheel wheel = UnityEngine.Object.FindObjectOfType<AnimalSelectionWheel>(true);
        DefenseFrontUI front = UnityEngine.Object.FindObjectOfType<DefenseFrontUI>(true);
        Transform volley = wheel != null && wheel.AllVolleyButton != null ? wheel.AllVolleyButton.transform : null;
        Transform safeArea = front != null ? front.transform.Find("Safe Area") : null;
        Transform warning = safeArea != null ? safeArea.Find("Wave Warning") : null;
        if (volley == null || safeArea == null || warning == null)
            throw new InvalidOperationException("The authored wheel or defense HUD is missing.");

        RectTransform warningRect = warning.GetComponent<RectTransform>();
        warningRect.anchorMin = new Vector2(0.08f, 0.812f);
        warningRect.anchorMax = new Vector2(0.74f, 0.858f);
        EditorUtility.SetDirty(warningRect);

        Transform existing = front.DefendRoomButton != null ? front.DefendRoomButton.transform : safeArea.Find("Defend Room Button");
        if (existing == null)
        {
            GameObject buttonObject = new GameObject("Defend Room Button",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.layer = 5;
            buttonObject.transform.SetParent(safeArea, false);
            existing = buttonObject.transform;

            GameObject labelObject = new GameObject("Label",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.layer = 5;
            labelObject.transform.SetParent(existing, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(4f, 2f);
            labelRect.offsetMax = new Vector2(-4f, -2f);
            Text label = labelObject.GetComponent<Text>();
            label.font = GameTypography.Font;
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.9f, 0.65f);
            label.raycastTarget = false;
            label.text = "ЗАЩИЩАТЬ\nКОМНАТУ";
            GameTypography.Apply(label);
        }

        RectTransform rect = existing.GetComponent<RectTransform>();
        Image background = existing.GetComponent<Image>();
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
        background.type = background.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = new Color(0.15f, 0.30f, 0.22f, 1f);
        Button button = existing.GetComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        Text buttonLabel = existing.Find("Label")?.GetComponent<Text>();
        if (buttonLabel == null)
            throw new InvalidOperationException("The defend-room button has no label.");
        Transform map = safeArea.Find("Defense Map");
        if (map != null) map.SetAsLastSibling();
        front.ConfigureDefendRoomButton(button, background, buttonLabel);
        GameplayActionButtonsAuthoring.Apply(wheel, front);
        EditorUtility.SetDirty(front);
        EditorUtility.SetDirty(rect);
        EditorUtility.SetDirty(background);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Selection.activeGameObject = existing.gameObject;
        Debug.Log("The volley button and editable room-defense control were saved in the gameplay scene.");
    }
}
