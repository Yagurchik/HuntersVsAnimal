using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RoomDarknessAppearanceValidation
{
    private const string Running = "RoomDarknessAppearanceValidation.Running";
    private static double deadline;

    static RoomDarknessAppearanceValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 60d;
            EditorApplication.update += Validate;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run in an isolated project copy.");
        Directory.CreateDirectory("Logs");
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(1);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void Validate()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("The campaign scene did not initialize.");
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.Front == null || !manager.Front.IsReady) return;
            manager.enabled = false;
            Camera camera = Camera.main;
            CameraBehaviour focus = Object.FindObjectOfType<CameraBehaviour>();
            Check(camera != null && focus != null, "The room camera is available.");
            for (int line = 2; line <= 4; line++)
            {
                manager.Front.ActivateLine(line);
                LevelGenerator generator = manager.Front.GetGenerator(DefenseFront.IdFor(line, 1));
                Stage room = generator != null ? generator.GetComponentInParent<Stage>() : null;
                Check(room != null, "Line " + line + " has an authored room.");
                if (room.transform.parent != null) room.transform.parent.gameObject.SetActive(true);
                room.gameObject.SetActive(true);
                focus.ForceFocusRoom(room);
                RoomDarkness darkness = room.GetComponent<RoomDarkness>();
                darkness.Refresh();
                BattlefieldForestStyle frame = room.GetComponent<BattlefieldForestStyle>();
                Check(Mathf.Approximately(darkness.EnvironmentOpacity,
                        RoomDarknessTuningAuthoring.OpacityForLine(line))
                    && darkness.Overlay != null && darkness.Overlay.isActiveAndEnabled,
                    "Line " + line + " uses its stronger authored darkness.");
                Check(frame != null && frame.FrameRenderer != null
                    && frame.FrameRenderer.sortingOrder > RoomDarkness.WorldOverlayOrder
                    && frame.BoardEdgeRenderers.All(edge => edge != null
                        && edge.sortingOrder > RoomDarkness.WorldOverlayOrder)
                    && frame.VineRenderer != null
                    && frame.VineRenderer.sortingOrder > RoomDarkness.WorldOverlayOrder,
                    "Line " + line + " keeps its frame in front of fog.");
                ValidatePixels(camera, room, darkness, line);
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void ValidatePixels(Camera camera, Stage room, RoomDarkness darkness, int line)
    {
        GameObject fieldPatch = null;
        GameObject framePatch = null;
        Sprite sprite = null;
        RenderTexture target = null;
        Texture2D image = null;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                Vector2.one * 0.5f, 1f);
            float depth = room.GetComponentInChildren<LevelGenerator>(true).transform.position.z;
            float y = room.transform.position.y + room.BattlefieldSize.y * 0.5f - 1f;
            fieldPatch = CreatePatch("Field darkness probe", sprite,
                new Vector3(room.transform.position.x - 1f, y, depth), 50);
            framePatch = CreatePatch("Foreground frame probe", sprite,
                new Vector3(room.transform.position.x + 1f, y, depth),
                BattlefieldForestStyle.FrameSortingOrder);
            target = new RenderTexture(720, 1280, 24);
            image = new Texture2D(720, 1280, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.GetComponent<CameraRenderer>().FrameRoom(room);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            image.Apply();
            Color field = PixelAt(camera, image, fieldPatch.transform.position);
            Color foreground = PixelAt(camera, image, framePatch.transform.position);
            Check(field.maxColorComponent < 0.55f && foreground.maxColorComponent > 0.85f,
                "Line " + line + " darkens field content but preserves frame-layer brightness ("
                + field + " / " + foreground + ").");
            fieldPatch.SetActive(false);
            framePatch.SetActive(false);
            camera.Render();
            image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/Darkness-Visual-Line" + line + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            if (fieldPatch != null) Object.Destroy(fieldPatch);
            if (framePatch != null) Object.Destroy(framePatch);
            if (sprite != null) Object.Destroy(sprite);
            if (image != null) Object.Destroy(image);
            if (target != null) Object.Destroy(target);
        }
    }

    private static GameObject CreatePatch(string name, Sprite sprite, Vector3 position, int order)
    {
        var patch = new GameObject(name, typeof(SpriteRenderer));
        SpriteRenderer renderer = patch.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        patch.transform.position = position;
        patch.transform.localScale = Vector3.one * 0.3f;
        return patch;
    }

    private static Color PixelAt(Camera camera, Texture2D image, Vector3 world)
    {
        Vector3 pixel = camera.WorldToScreenPoint(world);
        int x = Mathf.Clamp(Mathf.RoundToInt(pixel.x), 0, image.width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(pixel.y), 0, image.height - 1);
        return image.GetPixel(x, y);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Validate;
        SessionState.SetBool(Running, false);
        string result = error == null ? "PASS: lines 2-4 darken the board and keep frames bright."
            : "FAIL: " + error;
        File.WriteAllText("Logs/RoomDarknessAppearanceValidation.txt", result);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
