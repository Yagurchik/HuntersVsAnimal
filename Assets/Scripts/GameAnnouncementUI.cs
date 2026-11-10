using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameAnnouncementUI : MonoBehaviour
{
    private static GameAnnouncementUI instance;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text messageText;
    private Coroutine showCoroutine;

    public static void Show(string message, float duration = 2.5f)
    {
        GameAnnouncementUI announcement = GetOrCreate();
        announcement.ShowMessage(message, duration);
    }

    public static GameAnnouncementUI GetOrCreate()
    {
        if (instance == null) instance = FindObjectOfType<GameAnnouncementUI>(true);
        if (instance != null)
        {
            Canvas hud = instance.GetComponentInParent<Canvas>(true);
            if (hud != null) hud.transform.localScale = Vector3.one;
            return instance;
        }

        GameObject canvasObject = new GameObject(
            "Game Announcement HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 1028f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject(
            "Announcement",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(GameAnnouncementUI));

        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(900f, 150f);

        Image panel = panelObject.GetComponent<Image>();
        panel.color = new Color(0.05f, 0.08f, 0.05f, 0.88f);
        panel.raycastTarget = false;

        instance = panelObject.GetComponent<GameAnnouncementUI>();
        instance.canvasGroup = panelObject.GetComponent<CanvasGroup>();
        instance.messageText = CreateText(panelRect);
        instance.canvasGroup.alpha = 0f;
        return instance;
    }

    private void Awake()
    {
        instance = this;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void Preview(string message)
    {
        if (Application.isPlaying) return;
        GetComponentInParent<Canvas>(true).transform.localScale = Vector3.one;
        messageText.text = message;
        canvasGroup.alpha = string.IsNullOrEmpty(message) ? 0f : 1f;
        canvasGroup.blocksRaycasts = false;
    }

    private static Text CreateText(RectTransform parent)
    {
        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));

        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 15f);
        textRect.offsetMax = new Vector2(-30f, -15f);

        Text text = textObject.GetComponent<Text>();
        text.font = GameTypography.Font;
        text.fontSize = 38;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.86f, 0.35f, 1f);
        text.raycastTarget = false;
        GameTypography.Apply(text);
        return text;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void ShowMessage(string message, float duration)
    {
        if (showCoroutine != null)
        {
            StopCoroutine(showCoroutine);
        }

        messageText.text = message;
        showCoroutine = StartCoroutine(ShowRoutine(Mathf.Max(0.5f, duration)));
    }

    private IEnumerator ShowRoutine(float duration)
    {
        yield return Fade(0f, 1f, 0.2f);
        yield return new WaitForSecondsRealtime(duration);
        yield return Fade(1f, 0f, 0.45f);
        showCoroutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }
}
