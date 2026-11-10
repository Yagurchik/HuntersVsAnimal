using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FiveWaveCampaignValidation
{
    private const string Running = "FiveWaveCampaignValidation.Running";
    private const string ReportPath = "Logs/FiveWaveCampaignValidation.txt";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static readonly List<string> report = new List<string>();
    private static double deadline;
    private static int lastFrame;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static FiveWaveCampaignValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            report.Clear(); report.AddRange(File.ReadAllLines(ReportPath));
            steps.Clear(); steps.Push(CheckRuntime());
            deadline = EditorApplication.timeSinceStartup + 120; lastFrame = -1;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run in the isolated validation project.");
        Directory.CreateDirectory("Logs"); report.Clear();
        PlayerPrefs.DeleteAll(); GameRunMode.SetDeveloperMode(false); GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(20);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        File.WriteAllLines(ReportPath, report);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckRuntime()
    {
        yield return null; yield return null;
        GameManager manager = GameManager.Instance; manager.enabled = false;
        foreach (Player player in Object.FindObjectsOfType<Player>()) player.enabled = false;
        manager.stepPlayer = false; manager.stepGame = false;
        var progression = LevelProgression.Instance;
        Require(manager.stage == 1 && manager.Front.Model.OpenedLine == 1, "Campaign starts with only line one open");
        Require(manager.CampaignLineIntervalTurns == 5, "Authored line interval is five completed enemy turns");
        Require(progression.GetEnemyHealthMultiplier(19) * 100f < 1000f
            && Mathf.Abs(progression.GetEnemyHealthMultiplier(20) * 100f - 1008.6028f) < 0.01f,
            "The first wave first exceeds 1000 base HP at level twenty");
        float previousGrowth = float.PositiveInfinity;
        for (int level = 2; level <= 30; level++)
        {
            float growth = progression.GetEnemyHealthMultiplier(level) / progression.GetEnemyHealthMultiplier(level - 1) - 1f;
            Require(growth > 0f && growth < previousGrowth, "Percentage HP growth diminishes at level " + level);
            previousGrowth = growth;
        }
        LevelGenerator first = manager.Front.GetGenerator(0);
        Require(first.SpawnedWaveCount == 1 && first.RemainingWaves == 4, "First room starts with four remaining waves");
        Require(first.EnemyCollection.All(go => Mathf.Abs(go.GetComponent<Brick>().MaxHp - 1008.6028f) < 0.02f),
            "Actual level-twenty ordinary enemies match the approved HP table");
        MethodInfo advance = typeof(GameManager).GetMethod("AdvanceCampaignLineSchedule", Private);
        FieldInfo turn = typeof(GameManager).GetField("<TurnNumber>k__BackingField", Private);
        MethodInfo resolve = typeof(GameManager).GetMethod("ResolveEnemyTurnRoutine", Private);
        for (int line = 2; line <= 4; line++)
        {
            int openingTurn = (line - 1) * 5;
            turn.SetValue(manager, openingTurn - 1); advance.Invoke(manager, null);
            Require(manager.stage == line - 1, "Line " + line + " remains locked before turn " + openingTurn);
            int focus = manager.Front.CurrentRoomId;
            yield return (IEnumerator)resolve.Invoke(manager, null);
            Require(manager.TurnNumber == openingTurn && manager.stage == line
                && manager.Front.Model.OpenedLine == line, "Enemy turn " + openingTurn + " opens line " + line);
            Require(first.HasLivingEnemies, "Line " + line + " opens while line one still contains enemies");
            Require(manager.Front.CurrentRoomId == focus, "Opening line " + line + " preserves the selected room");
            foreach (DefenseFront.Room room in manager.Front.Model.Rooms.Where(r => r.Line == line && r.IsActive))
            {
                LevelGenerator opened = manager.Front.GetGenerator(room.Id);
                Require(opened.IsInitialized && opened.SpawnedWaveCount == 1 && opened.RemainingWaves == 4,
                    "Room " + room.Label + " opens on wave one and does not move or spawn twice");
            }
        }

        LevelGenerator sample = manager.Front.GetGenerator(DefenseFront.IdFor(4, 1));
        int[] counts = { 4, 2, 2, 1 };
        for (int wave = 2; wave <= 5; wave++)
        {
            var before = new HashSet<int>(sample.EnemyCollection.Select(go => go.GetInstanceID()));
            sample.SpawnNextWave();
            Brick[] added = sample.EnemyCollection.Where(go => !before.Contains(go.GetInstanceID()))
                .Select(go => go.GetComponent<Brick>()).ToArray();
            Require(added.Length == counts[wave - 2] && sample.SpawnedWaveCount == wave
                && sample.RemainingWaves == 5 - wave, "Finite wave " + wave + " respects the density limit and remaining count");
            Require(added.All(enemy => Mathf.Abs(enemy.MaxHp - sample.OrdinaryEnemyBaseHealth
                * enemy.HealthCoefficient * progression.EnemyHealthMultiplier * (1f + 0.25f * (wave - 1))) < 0.03f),
                "Actual wave " + wave + " uses the approved HP multiplier");
        }
        Require(manager.HasLivingCampaignBossOnLine(4), "The final-line boss remains alive after wave five");
        sample.SpawnNextWave(); sample.SpawnNextWave();
        Require(sample.SpawnedWaveCount == 5 && sample.LivingEnemyCount == 15 && sample.RemainingWaves == 0,
            "A living boss cannot create a sixth campaign wave");

        LevelGenerator blocked = manager.Front.GetGenerator(DefenseFront.IdFor(4, 2));
        var extras = new List<GameObject>();
        while (blocked.LivingEnemyCount < 15)
        {
            GameObject extra = Object.Instantiate(blocked.brickPrefab, new Vector3(100f, 100f), Quaternion.identity, blocked.transform);
            extra.GetComponent<Brick>().levelGenerator = blocked; blocked.EnemyCollection.Add(extra); extras.Add(extra);
        }
        blocked.SpawnNextWave();
        Require(blocked.SpawnedWaveCount == 1 && blocked.RemainingWaves == 4, "A full room preserves its pending wave");
        foreach (GameObject extra in extras) { blocked.EnemyCollection.Remove(extra); Object.DestroyImmediate(extra); }
        blocked.SpawnNextWave();
        Require(blocked.SpawnedWaveCount == 2 && blocked.RemainingWaves == 3, "The deferred wave spawns once space is available");

        LevelGenerator bossRoom = manager.Front.GetGenerator(DefenseFront.IdFor(4, 4));
        Brick boss = bossRoom.EnemyCollection.Select(go => go.GetComponent<Brick>()).Single(enemy => enemy.UsesBossHealth);
        boss.VanishForBossAbsorption();
        Require(bossRoom.RemainingWaves == 4, "Defeating a boss preserves the four remaining waves");
        bossRoom.SpawnNextWave();
        Require(bossRoom.SpawnedWaveCount == 2 && bossRoom.RemainingWaves == 3, "A defeated boss room still receives wave two");

        Require(!manager.Front.IsQuiet && !manager.GameEnded, "Remaining enemies and waves prevent premature victory");
        foreach (LevelGenerator room in manager.rooms.ToArray())
        {
            Clear(room);
            for (int attempt = 0; room.RemainingWaves > 0 && attempt < 5; attempt++)
            {
                room.SpawnNextWave(); Clear(room);
            }
            Require(room.SpawnedWaveCount == 5 && room.RemainingWaves == 0, "Room " + room.RoomId + " completes exactly five waves");
        }
        manager.Front.RefreshAll();
        Require(manager.Front.IsQuiet && manager.Front.PendingTransferEnemies == 0, "The completed four-line front is quiet");
        manager.stepPlayer = false; manager.stepGame = false; manager.TryCompletePlayerTurn();
        Require(manager.GameEnded, "Victory remains reachable after all five waves on every line");
        Finish(null);
    }

    private static void Clear(LevelGenerator room)
    {
        foreach (GameObject enemy in room.EnemyCollection.ToArray()) if (enemy != null) Object.DestroyImmediate(enemy);
        room.EnemyCollection.Clear(); GameManager.Instance.Front.RefreshRoom(room);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        report.Add("PASS: " + message);
    }
    private static void Tick()
    {
        if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Five-wave validation timed out.");
            while (steps.Count > 0)
            {
                IEnumerator step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                break;
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false); EditorApplication.update -= Tick;
        report.Add(error == null ? "RESULT: PASS (" + report.Count + " checks)" : "RESULT: FAIL " + error);
        File.WriteAllLines(ReportPath, report);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
