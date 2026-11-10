using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MenuEncounterRuntimeValidation
{
    private const string Running = "MenuEncounterRuntimeValidation.Running";
    private static int frames;
    private static int lastFrame = -1;
    private static double deadline;

    static MenuEncounterRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            frames = 0;
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new InvalidOperationException("Run only in an isolated project copy.");
        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        if (++frames < 6) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Menu Play Mode did not render in time.");
            MenuHubController hub = Object.FindObjectOfType<MenuHubController>();
            if (hub == null || hub.SelectedTab != 0)
                throw new InvalidOperationException("Campaign menu is not active in Play Mode.");
            Transform overlay = hub.tabs[0].page.transform.Find(
                "Viewport/Content/Forest Hero/Level Enemy Preview");
            if (overlay == null || !overlay.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Enemy preview is hidden in Play Mode.");
            int visible = 0;
            foreach (Image portrait in hub.encounterPortraits)
                if (portrait != null && portrait.gameObject.activeInHierarchy
                    && portrait.sprite != null) visible++;
            if (visible < 6)
                throw new InvalidOperationException("Only " + visible + " enemy portraits are visible.");
            Capture(hub);
            Text title = hub.encounterTitle;
            var corners = new Vector3[4];
            title.rectTransform.GetWorldCorners(corners);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/MenuEncounterRuntimeValidation.txt",
                "PASS: " + visible + " enemy portraits visible in Play Mode; selected level "
                + LevelProgression.SelectedMenuLevel + ".\nTitle: " + title.text
                + "; enabled=" + title.enabled + "; active=" + title.gameObject.activeInHierarchy
                + "; cull=" + title.canvasRenderer.cull + "; preferredWidth="
                + title.preferredWidth + "; corners=" + corners[0] + " / " + corners[2]);
            Debug.Log("MENU_ENCOUNTER_RUNTIME_PASSED: " + visible + " visible portraits.");
            Finish(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Finish(1);
        }
    }

    private static void Capture(MenuHubController hub)
    {
        const int width = 1080;
        const int height = 1920;
        Canvas canvas = hub.GetComponent<Canvas>();
        var cameraObject = new GameObject("Encounter runtime camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(width, height, 24);
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        RenderTexture oldActive = RenderTexture.active;
        try
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/MenuEncounterRuntime.png", image.EncodeToPNG());
            Object.Destroy(image);
        }
        finally
        {
            RenderTexture.active = oldActive;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(cameraObject);
        }
    }

    private static void Finish(int exitCode)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        EditorApplication.Exit(exitCode);
    }
}
