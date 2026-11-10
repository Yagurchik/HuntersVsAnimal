using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BossRoomLayoutValidation
{
    private const string Running = "BossRoomLayoutValidation.Running";
    private static readonly List<string> Report = new List<string>();
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static int lastFrame = -1;
    private static double deadline;

    static BossRoomLayoutValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Steps.Clear(); Steps.Push(Checks()); deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run in the isolated validation project.");
        Directory.CreateDirectory("Logs"); Report.Clear();
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        LevelProgression authored = Object.FindObjectOfType<LevelProgression>(true);
        Check(authored != null, "Uses the native gameplay scene and authored progression");
        var data = new SerializedObject(authored);
        var levels = data.FindProperty("levels");
        Check(levels.arraySize == LevelProgression.MaxLevel, "Native scene retains all 30 authored levels");
        for (int i = 0; i < levels.arraySize; i++)
        {
            var level = levels.GetArrayElementAtIndex(i);
            var rooms = level.FindPropertyRelative("activeFourthLineRooms");
            Check(level.FindPropertyRelative("bosses").arraySize == 3
                && level.FindPropertyRelative("specialistSlots").arraySize >= 2,
                "Level " + (i + 1) + " stores editable boss and ordinary encounter rosters in the gameplay scene");
            Check(rooms.arraySize == 7 && Enumerable.Range(0, 7).All(j => rooms.GetArrayElementAtIndex(j).intValue == j + 1)
                && level.FindPropertyRelative("bossRoomIndex").intValue == 4,
                "Authored level " + (i + 1) + " opens all seven outer rooms and puts the outer boss in 4-4");
        }
        GameRunMode.SetDeveloperMode(false); GameRunMode.PrepareCampaign(); LevelProgression.PrepareLevel(2);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return Frames(4);
        LevelProgression progression = LevelProgression.Instance;
        for (int level = 1; level <= LevelProgression.MaxLevel; level++)
        {
            Check(Enumerable.Range(2, 3).Select(line => progression.GetBossKind(level, line, line))
                .Distinct().Count() == 3,
                "Level " + level + " assigns three different bosses to its three lines");
            for (int line = 2; line <= 4; line++)
            {
                var kind = progression.GetBossKind(level, line, line);
                int dragonLocation = kind == LevelProgression.BossKind.BoneDragon ? 1
                    : kind == LevelProgression.BossKind.GhostlySorrow ? 2
                    : kind == LevelProgression.BossKind.PoisonousGloom ? 3 : 0;
                Check(kind != LevelProgression.BossKind.BoneDragonMini
                    && (dragonLocation == 0 || (LevelProgression.LocationIndexForLevel(level) == dragonLocation
                        && LevelProgression.LocalLevelFor(level) == 5 && line == 4)),
                    "Level " + level + " line " + line + " respects dragon location and finale restrictions");
            }
            if (level % 5 == 0 && level <= 15)
                Check(new[] { LevelProgression.BossKind.BoneDragon, LevelProgression.BossKind.GhostlySorrow,
                    LevelProgression.BossKind.PoisonousGloom }.Contains(progression.GetBossKind(level, 4, 4)),
                    "Location " + (level / 5) + " ends with an authored dragon in 4-4");
            int bossRooms = 0;
            int planned = 0;
            int finalLine = LevelProgression.FinalCampaignLineForLevel(level);
            for (int line = finalLine + 1; line <= 4; line++)
            for (int index = 1; index <= line * 2 - 1; index++)
                Check(!progression.IsRoomActive(level, line, index)
                    && progression.GetCampaignRoomEnemyTotal(level, line, index) == 0,
                    "Level " + level + " room " + line + "-" + index + " is beyond its final line");
            for (int line = 1; line <= finalLine; line++)
            for (int index = 1; index <= line * 2 - 1; index++)
            {
                bool expectedBoss = line >= 2 && index == line;
                Check(progression.IsRoomActive(level, line, index)
                    && progression.IsBossRoom(level, line, index) == expectedBoss,
                    "Level " + level + " room " + line + "-" + index + " is active with the correct boss rule");
                Check((progression.GetBossKind(level, line, index) != LevelProgression.BossKind.None) == expectedBoss,
                    "Level " + level + " room " + line + "-" + index + " resolves a boss only in central rooms");
                int total = progression.GetCampaignRoomEnemyTotal(level, line, index);
                planned += total;
                if (line >= 2)
                {
                    if (expectedBoss) bossRooms++;
                    Check(progression.GetCampaignWaveEnemyCount(level, line, index, 1) == 6
                        && total == 14,
                        "Room " + line + "-" + index + " projects six starting enemies and adaptive reinforcements on level " + level);
                }
                Check(Enumerable.Range(1, LevelProgression.CampaignWavesPerRoom).All(wave =>
                    progression.GetCampaignWaveEnemyCount(level, line, index, wave) <= LevelProgression.GetCampaignWaveEnemyLimit(line)),
                    "Room " + line + "-" + index + " keeps the per-wave cap on level " + level);
            }
            Check(bossRooms == finalLine - 1 && progression.GetCampaignPlannedEnemyTotal(level) == planned + (level == 1 ? 6 : 0),
                "Level " + level + " has one planned boss room per outer line and counts every opened room");
        }
        Check(!progression.IsRoomActive(1, 4, 8) && !progression.IsRoomActive(1, 1, 2)
            && !progression.IsBossRoom(1, 1, 1), "Invalid rooms and the first line cannot host bosses");
        Check(progression.IsMirageRoom(6, 4, 4) && progression.IsMirageRoom(8, 3, 3)
            && progression.IsMirageRoom(10, 2, 2) && !progression.IsMirageRoom(6, 4, 1),
            "Mirage follows the authored per-level roster in Gloomy Hollow");

        var liveBossNames = new Dictionary<int, string[]>();
        foreach (int level in Enumerable.Range(1, LevelProgression.MaxLevel))
        {
            GameRunMode.PrepareCampaign(); LevelProgression.PrepareLevel(level);
            EditorSceneManager.LoadSceneInPlayMode("Assets/Asset.unity", new LoadSceneParameters(LoadSceneMode.Single));
            foreach (FirstLevelTutorial tutorial in Object.FindObjectsOfType<FirstLevelTutorial>(true)) tutorial.enabled = false;
            foreach (Player player in Object.FindObjectsOfType<Player>(true)) player.enabled = false;
            yield return Frames(4);
            GameManager manager = GameManager.Instance;
            manager.enabled = false; manager.isPaused = manager.stepGame = manager.stepPlayer = false;
            Time.timeScale = 1f;
            progression = LevelProgression.Instance;
            Check(progression.CurrentLevel == level && manager.Front.IsReady, "Native gameplay loads campaign level " + level);
            for (int line = 2; line <= 4; line++) manager.Front.ActivateLine(line);
            yield return Frames(2);
            Check(manager.Front.Model.Rooms.Count(room => room.Line == 4 && room.IsActive && room.CanSelect) == 7,
                "All seven fourth-line rooms are selectable on the live level " + level);
            int liveBosses = 0;
            var names = new string[5];
            for (int line = 1; line <= 4; line++)
            for (int index = 1; index <= line * 2 - 1; index++)
            {
                LevelGenerator room = manager.Front.GetGenerator(DefenseFront.IdFor(line, index));
                bool expectedBoss = line >= 2 && index == line;
                Brick[] firstWave = Enemies(room);
                int bosses = firstWave.Count(enemy => enemy.UsesBossHealth);
                liveBosses += bosses;
                Check(room.IsInitialized && room.gameObject.activeInHierarchy && room.IsCampaignBossRoom == expectedBoss
                    && bosses == (expectedBoss ? 1 : 0)
                    && firstWave.Length == progression.GetCampaignWaveEnemyCount(level, line, index, 1),
                    "Actual level " + level + " room " + line + "-" + index + " starts with its correct first wave");
                if (expectedBoss)
                {
                    names[line] = firstWave[0].name;
                    Check(firstWave.Length == 6 && firstWave[0].name.StartsWith(ExpectedBossName(level, line)),
                        "Central room " + line + "-" + index + " spawns " + ExpectedBossName(level, line) + " on level " + level);
                    Brick template = Resources.Load<GameObject>("Enemies/" + ExpectedBossName(level, line)).GetComponent<Brick>();
                    Brick spawnedBoss = firstWave[0];
                    var grid = new EnemyRoomGrid(room, room.Rows);
                    Vector2Int anchor = grid.Anchor(spawnedBoss.transform.position, spawnedBoss.Footprint);
                    Check(spawnedBoss.Footprint == template.Footprint
                        && Mathf.Approximately(spawnedBoss.MaxHp, room.OrdinaryEnemyBaseHealth * progression.EnemyHealthMultiplier * progression.GetBossHealthMultiplier(line))
                        && anchor.x >= 0 && anchor.y >= 0 && anchor.x + spawnedBoss.Footprint.x <= room.Columns
                        && anchor.y + spawnedBoss.Footprint.y <= room.Rows
                        && spawnedBoss.GetComponentsInChildren<SpriteRenderer>().Any(renderer => renderer.enabled
                            && renderer.gameObject.activeInHierarchy && renderer.sprite != null),
                        "Level " + level + " boss on line " + line + " keeps its authored footprint, visible sprite and current-level HP");
                }
                for (int wave = 2; wave <= LevelProgression.CampaignWavesPerRoom; wave++)
                {
                    var previous = new HashSet<Brick>(Enemies(room));
                    room.SpawnNextWave();
                    Brick[] spawned = Enemies(room).Where(enemy => !previous.Contains(enemy)).ToArray();
                    Check(room.SpawnedWaveCount == wave
                        && (line == 1
                            ? spawned.Length == progression.GetCampaignWaveEnemyCount(level, line, index, wave)
                            : spawned.Length <= progression.GetCampaignWaveEnemyCount(level, line, index, wave)
                                && room.LivingEnemyCount <= LevelGenerator.BossPressureEnemyLimit)
                        && spawned.All(enemy => !enemy.UsesBossHealth),
                        "Actual wave " + wave + " in " + line + "-" + index + " adds only ordinary enemies on level " + level);
                }
                Check(room.RemainingWaves == (line == 1 ? 0 : 1),
                    "Room " + line + "-" + index + " keeps the correct wave state on level " + level);
            }
            Check(liveBosses == 3, "Exactly three bosses actually spawn on level " + level);
            Check(names.Skip(2).Distinct().Count() == 3,
                "The three spawned bosses have different identities on level " + level);
            liveBossNames.Add(level, names);
            if (level > 1 && LevelProgression.LocalLevelFor(level) != 1)
                Check(!names.Skip(2).OrderBy(value => value).SequenceEqual(
                    liveBossNames[level - 1].Skip(2).OrderBy(value => value)),
                    "Neighboring levels within a location have different actual boss trios");
            Report.Add("LEVEL " + level + " LIVE PASSED");
        }
        GameRunMode.PrepareSurvival();
        EditorSceneManager.LoadSceneInPlayMode("Assets/Asset.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return Frames(4);
        GameManager survival = GameManager.Instance; survival.enabled = false;
        Check(survival.Front.Model.Rooms.Count(room => room.Line == 4 && room.CanSelect) == 7,
            "Survival also retains all seven opened fourth-line rooms");
        Check(survival.rooms.SelectMany(Enemies).Count(enemy => enemy.UsesBossHealth) == 1
            && Enemies(survival.Front.GetGenerator(DefenseFront.IdFor(4, 4))).Count(enemy => enemy.UsesBossHealth) == 1,
            "Survival starts its single outer boss in 4-4 while inner lines remain pre-liberated");
    }

    private static string ExpectedBossName(int level, int line)
    {
        return MenuEncounterPreviewAuthoring.BossResource(LevelProgression.Instance.GetBossKind(level, line, line));
    }
    private static Brick[] Enemies(LevelGenerator room) => room.EnemyCollection.Where(value => value != null)
        .Select(value => value.GetComponent<Brick>()).Where(enemy => enemy != null && !enemy.IsDefeated).ToArray();
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Report.Add("PASS " + description);
    }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Boss room layout validation timed out");
            while (Steps.Count > 0) { var step = Steps.Peek(); if (!step.MoveNext()) { Steps.Pop(); continue; } if (step.Current is IEnumerator child) { Steps.Push(child); continue; } return; }
            Finish(null);
        } catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false); Time.timeScale = 1f;
        Report.Add(error == null ? "BOSS_ROOM_LAYOUT_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/BossRoomLayoutValidation.txt", Report);
        if (error != null) Debug.LogException(error); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
