using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ArtifactEquipmentValidation
{
    private const string Running = "ArtifactEquipmentValidation.Running";
    private static IEnumerator steps;
    private static int lastFrame;
    private static double deadline;
    static ArtifactEquipmentValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            steps = CheckAndCapture(); deadline = EditorApplication.timeSinceStartup + 90;
            lastFrame = -1; EditorApplication.update += Tick;
        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new InvalidOperationException("Use the isolated project.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ArtifactEquipmentValidation.txt", "Six-slot equipment validation\n");
        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        HeroEquipmentAuthoring.Synchronize(hub);
        EditorSceneManager.SaveScene(hub.gameObject.scene);
        Require(HeroEquipmentAuthoring.HasCurrentLayout(hub), "All six slots and sixty backpack cells are authored");
        Require(!HeroEquipmentAuthoring.Synchronize(hub), "Migration is idempotent and preserves later Inspector edits");
        Require(VillageEntityWindowAuthoring.HasCurrentLayout(hub), "Complete village windows remain intact");
        MenuHubValidation.Validate();
        PlayerPrefs.DeleteAll();
        for (int i = 0; i < 14; i++) PlayerPrefs.SetInt("ForestAnimalWar.Village.Artifact." + MetaProgression.GetArtifactDefinition(i).Id, 1);
        for (int i = 0; i < 8; i++) PlayerPrefs.SetString("ForestAnimalWar.Hero.EquipmentSlot." + i, MetaProgression.GetArtifactDefinition(i).Id);
        ArtifactEquipmentBonuses.Invalidate();
        MetaProgression.NormalizeEquipmentCapacity();
        Require(!PlayerPrefs.HasKey("ForestAnimalWar.Hero.EquipmentSlot.6") && !PlayerPrefs.HasKey("ForestAnimalWar.Hero.EquipmentSlot.7"), "Old seventh and eighth slots are retired");
        Require(MetaProgression.OwnedArtifactAmount(6) == 1 && MetaProgression.AvailableArtifactAmount(6) == 1
            && MetaProgression.OwnedArtifactAmount(7) == 1 && MetaProgression.AvailableArtifactAmount(7) == 1, "Retired items remain owned and available in backpack");
        Require(MetaProgression.RoomGoldMultiplier == 1d && Mathf.Approximately(MetaProgression.VictoryAcornMultiplier, 1f), "Retired equipment grants no hidden bonuses");
        Require(MetaProgression.GetFirstEmptyEquipmentSlot() == -1 && !MetaProgression.TryEquipArtifact(6, 6)
            && !MetaProgression.TryEquipArtifact(7, 7), "Six is a hard equipment limit");
        PlayerPrefs.Save();
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    private static IEnumerator CheckAndCapture()
    {
        yield return null; yield return null;
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>();
        hub.SelectTab(1); hub.Refresh();
        Transform content = hub.tabs[1].page.transform.Find("Hero Equipment Runtime/Viewport/Content");
        Require(content.Find("Equipped Count").GetComponent<Text>().text == "НАДЕТО 6 / 6", "Runtime counter shows six equipped items");
        Require(content.Find("Equipment Slot 7") == null && content.GetComponentsInChildren<ArtifactCellView>(true).Length == 66, "Runtime does not recreate removed cells");
        foreach (ArtifactCellView view in content.GetComponentsInChildren<ArtifactCellView>(true))
            Require(view.GetComponent<Button>().image.sprite != null, "Native frame reference: " + view.name);
        yield return null;
        // A selected empty slot and occupied slots must have distinct presentation.
        MetaProgression.TryUnequipArtifact(5); hub.Refresh();
        content.Find("Equipment Slot 6").GetComponent<Button>().onClick.Invoke();
        Require(content.Find("Equipment Slot 6/Selected Edge").GetComponent<Image>().enabled, "Selecting a slot highlights its authored frame");
        yield return null;
        Canvas canvas = hub.GetComponent<Canvas>();
        var cameraObject = new GameObject("Artifact preview camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = VillageUIStyle.Background;
        camera.nearClipPlane = 0.01f;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
        foreach (Vector2Int size in new[] { new Vector2Int(720, 1280), new Vector2Int(960, 540) })
        {
            var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            hub.GetComponentInChildren<MenuDesignFrame>(true).SendMessage("LateUpdate"); Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
            File.WriteAllBytes("Logs/ArtifactEquipment" + size.x + "x" + size.y + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; target.Release();
            Object.Destroy(texture); Object.Destroy(target);
        }
        Finish(null);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        File.AppendAllText("Logs/ArtifactEquipmentValidation.txt", "PASS: " + message + "\n");
    }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try { if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(); steps.MoveNext(); }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false); EditorApplication.update -= Tick;
        File.AppendAllText("Logs/ArtifactEquipmentValidation.txt", error == null ? "RESULT: PASS\n" : "RESULT: FAIL " + error);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
