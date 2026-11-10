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
public static class SurvivalModeValidation
{
    private const string BatchKey = "ForestAnimalWar.SurvivalValidation.Running";
    private const string PrefsKey = "ForestAnimalWar.SurvivalValidation.Prefs";
    private static readonly string[] SavedKeys = {
        GameRunMode.RequestedModeKey,
        GameRunMode.LastSurvivalTurnsKey,
        GameRunMode.BestSurvivalTurnsKey,
        "ForestAnimalWar.RequestedLevel",
        "ForestAnimalWar.CurrentLevel",
        "ForestAnimalWar.HighestUnlockedLevel",
        "ForestAnimalWar.MenuSelectedLevel"
    };
    private static IEnumerator routine;
    private static double deadline;
    private static int lastFrame = -1;
    private static int checks;
    private static readonly List<string> report = new List<string>();

    static SurvivalModeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(BatchKey, false))
                BeginChecks();
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Directory.CreateDirectory("Logs");
        List<string> saved = new List<string>();
        foreach (string key in SavedKeys)
            saved.Add(PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key).ToString() : "-");
        SessionState.SetString(PrefsKey, string.Join("|", saved));
        SessionState.SetBool(BatchKey, true);
        PlayerPrefs.SetInt(GameRunMode.LastSurvivalTurnsKey, 0);
        PlayerPrefs.SetInt(GameRunMode.BestSurvivalTurnsKey, 0);
        GameRunMode.PrepareSurvival();
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static void BeginChecks()
    {
        deadline = EditorApplication.timeSinceStartup + 90d;
        routine = Checks();
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Survival validation timed out.");
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (routine.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error)
        {
            Finish(error.InnerException ?? error);
        }
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        LevelProgression progression = LevelProgression.Instance;
        Check(manager != null && manager.IsSurvivalMode, "Gameplay consumes the survival request");
        Check(progression != null && progression.CurrentLevel == 1,
            "Survival starts with the basic campaign balance");
        Check(manager.Front.Model.Rooms.Where(r => r.Line == 4).All(r => r.IsActive),
            "All seven fourth-line rooms are active in survival, whatever the campaign level allows");
        Check(manager.stage == 4 && manager.Front.Model.OpenedLine == 4,
            "All four defense lines are deployed immediately");
        Check(manager.rooms.Count == 16 && manager.rooms.All(room => room.IsInitialized),
            "Every survival room is initialized at the start");
        Check(manager.rooms.Where(room => room.DefenseLine < 4)
                .All(room => !room.HasEnemies && !room.IsLost),
            "The nine inner rooms start liberated and empty");
        Check(manager.Front.IncomePerTurn == 9 * manager.Front.GoldPerSecuredRoom,
            "Every pre-liberated inner room produces turn income");
        Check(manager.PlayerUpgradeLevel == 7 && manager.PendingProjectileUpgrades == 6
                && manager.IsChoosingUpgrade,
            "Survival starts at player level seven with six upgrade choices");

        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        for (int choice = 0; choice < 6; choice++)
        {
            List<AnimalProjectileQueue.UpgradeOption> options = queue.GetAvailableUpgradeOptions();
            Check(options.Count > 0, "A starting projectile upgrade option is available");
            manager.SelectProjectileUpgrade(options[0].AnimalIndex, options[0].Kind);
            yield return null;
        }
        Check(!manager.IsChoosingUpgrade && manager.PendingProjectileUpgrades == 0,
            "Combat resumes only after every starting upgrade is selected");

        LevelGenerator[] outerRooms = manager.rooms.Where(room => room != null
            && room.DefenseLine == 4).OrderBy(room => room.RoomIndex).ToArray();
        Check(outerRooms.Length == 7 && outerRooms.All(room => room.IsEndlessSurvivalSource),
            "Every fourth-line room is an endless source from the first turn");
        Check(outerRooms.All(room => room.RemainingWaves == 1 && !room.IsCompleted),
            "Endless rooms can never become complete");

        Brick sample = outerRooms[0].EnemyCollection[0].GetComponent<Brick>();
        float startHealth = sample.hp;
        float startDamage = sample.dmg;
        MethodInfo advance = typeof(GameManager).GetMethod("AdvanceSurvivalDifficulty",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(advance != null, "Per-turn survival scaler is available");
        advance.Invoke(manager, null);
        Check(manager.SurvivalPressureTurns == 1 && sample.hp > startHealth,
            "Enemy strength starts growing after the first survival turn");
        advance.Invoke(manager, null);
        advance.Invoke(manager, null);
        Check(manager.SurvivalPressureTurns == 3, "Pressure increases on every survival turn");
        Check(sample.hp > startHealth && sample.dmg > startDamage,
            "Existing enemies gain health and damage every turn");
        Check(manager.GetSurvivalWaveEnemyCount(2) == 3,
            "Endless waves gain another enemy at the configured interval");
        advance.Invoke(manager, null);
        advance.Invoke(manager, null);
        advance.Invoke(manager, null);
        Check(manager.GetSurvivalWaveEnemyCount(2) == 4
                && manager.GetSurvivalWaveEnemyCount(99) == 4,
            "Every endless wave stays within the one-to-four enemy cap");

        int enemiesBeforeWave = outerRooms[0].EnemyCollection.Count;
        outerRooms[0].SpawnNextWave();
        manager.Front.RefreshRoom(outerRooms[0]);
        Check(outerRooms[0].EnemyCollection.Count > enemiesBeforeWave,
            "A new fourth-line wave is spawned after its finite campaign waves are exhausted");
        Check(outerRooms[0].RemainingWaves == 1
            && manager.Front.Model.GetRoom(outerRooms[0].RoomId).Waves == 1
            && !manager.Front.IsQuiet,
            "The defense model keeps an endless threat and cannot trigger campaign victory");

        LevelGenerator innerDestination = manager.Front.GetGenerator(DefenseFront.IdFor(3, 1));
        outerRooms[0].EnemyCollection[0].transform.position = new Vector3(
            outerRooms[0].transform.position.x, outerRooms[0].DefenseBoundaryY,
            outerRooms[0].transform.position.z);
        int innerBeforeBreach = innerDestination.EnemyCollection.Count;
        manager.Front.BreachRoom(outerRooms[0]);
        Check(!manager.GameEnded && !outerRooms[0].IsLost
            && manager.Front.Model.GetRoom(outerRooms[0].RoomId).CanSelect,
            "A fourth-line breach neither ends survival nor locks the outer room");
        manager.Front.DeliverPressure();
        Check(!manager.GameEnded && innerDestination.EnemyCollection.Count == innerBeforeBreach + 1
            && manager.Front.PendingTransferEnemies == 0,
            "Only the yellow-line enemy moves inward; nothing else is queued");
        int outerBeforeReinforcement = outerRooms[0].EnemyCollection.Count;
        outerRooms[0].SpawnNextWave();
        Check(!manager.GameEnded && outerRooms[0].EnemyCollection.Count > outerBeforeReinforcement,
            "Endless reinforcements keep spawning in the breached outer room itself");

        Check(GameRunMode.RecordSurvivalScore(9) == 9, "First survival score is recorded");
        Check(GameRunMode.RecordSurvivalScore(4) == 9, "A lower score does not erase the record");
        Check(GameRunMode.RecordSurvivalScore(12) == 12, "A higher score replaces the record");
        LevelTransition transition = UnityEngine.Object.FindObjectOfType<LevelTransition>(true);
        manager.LoseGame();
        yield return null;
        Check(manager.GameEnded && transition.LosePanel.activeSelf,
            "Defeat ends survival and opens the Lose panel");
        Check(transition.SurvivalResultText != null
            && transition.SurvivalResultText.gameObject.activeSelf
            && transition.SurvivalResultText.text.Contains("12"),
            "The editable Lose label displays the saved best result");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
        report.Add("PASS " + description);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(BatchKey, false);
        string[] saved = SessionState.GetString(PrefsKey, "").Split('|');
        for (int i = 0; i < SavedKeys.Length && i < saved.Length; i++)
        {
            if (saved[i] == "-") PlayerPrefs.DeleteKey(SavedKeys[i]);
            else PlayerPrefs.SetInt(SavedKeys[i], int.Parse(saved[i]));
        }
        PlayerPrefs.Save();
        string summary = error == null
            ? "PASS: " + checks + " survival integration checks."
            : "FAIL: " + error;
        report.Add(summary);
        File.WriteAllLines("Logs/SurvivalModeValidation.txt", report);
        if (error == null) Debug.Log(summary); else Debug.LogError(summary);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
