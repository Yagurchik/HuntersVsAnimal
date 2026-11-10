using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GameplayActionButtonsAuthoring
{
    public const string ActionsName = "Gameplay Actions";

    public static void Apply(AnimalSelectionWheel wheel, DefenseFrontUI front)
    {
        Canvas canvas = wheel.GetComponentInParent<Canvas>();
        Button volley = wheel.AllVolleyButton;
        Button defend = front.DefendRoomButton;
        Button recall = Object.FindObjectsOfType<Button>(true).Single(button =>
            Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                .Any(i => button.onClick.GetPersistentMethodName(i) == "DropDownBalls"));
        if (canvas == null || volley == null || defend == null)
            throw new InvalidOperationException("The authored gameplay action buttons are missing.");
        RectTransform actions = Rect(ActionsName, canvas.transform);
        actions.anchorMin = Vector2.zero; actions.anchorMax = Vector2.one;
        actions.offsetMin = actions.offsetMax = Vector2.zero;
        actions.SetAsLastSibling();
        Pin((RectTransform)volley.transform, actions, new Vector2(1, 0), new Vector2(-24, 24),
            new Vector2(312, 74), new Vector2(1, 0));
        Pin((RectTransform)defend.transform, actions, new Vector2(1, 0), new Vector2(-24, 110),
            new Vector2(312, 84), new Vector2(1, 0));
        StylePanel(volley, 24);
        StylePanel(defend, 22);
        Pin((RectTransform)recall.transform, actions, new Vector2(0.5f, 0), new Vector2(0, 22),
            new Vector2(136, 136), new Vector2(0.5f, 0));
        StyleRecall(recall);
        EditorUtility.SetDirty(wheel); EditorUtility.SetDirty(front);
    }

    private static void StylePanel(Button button, int fontSize)
    {
        Image background = button.GetComponent<Image>();
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/MenuStyle/WoodcutPanel.png");
        background.type = Image.Type.Sliced;
        Outline outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.66f, 0.48f, 0.23f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        Text label = button.GetComponentInChildren<Text>(true);
        label.text = label.text.Replace("\n", " ");
        label.fontSize = fontSize; label.resizeTextForBestFit = false;
        label.fontStyle = FontStyle.Bold; label.color = new Color(1f, 0.91f, 0.69f);
        label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
        Stretch(label.rectTransform, new Vector2(14, 7), new Vector2(-14, -7));
        GameTypography.Apply(label);
        ConfigureFeedback(button, background);
        EditorUtility.SetDirty(label); EditorUtility.SetDirty(outline);
    }

    private static void StyleRecall(Button button)
    {
        Image background = button.GetComponent<Image>();
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/WheelStyle/ForestRecallButton.png");
        if (background.sprite == null) throw new InvalidOperationException("The imagegen recall button sprite is missing.");
        background.type = Image.Type.Simple; background.preserveAspect = true;
        background.color = Color.white;
        Shadow shadow = button.GetComponent<Shadow>() ?? button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.015f, 0.025f, 0.02f, 0.6f);
        shadow.effectDistance = new Vector2(0, -3);
        // The wood, rim and arrow are one authored sprite; the caption remains editable.
        foreach (string oldDecoration in new[] { "Forest Rim", "Return Arrow" })
        {
            Transform existing = button.transform.Find(oldDecoration);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }
        Color cream = new Color(1f, 0.91f, 0.65f, 1f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) throw new InvalidOperationException("The original recall label is missing.");
        label.text = "СБРОС"; label.fontSize = 20; label.enableAutoSizing = false;
        label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
        label.color = cream; label.raycastTarget = false; label.transform.SetAsLastSibling();
        Pin(label.rectTransform, button.transform, Vector2.one * 0.5f, new Vector2(0, -35), new Vector2(92, 26), Vector2.one * 0.5f);
        GameTypography.Apply(label);
        ConfigureFeedback(button, background);
        foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
            if (graphic != background) { graphic.raycastTarget = false; EditorUtility.SetDirty(graphic); }
        EditorUtility.SetDirty(shadow);
    }

    private static void ConfigureFeedback(Button button, Image background)
    {
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1, 0.97f, 0.85f);
        colors.pressedColor = new Color(0.73f, 0.75f, 0.67f);
        colors.selectedColor = Color.white; colors.disabledColor = new Color(0.55f, 0.58f, 0.53f, 0.6f);
        colors.fadeDuration = 0.08f; button.colors = colors;
        EditorUtility.SetDirty(button); EditorUtility.SetDirty(background);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;
        var child = new GameObject(name, typeof(RectTransform)); child.layer = 5;
        child.transform.SetParent(parent, false); return (RectTransform)child.transform;
    }
    private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = minimum; rect.offsetMax = maximum; EditorUtility.SetDirty(rect);
    }
    private static void Pin(RectTransform rect, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity; EditorUtility.SetDirty(rect);
    }
}
