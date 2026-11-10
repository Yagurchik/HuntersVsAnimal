using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VillageEntityWindowValidation
{
    private const string Running = "ForestAnimalWar.VillageEntityWindowValidation.Running";
    private const string ReportPath = "Logs/VillageEntityWindowValidation.txt";
    private static readonly List<string> Report = new List<string>();
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;

    static VillageEntityWindowValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Report.Clear(); Report.AddRange(File.ReadAllLines(ReportPath));
            Steps.Clear(); Steps.Push(PlayChecks());
            lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch() => RunValidation(true);

    public static void RunSavedSceneBatch() => RunValidation(false);

    private static void RunValidation(bool bake)
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation"
            || Application.productName != "VillageEntityValidation")
            throw new InvalidOperationException("Use the isolated village validation project.");
        try
        {
            Directory.CreateDirectory("Logs");
            Report.Clear();
            byte[] sceneBefore = File.ReadAllBytes(VillageEntityWindowAuthoring.MenuPath);
            EditorSceneManager.OpenScene(VillageEntityWindowAuthoring.MenuPath);
            if (bake)
            {
                VillageEntityWindowAuthoring.Bake();
                EditorSceneManager.OpenScene(VillageEntityWindowAuthoring.MenuPath);
            }
            MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
            Require(VillageEntityWindowAuthoring.HasCurrentLayout(hub), "Saved scene already contains the complete migrated village");
            Transform field = Field(hub);
            Require(hub.tabs.Length == 4 && field.childCount == 4, "Four menu tabs and four entity windows are authored");
            Require(field.GetComponentsInChildren<RawImage>(true).Length == 0
                && field.Find("Clearing Background") == null, "The house map and landscape image have been removed");
            for (int i = 0; i < 4; i++)
            {
                Transform window = field.Find(VillageEntityWindowAuthoring.InstanceNames[i]);
                string prefabPath = VillageEntityWindowAuthoring.PrefabFolder + "/"
                    + VillageEntityWindowAuthoring.PrefabNames[i] + ".prefab";
                Require(window != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(window.gameObject) == prefabPath,
                    "Editable prefab instance " + window.name);
                Image portrait = window.Find("Character").GetComponent<Image>();
                Require(portrait.sprite != null && !portrait.raycastTarget
                    && window.GetComponent<Button>().targetGraphic == window.GetComponent<Image>(),
                    "Whole window is a service button; the portrait cannot intercept clicks");
                string art = AssetDatabase.GetAssetPath(portrait.sprite);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(art);
                Require(importer.textureType == TextureImporterType.Sprite && importer.alphaIsTransparency
                    && !importer.mipmapEnabled && importer.maxTextureSize == 1024,
                    "Portrait has a bounded mobile import and genuine alpha");
                Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                original.LoadImage(File.ReadAllBytes(art));
                Require(original.GetPixel(0, 0).a == 0f, "Portrait corner is transparent: " + window.name);
                Object.DestroyImmediate(original);
                Require(window.GetComponentsInChildren<Component>(true).All(component => component != null),
                    "Window contains no missing script references");
            }
            RectTransform tree = (RectTransform)field.Find("GreatTreeBuilding");
            Require(tree.sizeDelta.y / 1920f > 0.23f && tree.sizeDelta.y / 1920f < 0.25f,
                "Eternal Tree occupies approximately the upper quarter of the portrait screen");
            MenuHubValidation.Validate();
            if (!bake) Require(sceneBefore.SequenceEqual(File.ReadAllBytes(VillageEntityWindowAuthoring.MenuPath)),
                "Saved-scene validation performed no bake, migration or scene-file write");
            File.WriteAllLines(ReportPath, Report);
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt("ForestAnimalWar.Meta.Gold", 180);
            PlayerPrefs.SetInt("ForestAnimalWar.Meta.Acorns", 36);
            PlayerPrefs.SetInt("ForestAnimalWar.Meta.Seeds", 8);
            PlayerPrefs.SetInt("ForestAnimalWar.Meta.Diamonds", 60);
            PlayerPrefs.SetString("ForestAnimalWar.Village.PassiveTimestampUtc", DateTime.UtcNow.AddMinutes(-45).Ticks.ToString());
            PlayerPrefs.Save();
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(error); }
    }

    private static Transform Field(MenuHubController hub) => hub.tabs[2].page.transform
        .Find("Forest Village Runtime/Viewport/Content/Village Field");

    private static IEnumerator PlayChecks()
    {
        yield return Frames(5);
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>();
        Require(hub != null, "Native menu starts in Play mode");
        hub.SelectTab(2);
        yield return Frames(3);
        Transform page = hub.tabs[2].page.transform;
        Transform field = Field(hub);
        Click(field.Find("GreatTreeBuilding").GetComponent<Button>());
        yield return Frames(1);
        Require(hub.IsDialogOpen && page.Find("Building Menu").gameObject.activeSelf
            && page.Find("Building Menu/Panel/Title").GetComponent<Text>().text.StartsWith("Вечное древо")
            && page.Find("Building Menu/Panel/Collect Income").gameObject.activeSelf,
            "Eternal Tree opens the existing income and upgrade window");
        hub.CloseDialogs();
        Click(field.Find("Open Skin Shop").GetComponent<Button>());
        yield return Frames(1);
        ProjectileSkinShopUI skins = page.Find("Projectile Skin Shop").GetComponent<ProjectileSkinShopUI>();
        Require(hub.IsDialogOpen && skins.IsOpen && skins.Cards.Length == 8
            && skins.Cards.All(card => card.Preview.sprite != null), "Chameleon opens all eight projectile skins");
        hub.CloseDialogs();
        Click(field.Find("ForestShopBuilding").GetComponent<Button>());
        yield return Frames(1);
        Require(hub.IsDialogOpen && page.Find("Building Menu/Panel/Title").GetComponent<Text>().text.StartsWith("Лесная лавка")
            && page.Find("Building Menu/Panel/Shop Offers").gameObject.activeSelf,
            "Raven opens the existing Forest Shop and its artifact offers");
        hub.CloseDialogs();
        Click(field.Find("Open Bestiary").GetComponent<Button>());
        yield return Frames(1);
        Require(hub.IsDialogOpen && page.Find("Bestiary Menu").GetComponent<BestiaryUI>().IsOpen
            && page.GetComponentsInChildren<BestiaryEntryView>(true).Length > 30,
            "Owl opens the complete existing bestiary");
        hub.CloseDialogs();
        Require(!hub.IsDialogOpen && field.childCount == 4, "All dialogs close without rebuilding or duplicating the village");
        yield return Capture(hub, 720, 1280, "VillageEntitiesPortrait");
        yield return Capture(hub, 960, 540, "VillageEntitiesLandscape");
        Finish(null);
    }

    private static IEnumerator Capture(MenuHubController hub, int width, int height, string name)
    {
        Canvas canvas = hub.GetComponent<Canvas>();
        GameObject cameraObject = new GameObject("Village entity preview camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = VillageUIStyle.Background;
        camera.nearClipPlane = 0.01f;
        RenderTexture target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        yield return Frames(3);
        Canvas.ForceUpdateCanvases();
        hub.GetComponentInChildren<MenuDesignFrame>(true).SendMessage("LateUpdate");
        Canvas.ForceUpdateCanvases();
        RectTransform page = (RectTransform)hub.tabs[2].page.transform;
        Vector3[] corners = new Vector3[4];
        foreach (Transform child in Field(hub))
        {
            ((RectTransform)child).GetWorldCorners(corners);
            Require(corners.All(corner => InsidePage(page, corner)),
                child.name + " stays inside the visible page at " + width + "x" + height);
        }
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        capture.Apply();
        File.WriteAllBytes("Logs/" + name + ".png", capture.EncodeToPNG());
        Require(capture.GetPixel(width / 2, height / 2).maxColorComponent > 0.05f, "Native UI renders into " + name);
        RenderTexture.active = previous;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(capture); Object.Destroy(target); Object.Destroy(cameraObject);
        yield return Frames(2);
    }

    private static void Click(Button button)
    {
        Require(button != null && button.interactable, "Service window accepts input");
        button.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
    }
    private static bool InsidePage(RectTransform page, Vector3 worldPoint)
    {
        // Rect.Contains excludes the top/right edge, but an aligned window is
        // visible there. Permit the boundary and subpixel layout rounding.
        Vector3 point = page.InverseTransformPoint(worldPoint);
        return point.x >= page.rect.xMin - 0.5f && point.x <= page.rect.xMax + 0.5f
            && point.y >= page.rect.yMin - 0.5f && point.y <= page.rect.yMax + 0.5f;
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }

    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Village play checks timed out.");
            while (Steps.Count > 0)
            {
                IEnumerator step = Steps.Peek();
                if (!step.MoveNext()) { Steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { Steps.Push(nested); continue; }
                break;
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Report.Add("PASS: " + message);
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Report.Add(error == null ? "RESULT: PASS (" + Report.Count + " checks)" : "RESULT: FAIL " + error);
        File.WriteAllLines(ReportPath, Report);
        if (error != null) Debug.LogException(error); else Debug.Log(Report.Last());
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
