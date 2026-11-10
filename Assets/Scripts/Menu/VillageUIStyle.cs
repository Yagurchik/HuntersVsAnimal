using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Shared presentation primitives. No editor-only sprites or stencil textures.
public static class VillageUIStyle
{
    public static readonly Color Background = new Color(0.045f, 0.085f, 0.068f);
    public static readonly Color Panel = new Color(0.10f, 0.17f, 0.13f);
    public static readonly Color Gold = new Color(0.95f, 0.79f, 0.47f);
    public static readonly Color Cream = new Color(0.96f, 0.93f, 0.84f);
    public static readonly Color Muted = new Color(0.64f, 0.73f, 0.65f);
    public static readonly Color Green = new Color(0.25f, 0.40f, 0.28f);

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static void Top(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    public static RectTransform PanelRect(string name, Transform parent, Color color,
        float x, float y, float w, float h)
    {
        RectTransform rect = Rect(name, parent);
        Top(rect, x, y, w, h);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    public static Text Label(string name, Transform parent, string value,
        int size, Color color, float x, float y, float w, float h,
        bool bold = false, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        RectTransform rect = Rect(name, parent);
        Top(rect, x, y, w, h);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = GameTypography.Font;
        label.text = value;
        label.fontSize = size;
        // Kurale has tall ascenders; fit its full line rather than silently
        // dropping a heading or symbol when the authored cell is shorter.
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(12, Mathf.FloorToInt(size * 0.7f));
        label.resizeTextMaxSize = size;
        label.color = color;
        label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        GameTypography.Apply(label);
        return label;
    }

    public static Button Button(string name, Transform parent, string text,
        float x, float y, float w, float h, UnityAction action, int size = 24)
    {
        RectTransform rect = PanelRect(name, parent, Green, x, y, w, h);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.image.raycastTarget = true;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.12f, 0.95f);
        colors.pressedColor = new Color(0.75f, 0.85f, 0.72f);
        colors.disabledColor = new Color(0.8f, 0.85f, 0.8f, 1f);
        button.colors = colors;
        Label("Label", rect, text, size, Cream, 10, 5, w - 20, h - 10,
            true, TextAnchor.MiddleCenter);
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        return button;
    }

    public static RectTransform ScrollPage(Transform page, string name, float height)
    {
        var legacy = page.GetComponent<ScrollRect>();
        if (legacy != null)
        {
            legacy.enabled = false;
        }

        foreach (Transform child in page)
        {
            child.gameObject.SetActive(false);
        }

        RectTransform root = PanelRect(name, page, Background, 0, 0, 1080, height);
        Stretch(root);
        root.GetComponent<Image>().raycastTarget = true;
        RectTransform viewport = Rect("Viewport", root);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, height);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 55f;
        return content;
    }

    public static RectTransform Modal(Transform page, string name, float height, UnityAction close,
        out GameObject overlay)
    {
        RectTransform shade = PanelRect(name, page, new Color(0.015f, 0.025f, 0.02f, 0.93f),
            0, 0, 1080, height);
        Stretch(shade);
        shade.GetComponent<Image>().raycastTarget = true;
        var dismiss = shade.gameObject.AddComponent<Button>();
        dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(close);
        RectTransform panel = PanelRect("Panel", shade, Panel, 0, 0, 980, height);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().raycastTarget = true;
        // Consume clicks inside the panel so they never reach the dismiss button.
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        Button("Close", panel, "×", 884, 18, 70, 62, close, 38);
        overlay = shade.gameObject;
        overlay.SetActive(false);
        return panel;
    }
}
