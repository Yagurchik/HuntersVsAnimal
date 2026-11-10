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
public static class GameplayActionButtonsPreview
{
    private const string Running = "ForestAnimalWar.GameplayActionButtonsPreview";
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static double deadline;
    private static int lastFrame = -1;

    static GameplayActionButtonsPreview()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Steps.Clear(); Steps.Push(Checks()); deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run only in the isolated validation project.");
        Directory.CreateDirectory("Logs"); Report.Clear();
        RoomDefenseControlsAuthoring.Install();
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        var wheel = Object.FindObjectOfType<AnimalSelectionWheel>(true);
        var front = Object.FindObjectOfType<DefenseFrontUI>(true);
        if (wheel.AllVolleyButton.transform.parent.name != GameplayActionButtonsAuthoring.ActionsName
            || front.DefendRoomButton.transform.parent != wheel.AllVolleyButton.transform.parent)
            throw new Exception("The action layout did not survive saving the scene.");
        GameRunMode.SetDeveloperMode(false); GameRunMode.PrepareCampaign(); LevelProgression.PrepareLevel(2);
        AnimalProjectileQueue.PrepareRunLoadout(new[] { 0, 1, 2, 6, 7 }, 1);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    private static IEnumerator Checks()
    {
        yield return Frames(5);
        GameManager manager = GameManager.Instance; manager.enabled = false;
        var wheel = Object.FindObjectOfType<AnimalSelectionWheel>(true);
        Button volley = wheel.AllVolleyButton, defend = manager.Front.Interface.DefendRoomButton;
        Button recall = Object.FindObjectsOfType<Button>(true).Single(button =>
            Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                .Any(i => button.onClick.GetPersistentMethodName(i) == "DropDownBalls"));
        Transform actions = volley.transform.parent;
        Require(actions.name == GameplayActionButtonsAuthoring.ActionsName && defend.transform.parent == actions
            && recall.transform.parent == actions, "All three controls use the saved shared HUD layout in Play Mode");
        Require(recall.onClick.GetPersistentEventCount() == 4
            && Enumerable.Range(0, 4).All(i => recall.onClick.GetPersistentTarget(i) is Player),
            "Recall retains the four original scene callbacks and their player references");
        Require(AssetDatabase.GetAssetPath(recall.GetComponent<Image>().sprite) == "Assets/UI/WheelStyle/ForestRecallButton.png"
            && recall.transform.Find("Forest Rim") == null && recall.transform.Find("Return Arrow") == null
            && recall.GetComponentsInChildren<Graphic>().Where(graphic => graphic.gameObject != recall.gameObject)
                .All(graphic => !graphic.raycastTarget), "Recall uses its saved imagegen wood sprite and its caption does not intercept clicks");
        var queue = AnimalProjectileQueue.Instance;
        var before = ((RectTransform)volley.transform).anchoredPosition;
        queue.ReloadAll(); yield return Frames(2);
        Require(((RectTransform)volley.transform).anchoredPosition == before && volley.transform.parent == actions,
            "Updating the wheel preserves the new volley position");
        Require(manager.CanSwitchRooms && !manager.Front.IsDefendingRoom, "Actual battle is ready for room protection");
        Click(defend); Require(manager.Front.IsDefendingRoom, "Moved defense button enables room protection");
        Click(defend); Require(!manager.Front.IsDefendingRoom, "Moved defense button also disables room protection");
        foreach (GameAnnouncementUI notice in Object.FindObjectsOfType<GameAnnouncementUI>(true)) notice.gameObject.SetActive(false);
        foreach (EnemyFirstEncounterUI notice in Object.FindObjectsOfType<EnemyFirstEncounterUI>(true)) notice.gameObject.SetActive(false);
        Click(volley); Require(queue.AllVolleyEnabled, "Moved volley button enables the combined volley");
        yield return Capture(volley, defend, recall, "Logs/GameplayActionsPortrait.png", 540, 960);
        Click(volley); Require(!queue.AllVolleyEnabled, "Moved volley button disables the combined volley");
        yield return Capture(volley, defend, recall, "Logs/GameplayActionsLandscape.png", 960, 540);
        Player player = Object.FindObjectsOfType<Player>().First(value => value.isActiveAndEnabled);
        var bullet = Object.Instantiate(queue.Animals[0].Prefab, player.transform.position + Vector3.up, Quaternion.identity)
            .GetComponent<Animal>();
        bullet.SetReturnTarget(player.gameObject); bullet.GetComponent<ProjectilePhysics2D>().Launch(Vector2.up, 5);
        yield return Frames(1);
        Click(recall);
        Require(bullet.IsReturning && bullet.GetComponent<Rigidbody2D>().velocity.y < 0,
            "Restyled recall button sends a real flying projectile down");
        Object.Destroy(bullet.gameObject);
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases(); Canvas canvas = button.GetComponentInParent<Canvas>();
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(button.interactable && hits.Count > 0
            && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "Visible control receives the actual UI click: " + button.name);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static IEnumerator Capture(Button volley, Button defend, Button recall, string path, int width, int height)
    {
        Screen.SetResolution(width, height, false); Camera camera = Camera.main;
        var render = new RenderTexture(width, height, 24); render.Create(); camera.targetTexture = render;
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>(true))
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        yield return Frames(5); Canvas.ForceUpdateCanvases();
        Rect a = ScreenRect(volley, camera), b = ScreenRect(defend, camera), c = ScreenRect(recall, camera);
        Require(a.xMin > width * 0.5f && b.xMin > width * 0.5f && a.yMin > 0 && b.yMax < height * 0.3f
            && a.xMax < width && b.xMax < width && !a.Overlaps(b) && !a.Overlaps(c) && !b.Overlaps(c),
            "Actions fit the lower-right corner without overlap at " + width + "x" + height);
        camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(image); render.Release(); Object.Destroy(render);
    }
    private static Rect ScreenRect(Button button, Camera camera)
    {
        var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
        Vector2 minimum = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 maximum = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static void Require(bool success, string message) { if (!success) throw new Exception(message); Report.Add("PASS " + message); }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Gameplay button preview timed out.");
            while (Steps.Count > 0)
            {
                IEnumerator current = Steps.Peek(); if (!current.MoveNext()) { Steps.Pop(); continue; }
                if (current.Current is IEnumerator child) { Steps.Push(child); continue; } return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false);
        Report.Add(error == null ? "GAMEPLAY_ACTION_BUTTONS_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/GameplayActionButtonsPreview.txt", Report);
        if (error != null) Debug.LogException(error); else Debug.Log("GAMEPLAY_ACTION_BUTTONS_PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
