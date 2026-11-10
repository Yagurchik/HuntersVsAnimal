using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuLevelUI
{
    private const string CanvasName = "Campaign Progress HUD";
    private static readonly List<Image> levelMarkers = new List<Image>();
    private static readonly List<Text> levelNumbers = new List<Text>();
    private static readonly List<Button> levelButtons = new List<Button>();
    private static Text titleText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex != 0 && scene.name != "UIscene")
        {
            return;
        }

        CreateProgressPanel();
    }

    private static void CreateProgressPanel()
    {
        if (Object.FindObjectOfType<MenuHubController>(true) != null)
        {
            return;
        }

        if (GameObject.Find(CanvasName) != null)
        {
            return;
        }

        Font font = GameTypography.Font;
        GameObject canvasObject = new GameObject(
            CanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 1028f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = CreateImage(
            "Campaign Progress",
            canvasObject.GetComponent<RectTransform>(),
            new Color(0.04f, 0.08f, 0.04f, 0.9f));
        SetRect(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(780f, 132f));
        panel.GetComponent<Image>().raycastTarget = false;

        titleText = CreateText("Title", panel, font, 25, FontStyle.Bold);
        titleText.color = new Color(1f, 0.84f, 0.3f, 1f);
        SetStretch(titleText.rectTransform, new Vector2(0f, 0.52f), Vector2.one,
            new Vector2(20f, 0f), new Vector2(-20f, -4f));

        levelMarkers.Clear();
        levelNumbers.Clear();
        levelButtons.Clear();

        RectTransform levelsRoot = new GameObject("Levels", typeof(RectTransform))
            .GetComponent<RectTransform>();
        levelsRoot.SetParent(panel, false);
        SetStretch(levelsRoot, Vector2.zero, new Vector2(1f, 0.55f),
            new Vector2(24f, 12f), new Vector2(-24f, -2f));

        const float markerSize = 48f;
        const float markerGap = 22f;
        float totalWidth = LevelProgression.MaxLevel * markerSize
            + (LevelProgression.MaxLevel - 1) * markerGap;
        float startX = -totalWidth * 0.5f + markerSize * 0.5f;

        for (int level = 1; level <= LevelProgression.MaxLevel; level++)
        {
            RectTransform marker = CreateImage(
                "Level " + level,
                levelsRoot,
                new Color(0.14f, 0.18f, 0.13f, 1f));
            SetRect(marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(startX + (level - 1) * (markerSize + markerGap), 0f),
                Vector2.one * markerSize);
            Image markerImage = marker.GetComponent<Image>();
            markerImage.raycastTarget = true;

            Button button = marker.gameObject.AddComponent<Button>();
            button.targetGraphic = markerImage;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            int capturedLevel = level;
            button.onClick.AddListener(() => SelectLevel(capturedLevel));

            Text number = CreateText("Number", marker, font, 20, FontStyle.Bold);
            number.text = level.ToString();
            SetStretch(number.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            levelMarkers.Add(markerImage);
            levelNumbers.Add(number);
            levelButtons.Add(button);
        }

        RefreshLevelSelection();
    }

    private static void SelectLevel(int levelNumber)
    {
        if (LevelProgression.SelectMenuLevel(levelNumber))
        {
            RefreshLevelSelection();
        }
    }

    private static void RefreshLevelSelection()
    {
        int selectedLevel = LevelProgression.SelectedMenuLevel;
        int highestLevel = LevelProgression.HighestUnlockedLevel;
        titleText.text = "ВЫБРАН УРОВЕНЬ " + selectedLevel
            + "  —  ОТКРЫТО " + highestLevel + " ИЗ " + LevelProgression.MaxLevel;

        for (int i = 0; i < levelMarkers.Count; i++)
        {
            int levelNumber = i + 1;
            bool unlocked = levelNumber <= highestLevel;
            bool selected = levelNumber == selectedLevel;

            levelButtons[i].interactable = unlocked;
            levelMarkers[i].color = selected
                ? new Color(0.95f, 0.62f, 0.12f, 1f)
                : unlocked
                    ? new Color(0.24f, 0.46f, 0.2f, 1f)
                    : new Color(0.14f, 0.18f, 0.13f, 1f);
            levelNumbers[i].color = unlocked
                ? Color.white
                : new Color(0.45f, 0.5f, 0.43f, 1f);
        }
    }

    private static RectTransform CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        imageObject.GetComponent<Image>().color = color;
        return imageObject.GetComponent<RectTransform>();
    }

    private static Text CreateText(string name, RectTransform parent, Font font, int size, FontStyle style)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        GameTypography.Apply(text);
        return text;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void SetStretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
