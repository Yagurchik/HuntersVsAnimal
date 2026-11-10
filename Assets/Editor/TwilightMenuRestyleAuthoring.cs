using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Authored once into UIscene. Nothing is replaced or created when the game starts.
public static class TwilightMenuRestyleAuthoring
{
    private const string ScenePath = "Assets/Scenes/UIscene.unity";
    private const string ForestArtPath =
        "Assets/LocationArt/TwilightThicket/TwilightThicketMenu_v2.png";
    private const string PanelPath = "Assets/UI/MenuStyle/WoodcutPanel.png";

    [MenuItem("Forest Animal War/UI/Restyle editable campaign menu")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before restyling UIscene.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        MenuHubController hub = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null) throw new InvalidOperationException("Editable menu is missing from UIscene.");

        EnsurePanelSprite();
        Sprite panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
        Texture2D forest = AssetDatabase.LoadAssetAtPath<Texture2D>(ForestArtPath);
        if (panel == null || forest == null)
            throw new InvalidOperationException("Twilight menu artwork was not imported.");

        RawImage illustration = hub.forestIllustration != null
            ? hub.forestIllustration.GetComponent<RawImage>() : null;
        if (illustration == null)
            throw new InvalidOperationException("Forest Illustration is missing from UIscene.");
        illustration.texture = forest;
        illustration.uvRect = new Rect(0f, 0f, 1f, 1f);
        EditorUtility.SetDirty(illustration);

        Transform frame = hub.transform.Find("Safe Area/Portrait Frame");
        Transform header = frame != null ? frame.Find("Header") : null;
        Transform content = hub.tabs[0].page.transform.Find("Viewport/Content");
        if (frame == null || header == null || content == null)
            throw new InvalidOperationException("Editable menu hierarchy is incomplete.");

        StyleImage(frame.Find("Frame Background"), panel, Hex("14221D"));
        EnsureAtmosphere(frame);
        StyleImage(header, panel, Hex("26352B"));
        StyleImage(header.Find("Settings"), panel, Hex("4A3A29"));
        Transform resources = header.Find("Resource Bar");
        if (resources != null)
            foreach (Transform badge in resources)
            {
                StyleImage(badge, panel, Hex("334333"));
                FitResourceBadge(badge);
            }

        Text campaignTitle = content.Find("Title")?.GetComponent<Text>();
        if (campaignTitle != null)
        {
            campaignTitle.fontSize = 50;
            campaignTitle.rectTransform.sizeDelta = new Vector2(992f, 85f);
            EditorUtility.SetDirty(campaignTitle);
            EditorUtility.SetDirty(campaignTitle.rectTransform);
        }

        Transform hero = content.Find("Forest Hero");
        StyleImage(hero, panel, Hex("354736"));
        if (hero.GetComponent<RectMask2D>() is RectMask2D rectangularMask)
            UnityEngine.Object.DestroyImmediate(rectangularMask);
        Mask organicMask = hero.GetComponent<Mask>();
        if (organicMask == null) organicMask = hero.gameObject.AddComponent<Mask>();
        organicMask.showMaskGraphic = true;
        EditorUtility.SetDirty(organicMask);
        StyleImage(hero.Find("Hero Caption"), panel, new Color(0.09f, 0.15f, 0.12f, 0.97f));
        StyleImage(hero.Find("Hero Caption/Previous Location"), panel, Hex("57412C"));
        StyleImage(hero.Find("Hero Caption/Next Location"), panel, Hex("57412C"));
        SetButtonLabel(hub.previousLocationButton, "<");
        SetButtonLabel(hub.nextLocationButton, ">");

        foreach (MenuLevelCard card in hub.levels)
        {
            if (card == null || card.background == null) continue;
            card.background.sprite = panel;
            card.background.type = Image.Type.Sliced;
            card.selectedColor = Hex("785331");
            card.availableColor = Hex("35503B");
            card.lockedColor = Hex("24342D");
            EditorUtility.SetDirty(card.background);
            EditorUtility.SetDirty(card);
        }
        StyleImage(content.Find("Play Selected Level"), panel, Hex("607543"));
        StyleImage(content.Find("Survival Mode Card"), panel, Hex("344735"));
        StyleImage(content.Find("Survival Mode Card/Play Survival"), panel, Hex("526A40"));

        Transform navigation = frame.Find("Bottom Navigation");
        StyleImage(navigation, panel, Hex("203027"));
        hub.activeTabColor = Hex("715333");
        hub.inactiveTabColor = Hex("2A3A2F");
        for (int i = 0; i < hub.tabs.Length; i++)
        {
            Image background = hub.tabs[i].buttonBackground;
            if (background == null) continue;
            background.sprite = panel;
            background.type = Image.Type.Sliced;
            RectTransform rect = background.rectTransform;
            rect.anchoredPosition = new Vector2(i * 216f + 7f, -10f);
            rect.sizeDelta = new Vector2(202f, 136f);
            Transform unsupportedGlyph = background.transform.Find("Icon");
            if (unsupportedGlyph != null) unsupportedGlyph.gameObject.SetActive(false);
            RectTransform label = background.transform.Find("Label") as RectTransform;
            if (label != null)
            {
                label.anchoredPosition = new Vector2(0f, -48f);
                label.sizeDelta = new Vector2(202f, 50f);
                Text text = label.GetComponent<Text>();
                if (text != null) { text.fontSize = 27; EditorUtility.SetDirty(text); }
            }
            RectTransform mark = hub.tabs[i].selectedMark != null
                ? hub.tabs[i].selectedMark.transform as RectTransform : null;
            if (mark != null) mark.anchoredPosition = new Vector2(45f, -3f);
            EditorUtility.SetDirty(background);
            EditorUtility.SetDirty(rect);
        }

        hub.SelectTab(0);
        hub.Refresh();
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Twilight menu illustration and woodcut panels baked into UIscene.");
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static int previewFrames;

    public static void CaptureMenuBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        MenuHubController hub = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
        hub.SelectTab(0);
        hub.Refresh();
        previewFrames = 0;
        EditorApplication.update += CaptureMenuTick;
    }

    private static void CaptureMenuTick()
    {
        try
        {
            Canvas.ForceUpdateCanvases();
            if (++previewFrames < 24) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            EditorApplication.update -= CaptureMenuTick;
            CaptureMenu();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            EditorApplication.update -= CaptureMenuTick;
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void CaptureMenu()
    {
        Camera camera = Camera.main != null ? Camera.main
            : UnityEngine.Object.FindObjectOfType<Camera>();
        if (camera == null) throw new InvalidOperationException("Menu preview needs a camera.");
        foreach (Text label in UnityEngine.Object.FindObjectsOfType<Text>(true))
            if (label.isActiveAndEnabled && label.font != null)
                label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
        MenuHubController hub = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
        Transform content = hub.tabs[0].page.transform.Find("Viewport/Content");
        LogTextLayout("Campaign Title", content.Find("Title")?.GetComponent<Text>());
        Transform badges = hub.transform.Find("Safe Area/Portrait Frame/Header/Resource Bar");
        if (badges != null)
            foreach (Transform badge in badges)
            {
                LogTextLayout(badge.name + " Name", badge.Find("Name")?.GetComponent<Text>());
                LogTextLayout(badge.name + " Amount", badge.Find("Amount")?.GetComponent<Text>());
            }

        Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
        var modes = new RenderMode[canvases.Length];
        var cameras = new Camera[canvases.Length];
        var distances = new float[canvases.Length];
        var render = new RenderTexture(1080, 1920, 24);
        var image = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        try
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                cameras[i] = canvases[i].worldCamera;
                distances[i] = canvases[i].planeDistance;
                if (!canvases[i].isRootCanvas || modes[i] != RenderMode.ScreenSpaceOverlay)
                    continue;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = camera;
                canvases[i].planeDistance = 2f;
            }
            camera.targetTexture = render;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = render;
            image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/TwilightMenuOnly.png", image.EncodeToPNG());
            Debug.Log("Saved Logs/TwilightMenuOnly.png");
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = modes[i];
                canvases[i].worldCamera = cameras[i];
                canvases[i].planeDistance = distances[i];
            }
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(render);
        }
    }

    private static void LogTextLayout(string name, Text label)
    {
        if (label == null) { Debug.LogWarning(name + " text missing"); return; }
        Debug.Log(name + ": text='" + label.text + "', enabled=" + label.isActiveAndEnabled
            + ", rectHeight=" + label.rectTransform.rect.height
            + ", preferredHeight=" + label.preferredHeight
            + ", generatedVertices=" + label.cachedTextGenerator.vertexCount);
    }

    private static void StyleImage(Transform target, Sprite sprite, Color tint)
    {
        if (target == null) return;
        Image image = target.GetComponent<Image>();
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = tint;
        EditorUtility.SetDirty(image);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        Text text = button.GetComponentInChildren<Text>(true);
        if (text == null) return;
        text.text = label;
        EditorUtility.SetDirty(text);
    }

    private static void FitResourceBadge(Transform badge)
    {
        Text name = badge.Find("Name")?.GetComponent<Text>();
        if (name != null)
        {
            name.fontSize = 16;
            name.rectTransform.anchoredPosition = new Vector2(64f, -1f);
            name.rectTransform.sizeDelta = new Vector2(162f, 24f);
            EditorUtility.SetDirty(name);
            EditorUtility.SetDirty(name.rectTransform);
        }
        Text amount = badge.Find("Amount")?.GetComponent<Text>();
        if (amount != null)
        {
            amount.rectTransform.anchoredPosition = new Vector2(64f, -22f);
            amount.rectTransform.sizeDelta = new Vector2(162f, 36f);
            EditorUtility.SetDirty(amount.rectTransform);
        }
        ResourceIconGraphic icon = badge.Find("Icon")?.GetComponent<ResourceIconGraphic>();
        if (icon != null)
        {
            if (icon.GetComponent<CanvasRenderer>() == null)
                icon.gameObject.AddComponent<CanvasRenderer>();
            icon.SetAllDirty();
        }
    }

    private static void EnsureAtmosphere(Transform frame)
    {
        Texture2D exterior = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/LocationArt/TwilightThicket/TwilightExterior.png");
        if (exterior == null) throw new InvalidOperationException("Twilight exterior is missing.");
        Transform existing = frame.Find("Forest Atmosphere");
        if (existing == null)
        {
            var decoration = new GameObject("Forest Atmosphere", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            decoration.transform.SetParent(frame, false);
            existing = decoration.transform;
        }
        RectTransform rect = (RectTransform)existing;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Transform backing = frame.Find("Frame Background");
        rect.SetSiblingIndex(backing != null ? backing.GetSiblingIndex() + 1 : 0);
        RawImage image = existing.GetComponent<RawImage>();
        image.texture = exterior;
        image.color = new Color(0.84f, 0.91f, 0.82f, 0.23f);
        image.raycastTarget = false;
        EditorUtility.SetDirty(rect);
        EditorUtility.SetDirty(image);
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out Color color);
        return color;
    }

    private static void EnsurePanelSprite()
    {
        if (!File.Exists(PanelPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PanelPath));
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 position = new Vector2(x - 47.5f, y - 47.5f);
                Vector2 q = new Vector2(Mathf.Abs(position.x) - 32f,
                    Mathf.Abs(position.y) - 32f);
                float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                float distance = outside + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 14f;
                // Broad, subtle irregularity keeps the silhouette handmade, not noisy.
                distance += 0.85f * Mathf.Sin(y * 0.17f + x * 0.025f)
                    + 0.55f * Mathf.Sin(x * 0.13f - y * 0.04f);
                float alpha = Mathf.Clamp01(0.75f - distance);
                float grain = 0.018f * Mathf.Sin(y * 0.31f + x * 0.012f);
                float shade = distance > -4f ? 0.44f : 0.93f + grain;
                shade *= Mathf.Lerp(0.86f, 1f, y / 95f);
                texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
            texture.Apply();
            File.WriteAllBytes(PanelPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(PanelPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Woodcut panel import failed.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(23f, 23f, 23f, 23f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
