using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FirstLevelTutorialValidation
{
    private const string Running = "ForestAnimalWar.FirstLevelTutorialValidation.Running";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> report = new List<string>();
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;

    static FirstLevelTutorialValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 60d;
            stack.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Directory.CreateDirectory("Logs");
        report.Clear();
        stack.Clear();
        SessionState.SetBool(Running, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(1);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        yield return null;

        GameManager manager = GameManager.Instance;
        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        FirstLevelTutorial tutorial = UnityEngine.Object.FindObjectOfType<FirstLevelTutorial>(true);
        Check(manager != null && queue != null && tutorial != null,
            "Gameplay scene contains the tutorial and its required systems");
        Check(tutorial.IsRunning && manager.IsTutorialFlowActive,
            "Campaign level 1 always starts the mandatory tutorial flow");
        Check(manager.isPaused && Mathf.Approximately(Time.timeScale, 0f)
            && tutorial.TutorialCanvas != null && tutorial.TutorialCanvas.gameObject.activeSelf,
            "The opening lesson safely pauses play and shows its editable scene UI");
        Check(queue.MaximumAllowedAnimalIndex == AnimalProjectileQueue.HedgehogIndex
            && queue.IsAvailable(0) && queue.HasAmmo(0)
            && !queue.IsAvailable(1) && !queue.IsAvailable(2) && !queue.IsAvailable(3),
            "The first lesson starts with fireflies while hedgehog waits for experience");
        List<AnimalProjectileQueue.UpgradeOption> forcedOptions = queue.GetAvailableUpgradeOptions();
        Check(queue.HasForcedUpgradeOption && forcedOptions.Count == 1
            && forcedOptions[0].AnimalIndex == AnimalProjectileQueue.HedgehogIndex
            && forcedOptions[0].Kind == AnimalProjectileQueue.UpgradeKind.Ammo,
            "The first earned upgrade is reserved exclusively for unlocking hedgehog");

        tutorial.ContinueButton.onClick.Invoke();
        Check(tutorial.CurrentStepName == "Queue", "The queue lesson follows the introduction");
        tutorial.ContinueButton.onClick.Invoke();
        Check(tutorial.CurrentStepName == "FireflyAction" && !manager.isPaused,
            "The firefly lesson releases input and waits for a real shot");

        InvokeVolley(tutorial, AnimalProjectileQueue.FireflyIndex);
        yield return null;
        Check(tutorial.CurrentStepName == "Darkness" && manager.isPaused,
            "A resolved firefly volley advances to the darkness and burning lesson");
        tutorial.ContinueButton.onClick.Invoke();
        Check(tutorial.CurrentStepName == "Rooms" && manager.Front.Interface.IsMapOpen,
            "The room lesson force-opens the real defense map");

        tutorial.ContinueButton.onClick.Invoke();
        Check(tutorial.CurrentStepName == "Experience" && manager.isPaused
            && !queue.IsAvailable(AnimalProjectileQueue.HedgehogIndex),
            "Experience is explained before hedgehog becomes available");
        tutorial.ContinueButton.onClick.Invoke();
        Check(tutorial.CurrentStepName == "AwaitingExperience"
            && !manager.IsTutorialFlowActive && !manager.isPaused
            && !tutorial.TutorialCanvas.gameObject.activeSelf,
            "The lesson releases the player into real combat to earn experience");

        queue.ClearAllAmmo();
        manager.RegisterEnemyDefeated(manager.ExperienceForNextUpgrade);
        manager.TryCompletePlayerTurn();
        yield return null;
        forcedOptions = queue.GetAvailableUpgradeOptions();
        Check(manager.IsChoosingUpgrade
            && tutorial.CurrentStepName == "AwaitingHedgehogChoice"
            && forcedOptions.Count == 1
            && forcedOptions[0].AnimalIndex == AnimalProjectileQueue.HedgehogIndex,
            "Filling the experience bar opens the real upgrade UI with hedgehog only");
        Check(!queue.IsAvailable(AnimalProjectileQueue.HedgehogIndex),
            "The hedgehog hint never appears before the player chooses its card");

        LevelGenerator targetRoom = manager.Front.GetGenerator(manager.Front.CurrentRoomId);
        int enemiesBeforeHedgehog = targetRoom != null ? targetRoom.EnemyCollection.Count : 0;
        manager.SelectProjectileUpgrade(AnimalProjectileQueue.HedgehogIndex,
            AnimalProjectileQueue.UpgradeKind.Ammo);
        yield return null;
        Check(tutorial.CurrentStepName == "HedgehogIntro"
            && !manager.IsChoosingUpgrade && manager.IsTutorialFlowActive
            && queue.IsAvailable(0) && queue.IsAvailable(AnimalProjectileQueue.HedgehogIndex)
            && !queue.IsAvailable(2) && !queue.IsAvailable(3),
            "Choosing the earned hedgehog opens its lesson and keeps later animals excluded");
        Check(targetRoom != null && targetRoom.HasLivingEnemies
            && targetRoom.EnemyCollection.Count >= enemiesBeforeHedgehog + 6,
            "A compact six-enemy tutorial cluster appears for the hedgehog trial");

        FieldInfo shootingGate = typeof(Player).GetField("shootingPlayer",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (shootingGate == null) throw new MissingFieldException("Player.shootingPlayer");
        shootingGate.SetValue(null, false);
        tutorial.ContinueButton.onClick.Invoke();
        Check(tutorial.CurrentStepName == "HedgehogAction" && !manager.isPaused
            && (bool)shootingGate.GetValue(null),
            "The hedgehog lesson reopens the shot gate without reloading fireflies");
        InvokeVolley(tutorial, AnimalProjectileQueue.HedgehogIndex);
        yield return null;
        Check(tutorial.CurrentStepName == "Complete" && manager.isPaused,
            "A resolved hedgehog volley reaches the final lesson");
        tutorial.ContinueButton.onClick.Invoke();
        yield return null;
        Check(!tutorial.IsRunning && !manager.IsTutorialFlowActive
            && !tutorial.TutorialCanvas.gameObject.activeSelf && !manager.isPaused
            && Mathf.Approximately(Time.timeScale, 1f),
            "Finishing the tutorial restores the normal player-turn flow");
        Check(queue.MaximumAllowedAnimalIndex == queue.Animals.Count - 1
            && queue.GetAvailableUpgradeOptions().Any(option =>
                option.AnimalIndex == AnimalProjectileQueue.RabbitIndex
                && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo),
            "Finishing the tutorial offers rabbit as a projectile unlock");
    }

    private static void InvokeVolley(FirstLevelTutorial tutorial, int animalIndex)
    {
        MethodInfo method = typeof(FirstLevelTutorial).GetMethod("OnVolleyStarted", Private);
        if (method == null) throw new MissingMethodException("FirstLevelTutorial.OnVolleyStarted");
        method.Invoke(tutorial, new object[] { animalIndex });
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        report.Add("PASS " + description);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("First-level tutorial validation timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        Time.timeScale = 1f;
        string result = error == null
            ? "PASS: " + report.Count + " first-level tutorial checks."
            : "FAIL: " + error;
        report.Add(result);
        File.WriteAllLines("Logs/FirstLevelTutorialValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
