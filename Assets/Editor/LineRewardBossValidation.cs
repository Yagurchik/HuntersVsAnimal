using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class LineRewardBossValidation
{
    private const string Running = "LineRewardBossValidation.Running";
    private const string Report = "Logs/LineRewardBossValidation.txt";
    private static IEnumerator checks;
    private static int lastFrame;
    private static double deadline;
    private static readonly List<string> results = new List<string>();
    static LineRewardBossValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            results.Clear(); checks = CheckRuntime(); lastFrame = -1;
            deadline = EditorApplication.timeSinceStartup + 90; EditorApplication.update += Tick;
        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Use the isolated validation project.");
        Directory.CreateDirectory("Logs"); PlayerPrefs.DeleteAll();
        GameRunMode.SetDeveloperMode(true); GameRunMode.PrepareCampaign(); LevelProgression.PrepareLevel(20);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    private static IEnumerator CheckRuntime()
    {
        yield return null; yield return null;
        GameManager manager = GameManager.Instance; manager.enabled = false; manager.stepPlayer = true;
        foreach (Player player in Object.FindObjectsOfType<Player>()) player.enabled = false;
        Require(manager.Front.Model.OpenedLine == 4 && manager.PlayerUpgradeLevel == 1, "All test lines start without clear bonuses");
        LevelProgression progression = LevelProgression.Instance;
        for (int line = 2; line <= 4; line++)
        {
            LevelGenerator room = manager.Front.GetGenerator(DefenseFront.IdFor(line, line));
            Brick boss = room.EnemyCollection.Select(go => go.GetComponent<Brick>()).Single(enemy => enemy.UsesBossHealth);
            float ratio = line == 2 ? 100f : line == 3 ? 150f : 200f;
            float expected = room.OrdinaryEnemyBaseHealth * progression.EnemyHealthMultiplier * ratio;
            Require(Mathf.Abs(boss.MaxHp - expected) < 0.1f && Mathf.Approximately(boss.hp, boss.MaxHp),
                "Line " + line + " boss has ordinary base HP × " + ratio);
            Transform parent = boss.transform.parent;
            boss.gameObject.SetActive(false); boss.transform.SetParent(manager.Front.GetGenerator(0).transform, true);
            boss.gameObject.SetActive(true);
            Require(Mathf.Abs(boss.MaxHp - expected) < 0.1f, "Reactivation on another line does not multiply boss HP again");
            boss.gameObject.SetActive(false); boss.transform.SetParent(parent, true); boss.gameObject.SetActive(true);
            // This test manually restores the transfer fixture; production arrival does this registration.
            boss.levelGenerator = room;
            if (!room.EnemyCollection.Contains(boss.gameObject)) room.EnemyCollection.Add(boss.gameObject);
            manager.Front.RefreshRoom(room);
        }
        LevelGenerator fourth = manager.Front.GetGenerator(DefenseFront.IdFor(4, 1));
        Brick parentBoss = manager.Front.GetGenerator(DefenseFront.IdFor(4, 4)).EnemyCollection
            .Select(go => go.GetComponent<Brick>()).Single(enemy => enemy.UsesBossHealth);
        GameObject heir = Object.Instantiate(Resources.Load<GameObject>("Enemies/DrownedHeir"), manager.Front.GetGenerator(0).transform);
        heir.GetComponent<Brick>().InheritBossHealth(parentBoss);
        Require(Mathf.Abs(heir.GetComponent<Brick>().MaxHp - fourth.OrdinaryEnemyBaseHealth
            * progression.EnemyHealthMultiplier * 200f * 0.25f) < 0.1f, "Drowned heirs retain one quarter of parent HP even on another line");
        Object.DestroyImmediate(heir);
        manager.RegisterEnemyDefeated(7);
        int startLevel = manager.PlayerUpgradeLevel;
        FinishRoom(manager.Front.GetGenerator(DefenseFront.IdFor(2, 1)));
        Require(manager.PlayerUpgradeLevel == startLevel && manager.PendingProjectileUpgrades == 0,
            "A cleared side room grants no bonus level");
        FinishRoom(manager.Front.GetGenerator(DefenseFront.IdFor(2, 2)));
        Require(manager.PlayerUpgradeLevel == startLevel, "A second cleared room still grants no line bonus");
        FinishRoom(manager.Front.GetGenerator(DefenseFront.IdFor(2, 3)));
        Require(manager.PlayerUpgradeLevel == startLevel + 1 && manager.PendingProjectileUpgrades == 1
            && manager.CurrentExperience == 7 && manager.ExperienceForNextUpgrade == 30,
            "Completing line two grants one level and preserves XP and its price");
        for (int i = 0; i < 8; i++) manager.Front.RefreshAll();
        manager.RegisterLineCleared(2); manager.RegisterLineCleared(0); manager.RegisterLineCleared(5);
        Require(manager.PlayerUpgradeLevel == startLevel + 1, "Repeated refreshes, duplicate callbacks and invalid lines grant no extra level");
        LevelGenerator recaptured = manager.Front.GetGenerator(DefenseFront.IdFor(2, 1));
        GameObject invader = Object.Instantiate(recaptured.brickPrefab, recaptured.transform);
        invader.GetComponent<Brick>().levelGenerator = recaptured; recaptured.EnemyCollection.Add(invader);
        manager.Front.RefreshRoom(recaptured); Clear(recaptured);
        Require(manager.PlayerUpgradeLevel == startLevel + 1, "Recapturing the line cannot farm another level");
        manager.RegisterEnemyDefeated(23);
        Require(manager.PlayerUpgradeLevel == startLevel + 2 && manager.CurrentExperience == 0
            && manager.ExperienceForNextUpgrade == 34, "Kill XP continues granting its normal upgrades");

        for (int index = 1; index <= 4; index++) FinishRoom(manager.Front.GetGenerator(DefenseFront.IdFor(3, index)));
        LevelGenerator last = manager.Front.GetGenerator(DefenseFront.IdFor(3, 5));
        Clear(last);
        while (last.SpawnedWaveCount < 4) { last.SpawnNextWave(); Clear(last); }
        Require(manager.PlayerUpgradeLevel == startLevel + 2 && last.RemainingWaves == 1,
            "Four cleared rooms and an unfinished fifth wave do not reward line three");
        last.SpawnNextWave(); Clear(last);
        Require(manager.PlayerUpgradeLevel == startLevel + 3, "The final fifth wave completes line three exactly once");
        FinishRoom(manager.Front.GetGenerator(0));
        Require(manager.PlayerUpgradeLevel == startLevel + 4, "Line one grants its one bonus while line four is still active");
        for (int index = 1; index <= 7; index++) FinishRoom(manager.Front.GetGenerator(DefenseFront.IdFor(4, index)));
        Require(manager.PlayerUpgradeLevel == startLevel + 5 && manager.Front.IsQuiet,
            "The whole battle grants four line bonuses plus the one earned XP upgrade");
        manager.stepPlayer = false; manager.stepGame = false; manager.TryCompletePlayerTurn();
        Require(manager.GameEnded && !manager.IsChoosingUpgrade && manager.PendingProjectileUpgrades == 0,
            "The final line bonus cannot block campaign victory with an upgrade window");
        Finish(null);
    }
    private static void Clear(LevelGenerator room)
    {
        foreach (GameObject enemy in room.EnemyCollection.ToArray()) if (enemy != null) Object.DestroyImmediate(enemy);
        room.EnemyCollection.Clear(); GameManager.Instance.Front.RefreshRoom(room);
    }
    private static void FinishRoom(LevelGenerator room)
    {
        Clear(room);
        for (int i = 0; room.RemainingWaves > 0 && i < 5; i++) { room.SpawnNextWave(); Clear(room); }
        Require(room.RemainingWaves == 0, "Room " + room.RoomId + " completed its five waves");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }
    private static void Tick()
    {
        if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException();
            checks.MoveNext();
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false); EditorApplication.update -= Tick;
        results.Add(error == null ? "RESULT: PASS (" + results.Count + " checks)" : "RESULT: FAIL " + error);
        File.WriteAllLines(Report, results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
