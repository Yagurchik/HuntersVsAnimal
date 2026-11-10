using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class UIAuthoringValidation
{
    private const string Running = "ForestAnimalWar.UIAuthoringValidation.Running";
    private const string SavedPrefs = "ForestAnimalWar.UIAuthoringValidation.Prefs";
    private static readonly string[] Keys = {
        "ForestAnimalWar.Meta.Gold", "ForestAnimalWar.Meta.Acorns", "ForestAnimalWar.Meta.Victories",
        "ForestAnimalWar.CurrentLevel", "ForestAnimalWar.HighestUnlockedLevel",
        "ForestAnimalWar.MenuSelectedLevel", "ForestAnimalWar.RequestedLevel"
    };
    private static readonly List<string> report = new List<string>();
    private static IEnumerator routine;
    private static double deadline;
    private static int lastFrame = -1;
    private static readonly Stack<IEnumerator> captureStack = new Stack<IEnumerator>();

    static UIAuthoringValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            report.Clear();
            report.AddRange(SessionState.GetString(Running + ".Report", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            routine = RuntimeChecks();
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        SessionState.SetString(SavedPrefs, Prefs());
        SessionState.SetBool(Running, true);
        Directory.CreateDirectory("Logs");
        try
        {
            EditorSceneManager.OpenScene(GameUIPreviewWindow.MenuPath);
            var hub = GameUIPreviewWindow.Find<MenuHubController>();
            hub.Refresh();
            Check(AssetDatabase.GetAssetPath(GameTypography.Font) ==
                "Assets/Resources/Fonts/Kurale-Regular.ttf",
                "The global readable fairytale font is available");
            Check(GameTypography.Font.HasCharacter('Ж') && GameTypography.Font.HasCharacter('ё'),
                "The global font contains Russian Cyrillic glyphs");
            Check(SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Text>(true))
                .All(label => label.font == GameTypography.Font),
                "Every main-menu UI Text uses the global font");
            Check(SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                .All(label => label.font == GameTypography.TmpFont),
                "Every main-menu TMP label uses the global font");
            Check(hub.levels.Count(c => c != null && c.gameObject.activeSelf) == 5, "Menu preview shows five levels");
            for (int tab = 0; tab < hub.tabs.Length; tab++)
            {
                hub.SelectTab(tab);
                Check(hub.tabs.Count(t => t.page.activeSelf) == 1, "Menu preview selects exactly one page " + tab);
            }
            hub.PrepareVillageAndHero();
            hub.SelectTab(1);
            Check(hub.tabs[1].page.transform.Find("Hero Equipment Runtime") != null,
                "Hero equipment preview replaces permanent animal upgrades");
            hub.ShowSettings();
            Check(hub.settingsModal.activeSelf, "Settings preview opens");
            hub.CloseDialogs();
            hub.SelectTab(0);
            Check(Prefs() == SessionState.GetString(SavedPrefs, ""), "Menu previews preserve saved progress");

            EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
            GameUIPreviewWindow.InstallGameplay();
            AnimalProjectileQueue queue = GameUIPreviewWindow.Find<AnimalProjectileQueue>();
            string queueBefore = EditorJsonUtility.ToJson(queue);
            var wheel = GameUIPreviewWindow.Find<AnimalSelectionWheel>();
            var xp = GameUIPreviewWindow.Find<ExperienceAndUpgradeUI>();
            GameplayBackdropUI backdrop = GameUIPreviewWindow.Find<GameplayBackdropUI>();
            Check(backdrop != null && Object.FindObjectsOfType<GameplayBackdropUI>(true).Length == 1,
                "Gameplay uses one editable pumpkin backdrop");
            Check(Camera.main != null && Vector4.Distance(Camera.main.backgroundColor,
                GameplayBackdropUI.CameraColor) < 0.001f,
                "Gameplay camera uses the dark pumpkin background color");
            Check(backdrop.GetComponent<Canvas>().sortingOrder <
                wheel.GetComponentInParent<Canvas>().sortingOrder,
                "Pumpkin pattern stays behind gameplay HUD");
            Check(!backdrop.GetComponentInChildren<PumpkinBackdropGraphic>(true).raycastTarget,
                "Pumpkin pattern never blocks aiming or UI input");
            Check(SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Text>(true))
                .All(label => label.font == GameTypography.Font),
                "Every gameplay UI Text uses the global font");
            Check(SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                .All(label => label.font == GameTypography.TmpFont),
                "Every gameplay TMP label uses the global font");
            Check(queue.Animals.Take(3).All(entry => entry.HasCustomIcon
                && AssetDatabase.GetAssetPath(entry.GetIcon()).EndsWith("-v2-simple.png")),
                "Projectile wheel uses simple dedicated animal icons");
            Transform obsoleteWheelRim = wheel.transform.Find("Wheel Rim");
            Check(obsoleteWheelRim == null || !obsoleteWheelRim.gameObject.activeSelf,
                "Oversized golden wheel rim is disabled");
            Check(wheel.AllVolleyButton != null,
                "Editable gameplay HUD contains the combined-volley toggle");
            int[] objectIds = wheel.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).ToArray();
            int[] cardIds = xp.GetComponentsInChildren<Button>(true).Select(b => b.GetInstanceID()).ToArray();
            Check(cardIds.Length == Mathf.Min(ExperienceAndUpgradeUI.MaximumOfferedUpgrades,
                queue.GetAvailableUpgradeOptions().Count),
                "Exactly three random-offer card slots are authored in the scene");
            Check(xp.ChoicesRoot != null && Mathf.Approximately(
                xp.ChoicesRoot.anchorMax.y - xp.ChoicesRoot.anchorMin.y, 0.67f),
                "Vertical cards occupy about two thirds of the central screen");
            Canvas upgradeCanvas = xp.UpgradeModal.GetComponent<Canvas>();
            Check(upgradeCanvas != null && upgradeCanvas.overrideSorting
                && upgradeCanvas.sortingOrder > GameUIPreviewWindow.Find<DefenseFrontUI>()
                    .GetComponent<Canvas>().sortingOrder,
                "Upgrade choice renders above the room map and ordinary gameplay HUD");
            Canvas.ForceUpdateCanvases();
            Check(xp.GetComponentsInChildren<Button>(true).All(button =>
                ((RectTransform)button.transform).rect.height > ((RectTransform)button.transform).rect.width),
                "All projectile choices use tall vertical cards");
            Check(xp.GetComponentsInChildren<Image>(true).Count(image =>
                image.name == "Animal Portrait" && image.sprite != null
                && AssetDatabase.GetAssetPath(image.sprite).EndsWith("-v1.png")) == cardIds.Length,
                "Upgrade cards use the detailed animal artwork");
            Image cardBackground = xp.GetComponentsInChildren<Button>(true)[0]
                .transform.Find("Card Surface").GetComponent<Image>();
            Color oldColor = cardBackground.color;
            Color authoredColor = new Color(0.21f, 0.43f, 0.57f);
            cardBackground.color = authoredColor;
            GameUIPreviewWindow.InstallGameplay();
            Check(wheel.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).SequenceEqual(objectIds),
                "Repeated installation keeps every wheel object");
            Check(xp.GetComponentsInChildren<Button>(true).Select(b => b.GetInstanceID()).SequenceEqual(cardIds),
                "Repeated installation keeps every upgrade card");
            foreach (string mode in new[] { "hud", "map", "upgrade", "announcement", "health", "pause", "win", "lose" })
            {
                GameUIPreviewWindow.ShowGameplay(mode, 1, 4, 1, true);
                Check(xp.UpgradeModal.activeSelf == (mode == "upgrade"), mode + ": correct upgrade modal visibility");
                Check(GameUIPreviewWindow.Find<DefenseFrontUI>().IsMapOpen == (mode == "map"), mode + ": correct map visibility");
                Check(cardBackground.color == authoredColor, mode + ": authored card colors survive preview");
                Check(EditorJsonUtility.ToJson(queue) == queueBefore, mode + ": preview does not unlock animals or change ammunition");
            }
            Check(wheel.GetComponentsInChildren<Button>(true).All(b => b.gameObject.activeSelf && b.GetComponent<CanvasGroup>().alpha > 0f),
                "All-animal preview visibly includes rabbit as well as firefly and hedgehog");
            Check(Object.FindObjectsOfType<Graphic>(true).All(g => g.GetComponent<CanvasRenderer>() != null),
                "Every editable UI graphic has CanvasRenderer");
            Check(Prefs() == SessionState.GetString(SavedPrefs, ""), "Gameplay previews preserve saved progress");
            cardBackground.color = oldColor;
            GameUIPreviewWindow.ShowGameplay("hud", 1, 1, 1, false);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
            Check(GameUIPreviewWindow.Find<ExperienceAndUpgradeUI>().UpgradeModal != null,
                "Upgrade modal references survive saving and reloading the scene");
            GameUIPreviewWindow.ShowGameplay("upgrade", 1, 4, 1, true);
            SessionState.SetString(Running + ".Report", string.Join("\n", report));
            LevelProgression.PrepareLevel(1);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(error); }
    }

    private static IEnumerator RuntimeChecks()
    {
        yield return null;
        yield return null;
        var manager = GameManager.Instance;
        var queue = AnimalProjectileQueue.Instance;
        var xp = GameUIPreviewWindow.Find<ExperienceAndUpgradeUI>();
        Check(Object.FindObjectsOfType<AnimalSelectionWheel>(true).Length == 1, "Play reuses the saved wheel without duplicates");
        Check(Object.FindObjectsOfType<ExperienceAndUpgradeUI>(true).Length == 1, "Play reuses the saved experience HUD");
        Check(Object.FindObjectsOfType<GameAnnouncementUI>(true).Length == 1, "Play reuses the saved announcement");
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas))
            Check(canvas.transform.lossyScale.x > 0.001f && canvas.transform.lossyScale.y > 0.001f,
                canvas.name + ": authored canvas has nonzero visible scale in Play");
        Check(!xp.UpgradeModal.activeSelf && !manager.IsChoosingUpgrade, "Editor upgrade preview is closed at the start of play");
        Check(queue.Animals.Count == 5 && queue.Animals[0].RemainingAmmo == 3
            && !queue.Animals[1].IsUnlocked && !queue.Animals[2].IsUnlocked
            && !queue.Animals[3].IsUnlocked && !queue.Animals[4].IsUnlocked,
            "All-animal preview does not leak into starting gameplay");
        Check(manager.Front.CurrentRoomId == 0 && manager.Front.GetGenerator(0).HasEnemies,
            "Saved outer-room preview still starts a real battle in the main room");
        LevelTransition transition = GameUIPreviewWindow.Find<LevelTransition>();
        transition.PauseButtonPressed();
        Canvas pauseCanvas = transition.PausePanel.GetComponent<Canvas>();
        Check(pauseCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay && pauseCanvas.overrideSorting
            && pauseCanvas.sortingOrder > xp.GetComponent<Canvas>().sortingOrder,
            "Pause is a screen overlay above the field darkness and gameplay HUD");
        transition.ContinueButtonPressed();
        int wheelChildren = GameUIPreviewWindow.Find<AnimalSelectionWheel>().transform.childCount;
        Button[] cards = xp.UpgradeModal.GetComponentsInChildren<Button>(true);
        int cardId = cards[0].GetInstanceID();
        manager.RegisterEnemyDefeated(manager.ExperienceForNextUpgrade);
        queue.ClearAllAmmo();
        manager.stepPlayer = false;
        manager.TryCompletePlayerTurn();
        Check(manager.IsChoosingUpgrade && xp.UpgradeModal.activeSelf, "Real earned upgrade opens the authored modal");
        Button[] visibleCards = cards.Where(card => card.gameObject.activeInHierarchy).ToArray();
        Check(visibleCards.Length == Mathf.Min(ExperienceAndUpgradeUI.MaximumOfferedUpgrades,
            queue.GetAvailableUpgradeOptions().Count), "A real reward offers no more than three choices");
        if (queue.GetAvailableUpgradeOptions().Any(option =>
                option.AnimalIndex == AnimalProjectileQueue.RabbitIndex
                && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo))
            Check(visibleCards.Any(card => card.name.Contains("Rabbit")),
                "Rabbit unlock appears among real upgrade choices while locked");
        Check(visibleCards.Select(card => card.name).Distinct().Count() == visibleCards.Length,
            "The three offered upgrades are unique");
        Check(xp.UpgradeModal.GetComponentsInChildren<Button>(true)[0].GetInstanceID() == cardId,
            "Opening a real upgrade retains authored button instances");
        int before = queue.Animals.Sum(entry => entry.UpgradeLevel);
        visibleCards[0].onClick.Invoke();
        Check(queue.Animals.Sum(entry => entry.UpgradeLevel) == before + 1 && !manager.IsChoosingUpgrade,
            "Saved upgrade card invokes exactly one real upgrade");
        Check(!Player.HasActiveProjectiles, "Upgrade click does not launch a projectile");
        Check(GameUIPreviewWindow.Find<AnimalSelectionWheel>().transform.childCount == wheelChildren,
            "Ammo and upgrade refreshes preserve wheel objects");

        // Regression: an empty middle entry used to leave a hole in the arc, moving
        // Rabbit beyond the visible/clickable part of the wheel.
        queue.Unlock(1);
        queue.Unlock(2);
        queue.ReloadAll();
        queue.ClearAmmo(1);
        queue.Select(0);
        AnimalSelectionWheel wheel = GameUIPreviewWindow.Find<AnimalSelectionWheel>();
        yield return null;
        Button fireflyButton = wheel.GetComponentsInChildren<Button>(true)
            .First(button => button.name == queue.Animals[0].DisplayName);
        Button rabbitButton = wheel.GetComponentsInChildren<Button>(true)
            .First(button => button.name == queue.Animals[2].DisplayName);
        Check(fireflyButton.gameObject.activeInHierarchy && rabbitButton.gameObject.activeInHierarchy
            && fireflyButton.GetComponent<CanvasGroup>().blocksRaycasts
            && rabbitButton.GetComponent<CanvasGroup>().blocksRaycasts,
            "Firefly and Rabbit stay visible and clickable when Hedgehog has no ammo");
        rabbitButton.onClick.Invoke();
        Check(queue.SelectedIndex == 2,
            "Rabbit can be selected directly across an exhausted middle slot");
    }

    private static string Prefs() => string.Join("|", Keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "-"));

    public static void CaptureBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        deadline = EditorApplication.timeSinceStartup + 120;
        captureStack.Push(CapturePreviews());
        EditorApplication.update += CaptureTick;
    }

    private static void CaptureTick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("UI preview capture timed out.");
            while (captureStack.Count > 0)
            {
                IEnumerator current = captureStack.Peek();
                if (!current.MoveNext()) { captureStack.Pop(); continue; }
                if (current.Current is IEnumerator nested) { captureStack.Push(nested); continue; }
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }
            EditorApplication.update -= CaptureTick;
            Debug.Log("Saved UI preview images in Logs/UI-*.png.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            EditorApplication.update -= CaptureTick;
            while (captureStack.Count > 0) (captureStack.Pop() as IDisposable)?.Dispose();
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static IEnumerator CapturePreviews()
    {
        EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
        GameUIPreviewWindow.InstallGameplay();
        foreach (string mode in new[] { "hud", "upgrade", "map", "pause" })
        {
            GameUIPreviewWindow.ShowGameplay(mode, 1, mode == "hud" ? 3 : 4, 1, true);
            yield return CaptureImage("Logs/UI-" + mode + ".png");
        }
        GameUIPreviewWindow.ShowGameplay("map", 5, 4, 4, true);
        yield return CaptureImage("Logs/UI-map-level5.png");
        EditorSceneManager.OpenScene(GameUIPreviewWindow.MenuPath);
        var hub = GameUIPreviewWindow.Find<MenuHubController>();
        hub.Refresh();
        hub.SelectTab(0);
        yield return CaptureImage("Logs/UI-campaign.png");
    }

    private static IEnumerator CaptureImage(string path)
    {
        Camera camera = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        if (camera == null) throw new Exception("Preview scene needs a camera.");
        var render = new RenderTexture(720, 1280, 24);
        var image = new Texture2D(720, 1280, TextureFormat.RGB24, false);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true)
            .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var previousCameras = canvases.Select(c => c.worldCamera).ToArray();
        var previousDistances = canvases.Select(c => c.planeDistance).ToArray();
        Canvas[] screenCanvases = Object.FindObjectsOfType<Canvas>(true)
            .Where(c => c.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        int[] previousOrders = screenCanvases.Select(c => c.sortingOrder).ToArray();
        try
        {
            camera.targetTexture = render;
            // Overlay canvases normally render after the entire world. Preserve that order
            // when routing them through the capture camera (including nested modal canvases).
            foreach (Canvas canvas in screenCanvases) canvas.sortingOrder += 6000;
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 2f;
            }
            Stage room = Object.FindObjectsOfType<Stage>().FirstOrDefault();
            if (room != null) camera.GetComponent<CameraRenderer>().FrameRoom(room);
            for (int i = 0; i < 6; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            camera.Render();
            RenderTexture.active = render;
            image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].worldCamera = previousCameras[i];
                canvases[i].planeDistance = previousDistances[i];
            }
            for (int i = 0; i < screenCanvases.Length; i++) screenCanvases[i].sortingOrder = previousOrders[i];
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(render);
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("UI validation timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Check(bool ok, string description)
    {
        if (!ok) throw new Exception(description);
        report.Add("PASS " + description);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        string[] saved = SessionState.GetString(SavedPrefs, "").Split('|');
        for (int i = 0; i < Keys.Length && i < saved.Length; i++)
            if (saved[i] == "-") PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetInt(Keys[i], int.Parse(saved[i]));
        PlayerPrefs.Save();
        string result = error == null ? "PASS: " + report.Count + " UI authoring checks." : "FAIL: " + error;
        report.Add(result);
        File.WriteAllLines("Logs/UIAuthoringValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
