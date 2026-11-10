using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SquirrelUltimateValidation
{
    private const string Running = "ForestAnimalWar.SquirrelUltimateValidation";
    private const string UIPrefab = "Assets/SquirrelUltimateTestUI.prefab";
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static double deadline;
    private static int lastFrame = -1;

    static SquirrelUltimateValidation()
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
            throw new Exception("Run only in the isolated validation project.");
        Directory.CreateDirectory("Logs");
        EditorSceneManager.OpenScene("Assets/SquirrelUpgradeUISource.unity");
        var authored = Object.FindObjectOfType<ExperienceAndUpgradeUI>(true);
        if (authored == null || new SerializedObject(authored).FindProperty("upgradeCards").arraySize != 3)
            throw new Exception("The gameplay scene must contain three authored upgrade cards.");
        PrefabUtility.SaveAsPrefabAsset(authored.gameObject, UIPrefab);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        var player = new GameObject("Player"); player.tag = "Player"; player.transform.position = Vector3.down * 8;
        new GameObject("Input", typeof(EventSystem), typeof(StandaloneInputModule));
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = Vector3.back * 10; camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.04f, 0.09f, 0.065f);
        var queueObject = new GameObject("Queue"); queueObject.SetActive(false);
        var queue = queueObject.AddComponent<AnimalProjectileQueue>(); queue.enabled = false;
        var entries = new List<AnimalProjectileQueue.AnimalProjectileEntry>();
        var squirrel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Squirrel.prefab");
        for (int i = 0; i < AnimalProjectileQueue.AnimalTypeCount; i++)
        {
            var entry = new AnimalProjectileQueue.AnimalProjectileEntry();
            Set(entry, "prefab", i == 7 ? squirrel : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/BaseBullet.prefab"));
            Set(entry, "displayName", PauseDamageUI.AnimalNames[i]); entries.Add(entry);
        }
        Set(queue, "animals", entries); queueObject.SetActive(true);
        Require(queue.ConfigureRunLoadout(new[] { 0, 1, 2, 6, 7 }, 1), "Squirrel belongs to an actual five-type run loadout");
        Require(!OffersUltimate(queue) && !queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.SquirrelUltimate),
            "Locked squirrel cannot offer or select its ultimate");
        for (int i = 0; i < 3; i++)
        {
            Require(queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.Ammo), "Actual squirrel quantity upgrade " + (i + 1));
            Require(!OffersUltimate(queue), "Quantity alone does not unlock chain reaction");
        }
        for (int i = 0; i < 3; i++)
        {
            Require(queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.SquirrelDetonation), "Actual early explosion upgrade " + (i + 1));
            Require(!OffersUltimate(queue), "Missing vine ranks still block chain reaction");
        }
        for (int i = 0; i < 2; i++) Require(queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.SquirrelBinding), "Actual vine upgrade " + (i + 1));
        Require(!OffersUltimate(queue) && !queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.SquirrelUltimate),
            "Two of three vine ranks cannot select the ultimate");
        Require(queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.SquirrelBinding), "Final required branch upgrade succeeds");
        foreach (string branch in new[] { "ammoUpgradeLevel", "squirrelDetonationUpgradeLevel", "squirrelBindingUpgradeLevel" })
        {
            Set(entries[7], branch, 2);
            Require(!OffersUltimate(queue), "Every branch must be complete: " + branch);
            Set(entries[7], branch, 3);
        }
        var offer = queue.GetAvailableUpgradeOptions().Single(item => item.Kind == AnimalProjectileQueue.UpgradeKind.SquirrelUltimate);
        Require(offer.AnimalIndex == 7 && offer.Title.Contains("цепная реакция") && offer.Description.StartsWith("УЛЬТА:") && offer.Icon != null,
            "Fully upgraded squirrel offers its named ultimate with an icon and effect description");

        var room = new GameObject("Chain Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        var otherRoom = new GameObject("Other Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        var seedPrefab = squirrel.GetComponent<SquirrelProjectile>().AcornPrefab;
        var a = Target(room, Vector2.zero); var b = Target(room, Vector2.right); var c = Target(room, Vector2.right * 2);
        var other = Target(otherRoom, Vector2.right);
        var seeds = new[] { a, b, c, other }.Select(host => SquirrelAcorn.Plant(host, seedPrefab, 0.2f, 3, false, 0)).ToArray();
        Require(seeds.All(seed => seed != null && !(bool)Get(seed, "chainReaction")), "Previously planted acorns start without chain reaction");

        var managerObject = new GameObject("Manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<GameManager>(); Set(manager, "pendingProjectileUpgrades", 1);
        typeof(GameManager).GetProperty("Instance").SetValue(null, manager);
        var uiObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UIPrefab));
        var ui = uiObject.GetComponent<ExperienceAndUpgradeUI>(); uiObject.SetActive(true);
        var savedButtons = ui.ChoicesRoot.GetComponentsInChildren<Button>(true).Select(button => button.GetInstanceID()).ToArray();
        Require(savedButtons.Length == 3, "Test uses the three saved gameplay-scene cards");
        Require(queue.ForceNextUpgradeOption(7, AnimalProjectileQueue.UpgradeKind.SquirrelUltimate), "Existing offer can be displayed deterministically for a real UI click");
        Require((bool)typeof(GameManager).GetMethod("OpenUpgradeChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null),
            "Actual game manager opens the upgrade choice");
        for (int i = 0; i < 20; i++) yield return null;
        var card = ((IList)Get(ui, "upgradeCards")).Cast<object>().Single(item => ((Button)Get(item, "button")).gameObject.activeInHierarchy);
        var button = (Button)Get(card, "button");
        Require((AnimalProjectileQueue.UpgradeKind)Get(card, "upgradeKind") == AnimalProjectileQueue.UpgradeKind.SquirrelUltimate
            && ((Text)Get(card, "chooseLabel")).text == "ПРИНЯТЬ УЛЬТУ"
            && ((Text)Get(card, "kindLabel")).text.Contains("УЛЬТИМАТИВНАЯ")
            && button.GetComponent<UltimateUpgradeCardGlow>().enabled,
            "Native squirrel card is styled as an ultimate and exposes its accept button");
        Require(savedButtons.SequenceEqual(ui.ChoicesRoot.GetComponentsInChildren<Button>(true).Select(value => value.GetInstanceID())),
            "Displaying the ultimate retains all authored card objects");
        Require(manager.IsChoosingUpgrade && manager.PendingProjectileUpgrades == 1 && Time.timeScale == 0,
            "Ultimate selection uses the ordinary pending-upgrade and pause flow");
        foreach (Canvas canvas in uiObject.GetComponentsInChildren<Canvas>(true))
            if (canvas.isRootCanvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases(); for (int i = 0; i < 3; i++) yield return null;
        Capture(camera, "Logs/SquirrelUltimateUpgrade.png");
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "Visible ultimate card receives the actual UI raycast");
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Require(queue.HasSquirrelUltimate(7) && manager.PendingProjectileUpgrades == 0 && !manager.IsChoosingUpgrade
            && !ui.UpgradeModal.activeSelf && Time.timeScale > 0, "Clicking the saved card grants chain reaction and closes the choice normally");
        Require(!OffersUltimate(queue) && !queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.SquirrelUltimate), "Selected squirrel ultimate is never offered twice");
        Require(ProjectileUpgradeSummary.ForAnimal(queue, 7).Single(item => item.Ultimate).Title == "Цепная реакция",
            "The chosen ultimate appears in the pause upgrade summary");
        Require(seeds.All(seed => (bool)Get(seed, "chainReaction")), "Selecting the ultimate updates already planted acorns");
        seeds[0].Detonate();
        float chainLimit = Time.time + 3f;
        while (SquirrelAcorn.HasActiveBlasts && Time.time < chainLimit) yield return null;
        Require(seeds.Take(3).All(seed => seed.IsDetonated) && a.hp > 0 && b.hp > 0 && c.hp > 0,
            "Chain reaction detonates neighbouring acorns even when all hosts survive");
        Require(!SquirrelAcorn.HasActiveBlasts && a.hp == 9800 && b.hp == 9700 && c.hp == 9800,
            "All three pulses of every chained explosion finish with their correct total damage");
        Require(!seeds[3].IsDetonated && other.hp == 10000, "Chain reaction cannot cross into another room");
        foreach (SquirrelAcorn seed in Object.FindObjectsOfType<SquirrelAcorn>()) Object.Destroy(seed.gameObject);
        Object.Destroy(uiObject); typeof(GameManager).GetProperty("Instance").SetValue(null, null);
        Time.timeScale = 1;
    }

    private static bool OffersUltimate(AnimalProjectileQueue queue) => queue.GetAvailableUpgradeOptions()
        .Any(item => item.AnimalIndex == 7 && item.Kind == AnimalProjectileQueue.UpgradeKind.SquirrelUltimate);
    private static Brick Target(LevelGenerator room, Vector2 position)
    {
        var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/SwampDemon.prefab"),
            position, Quaternion.identity, room.transform).GetComponent<Brick>();
        enemy.levelGenerator = room; enemy.hp = 10000; room.EnemyCollection.Add(enemy.gameObject); return enemy;
    }
    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Require(bool success, string text) { if (!success) throw new Exception(text); Report.Add("PASS " + text); }
    private static void Capture(Camera camera, string path)
    {
        var render = new RenderTexture(540, 960, 24); render.Create(); camera.targetTexture = render; camera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(540, 960, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(image); render.Release(); Object.Destroy(render);
    }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Squirrel ultimate validation timed out.");
            while (Steps.Count > 0)
            {
                var current = Steps.Peek(); if (!current.MoveNext()) { Steps.Pop(); continue; }
                if (current.Current is IEnumerator child) { Steps.Push(child); continue; } return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false);
        string result = error == null ? "SQUIRREL_ULTIMATE_PASSED" : "FAIL " + error;
        Report.Add(result); File.WriteAllLines("Logs/SquirrelUltimateValidation.txt", Report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
