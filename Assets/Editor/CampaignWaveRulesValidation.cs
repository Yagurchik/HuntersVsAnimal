using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CampaignWaveRulesValidation
{
    private const string GameplayScene = "Assets/Asset.unity";
    private const string PlayValidationRunning = "ForestAnimalWar.CampaignWaveRulesValidation.Running";
    private static readonly List<string> failures = new List<string>();
    private static double playStartedAt;
    private static double playDeadline;

    static CampaignWaveRulesValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static void RunBatch()
    {
        try
        {
            failures.Clear();
            EditorSceneManager.OpenScene(GameplayScene);
            LevelProgression progression = UnityEngine.Object.FindObjectOfType<LevelProgression>(true);
            GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>(true);
            Check(progression != null && manager != null,
                "Gameplay scene contains progression and game manager");
            if (progression != null)
            {
                int[] minimumByLine = { 0, LevelProgression.FirstLineMinimumEnemyTotal,
                    LevelProgression.SecondLineMinimumEnemyTotal,
                    LevelProgression.ThirdLineMinimumEnemyTotal };
                int[] maximumByLine = { 0, LevelProgression.FirstLineMaximumEnemyTotal,
                    LevelProgression.SecondLineMaximumEnemyTotal,
                    LevelProgression.ThirdLineMaximumEnemyTotal };
                for (int level = 1; level <= LevelProgression.MaxLevel; level++)
                {
                    for (int line = 1; line <= 4; line++)
                        Check(progression.GetBalance(level, line).additionalWaves
                                == LevelProgression.CampaignWavesPerRoom - 1,
                            "Level " + level + " line " + line + " has five waves");
                    for (int line = 1; line <= 3; line++)
                    {
                        for (int room = 1; room <= line * 2 - 1; room++)
                        {
                            int roomTotal = progression.GetCampaignRoomEnemyTotal(level, line, room);
                            if (!progression.IsRoomActive(level, line, room))
                            {
                                Check(roomTotal == 0 && line > LevelProgression.FinalCampaignLineForLevel(level),
                                    "Level " + level + " room " + line + "-" + room
                                    + " is beyond the level's final line and plans no enemies");
                                continue;
                            }
                            if (line >= 2)
                            {
                                Check(progression.GetCampaignWaveEnemyCount(level, line, room, 1) == 6
                                    && progression.GetCampaignWaveEnemyCount(level, line, room, 2) == 4
                                    && progression.GetCampaignWaveEnemyCount(level, line, room, 3) == 2
                                    && progression.GetCampaignWaveEnemyCount(level, line, room, 4) == 2
                                    && progression.GetCampaignWaveEnemyCount(level, line, room, 5) == 1
                                    && roomTotal == 15,
                                    "Level " + level + " room " + line + "-" + room
                                    + " projects density-based boss pressure across five waves");
                                continue;
                            }
                            int waveTotal = Enumerable.Range(1, LevelProgression.CampaignWavesPerRoom)
                                .Sum(wave => progression.GetCampaignWaveEnemyCount(
                                    level, line, room, wave));
                            Check(roomTotal >= minimumByLine[line]
                                    && roomTotal <= maximumByLine[line]
                                    && waveTotal == roomTotal,
                                "Level " + level + " room " + line + "-" + room
                                + " stays inside its total range");
                            int limit = LevelProgression.GetCampaignWaveEnemyLimit(line);
                            Check(Enumerable.Range(1, LevelProgression.CampaignWavesPerRoom)
                                    .All(wave => progression.GetCampaignWaveEnemyCount(
                                        level, line, room, wave) <= limit),
                                "Level " + level + " room " + line + "-" + room
                                + " respects the per-wave line cap " + limit);
                            float[] shares = { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };
                            Check(Enumerable.Range(1, LevelProgression.CampaignWavesPerRoom)
                                    .All(wave => Mathf.Abs(
                                        progression.GetCampaignWaveEnemyCount(level, line, room, wave)
                                        - roomTotal * shares[wave - 1]) < 1f),
                                "Level " + level + " room " + line + "-" + room
                                + " distributes the unchanged total across five waves");
                        }
                    }
                    for (int room = 1; room <= 7; room++)
                    {
                        if (!progression.IsRoomActive(level, 4, room)) continue;
                        int roomTotal = progression.GetCampaignRoomEnemyTotal(level, 4, room);
                        Check(progression.GetCampaignWaveEnemyCount(level, 4, room, 1) == 6
                            && progression.GetCampaignWaveEnemyCount(level, 4, room, 2) == 4
                            && progression.GetCampaignWaveEnemyCount(level, 4, room, 3) == 2
                            && progression.GetCampaignWaveEnemyCount(level, 4, room, 4) == 2
                            && progression.GetCampaignWaveEnemyCount(level, 4, room, 5) == 1
                            && roomTotal == 15
                            && Enumerable.Range(1, LevelProgression.CampaignWavesPerRoom)
                                .All(wave => progression.GetCampaignWaveEnemyCount(
                                    level, 4, room, wave)
                                    <= LevelProgression.GetCampaignWaveEnemyLimit(4)),
                            "Level " + level + " room 4-" + room
                            + " projects boss pressure with six starting enemies");
                    }
                }
                Check(progression.GetMaximumEnemyTypesForWave(1, 1) == 1,
                    "Wave one allows one enemy type");
                Check(progression.GetMaximumEnemyTypesForWave(2, 2) == 2,
                    "Wave two allows two enemy types");
                Check(progression.GetMaximumEnemyTypesForWave(3, 3) == 3,
                    "Wave three allows three enemy types on inner lines");
                Check(progression.GetMaximumEnemyTypesForWave(4, 3) == 3,
                    "Fourth-line waves use the same per-wave type limit as other lines");
            }
            if (manager != null && progression != null)
            {
                GameObject baseEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Enemy/Brick.prefab");
                Check(baseEnemy != null && Mathf.Approximately(
                        baseEnemy.GetComponent<Brick>().hp, 100f),
                    "The first level and first wave start with exactly 100 enemy HP");
                string[] ordinaryPaths = {
                    "Assets/Resources/Enemies/Zombie.prefab",
                    "Assets/Resources/Enemies/ArmoredSkeleton.prefab",
                    "Assets/Resources/Enemies/LichRegular.prefab"
                };
                foreach (string path in ordinaryPaths)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Brick enemy = prefab != null ? prefab.GetComponent<Brick>() : null;
                    Check(enemy != null && !enemy.UsesBossHealth
                        && Mathf.Approximately(enemy.hp, baseEnemy.GetComponent<Brick>().hp)
                        && Mathf.Approximately(enemy.HealthCoefficient, 1f),
                        path + " starts from ordinary HP with an editable coefficient");
                }
                GameObject ghostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Enemies/Ghost.prefab");
                Brick ghost = ghostPrefab != null ? ghostPrefab.GetComponent<Brick>() : null;
                Check(ghost != null && !ghost.UsesBossHealth
                    && Mathf.Approximately(ghost.hp, baseEnemy.GetComponent<Brick>().hp * 0.8f)
                    && Mathf.Approximately(ghost.HealthCoefficient, 0.8f),
                    "Ghost starts with 80% of ordinary HP through its editable coefficient");
                string[] bossPaths = {
                    "Assets/Resources/Enemies/LichBoss.prefab",
                    "Assets/Resources/Enemies/BoneDragonMiniBoss.prefab",
                    "Assets/Resources/Enemies/BoneDragonBoss.prefab",
                    "Assets/Resources/Enemies/GhostlySorrowBoss.prefab",
                    "Assets/Resources/Enemies/PoisonousGloomBoss.prefab",
                    "Assets/Resources/Enemies/DrownedMiniBoss.prefab",
                    "Assets/Resources/Enemies/MirageBoss.prefab",
                    "Assets/Resources/Enemies/HiveHollowMiniBoss.prefab",
                    "Assets/Resources/Enemies/GreenJudgeMiniBoss.prefab",
                    "Assets/Resources/Enemies/BellRingerBoss.prefab",
                    "Assets/Resources/Enemies/KnotBinderBoss.prefab",
                    "Assets/Resources/Enemies/VolleyEaterBoss.prefab",
                    "Assets/Resources/Enemies/ThreeSealIdolBoss.prefab",
                    "Assets/Resources/Enemies/ImpostorBoss.prefab"
                };
                foreach (string path in bossPaths)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Check(prefab != null && prefab.GetComponent<Brick>()?.UsesBossHealth == true,
                        path + " keeps independent boss HP");
                }
                float[] expectedWaves = { 1f, 1.25f, 1.5f, 1.75f, 2f };
                Check(Enumerable.Range(1, expectedWaves.Length).All(wave =>
                        Mathf.Approximately(LevelProgression.GetCampaignWaveHealthMultiplier(wave),
                            expectedWaves[wave - 1])),
                    "Campaign wave health follows 100, 125, 150, 175 and 200 percent");
                Check(Mathf.Approximately(progression.GetEnemyHealthMultiplier(1), 1f)
                        && Mathf.Approximately(progression.GetEnemyHealthMultiplier(2), (float)Math.Pow(1.125, 1.9))
                        && Mathf.Approximately(progression.GetEnemyHealthMultiplier(3), (float)Math.Pow(1.25, 1.9)),
                    "Campaign level health follows the approved diminishing curve");
            }

            if (failures.Count > 0)
                throw new InvalidOperationException("Campaign wave validation failed:\n- "
                    + string.Join("\n- ", failures));
            Debug.Log("CampaignWaveRulesValidation passed: first-line budgets, boss-pressure projections and 100 starting HP.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    public static void RunPlayBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Directory.CreateDirectory("Logs");
        failures.Clear();
        SessionState.SetBool(PlayValidationRunning, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(6);
        EditorSceneManager.OpenScene(GameplayScene);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode
            || !SessionState.GetBool(PlayValidationRunning, false)) return;
        playStartedAt = EditorApplication.timeSinceStartup;
        playDeadline = playStartedAt + 45d;
        EditorApplication.update += TickPlayValidation;
    }

    private static void TickPlayValidation()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > playDeadline)
                throw new TimeoutException("Campaign wave play validation timed out.");
            if (EditorApplication.timeSinceStartup - playStartedAt < 0.8d) return;

            GameManager manager = GameManager.Instance;
            LevelProgression progression = LevelProgression.Instance;
            LevelGenerator room = manager != null && manager.Front != null
                ? manager.Front.GetGenerator(DefenseFront.IdFor(1, 1)) : null;
            Check(manager != null && progression != null && room != null && room.IsInitialized,
                "Level six starts with an initialized first room");
            Check(room.SpawnedWaveCount == 1
                    && room.RemainingWaves == LevelProgression.CampaignWavesPerRoom - 1,
                "The room starts on wave one with exactly four waves remaining");
            Check(room.EnemyCollection.Count
                    == progression.GetCampaignWaveEnemyCount(6, 1, 1, 1),
                "The complete first wave is present");
            Check(CountTypes(room.EnemyCollection) == 1,
                "Actual first-wave enemies use one type");
            var encounteredTypes = new HashSet<string>(room.EnemyCollection
                .Where(enemy => enemy != null).Select(TypeId));

            for (int wave = 2; wave <= LevelProgression.CampaignWavesPerRoom; wave++)
            {
                HashSet<int> before = InstanceIds(room.EnemyCollection);
                room.SpawnNextWave();
                List<GameObject> spawned = NewEnemies(room.EnemyCollection, before);
                Check(spawned.Count == progression.GetCampaignWaveEnemyCount(6, 1, 1, wave)
                        && spawned.Count <= LevelProgression.GetCampaignWaveEnemyLimit(1)
                        && CountTypes(spawned) <= progression.GetMaximumEnemyTypesForWave(1, wave),
                    "Actual wave " + wave + " matches its plan and first-line cap");
                foreach (GameObject enemy in spawned) encounteredTypes.Add(TypeId(enemy));
                if (wave == 2)
                    Check(spawned.Any(enemy => TypeId(enemy) == "ghost"),
                        "The second wave introduces the Ghost after the local ordinary type");
                if (wave == 3)
                    Check(spawned.Any(enemy => TypeId(enemy) == "armored_skeleton"),
                        "The third wave introduces the armored skeleton");
                float waveMultiplier = LevelProgression.GetCampaignWaveHealthMultiplier(wave);
                Check(spawned.All(enemy =>
                    {
                        Brick live = enemy.GetComponent<Brick>();
                        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(enemy);
                        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        Brick source = template != null ? template.GetComponent<Brick>() : null;
                        return source != null && Mathf.Approximately(live.MaxHp,
                            room.OrdinaryEnemyBaseHealth * source.HealthCoefficient
                            * progression.EnemyHealthMultiplier * waveMultiplier);
                    }),
                    "Actual wave " + wave + " uses ordinary HP and its editable coefficient");
            }
            Check(room.SpawnedWaveCount == LevelProgression.CampaignWavesPerRoom
                    && room.RemainingWaves == 0,
                "The campaign room stops after its fifth wave");
            Check(encounteredTypes.SetEquals(new[] {
                    "gloom_skull", "ghost", "armored_skeleton", "lich", "shadow_weaver" }),
                "All five Gloomy Hollow enemy types appear in the room");

            FinishPlayValidation(null);
        }
        catch (Exception error)
        {
            FinishPlayValidation(error);
        }
    }

    private static HashSet<int> InstanceIds(IEnumerable<GameObject> enemies)
    {
        return new HashSet<int>(enemies.Where(enemy => enemy != null)
            .Select(enemy => enemy.GetInstanceID()));
    }

    private static List<GameObject> NewEnemies(IEnumerable<GameObject> enemies, HashSet<int> before)
    {
        return enemies.Where(enemy => enemy != null && !before.Contains(enemy.GetInstanceID())).ToList();
    }

    private static int CountTypes(IEnumerable<GameObject> enemies)
    {
        return enemies.Where(enemy => enemy != null).Select(TypeId).Distinct().Count();
    }

    private static string TypeId(GameObject enemy)
    {
        EnemyFirstEncounterProfile profile = enemy.GetComponent<EnemyFirstEncounterProfile>();
        return profile != null ? profile.EnemyTypeId : enemy.name.Replace("(Clone)", string.Empty);
    }

    private static void FinishPlayValidation(Exception error)
    {
        EditorApplication.update -= TickPlayValidation;
        SessionState.SetBool(PlayValidationRunning, false);
        Time.timeScale = 1f;
        string result = error == null
            ? "PASS: live five-wave composition, line cap and wave-based HP growth checks."
            : "FAIL: " + error;
        File.WriteAllText("Logs/CampaignWaveRulesPlayValidation.txt", result);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) failures.Add(description);
    }
}
