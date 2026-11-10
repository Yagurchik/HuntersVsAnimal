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
public static class GloomyHollowValidation
{
    private const string Running = "ForestAnimalWar.GloomyHollowValidation.Running";
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static readonly List<string> report = new List<string>();
    private static double deadline;

    static GloomyHollowValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90d;
            stack.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        Directory.CreateDirectory("Logs");
        SessionState.SetBool(Running, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(6);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        Check(manager != null && LevelProgression.Instance != null, "Gameplay services started");
        manager.enabled = false;
        Check(LevelProgression.Instance.CurrentLevel == 6
            && LevelProgression.Instance.CurrentLocationIndex == 2
            && LevelProgression.Instance.CurrentLocalLevel == 1
            && LevelProgression.CurrentLocationName == LevelProgression.GloomyHollowName,
            "Global level 6 is Gloomy Hollow level 1");

        for (int local = 1; local <= 5; local++)
        {
            int global = LevelProgression.GlobalLevelFor(2, local);
            int captured = global;
            int expectedOuter = local == 5 ? 7 : 0;
            Check(Enumerable.Range(1, 7)
                .Count(index => LevelProgression.Instance.IsRoomActive(captured, 4, index)) == expectedOuter,
                "Gloomy Hollow level " + local + " opens " + expectedOuter + " fourth-line rooms");
        }

        LevelGenerator room = manager.Front.GetGenerator(DefenseFront.IdFor(1, 1));
        Check(room != null && room.IsInitialized, "First Gloomy Hollow room is initialized");
        Check(room.EnemyCollection.Select(enemy => enemy.GetComponent<Brick>())
            .All(enemy => enemy.GetComponent<LichSummoner>() == null),
            "The first wave contains only the base enemy type");
        room.SpawnNextWave();
        Check(room.EnemyCollection.Select(enemy => enemy.GetComponent<EnemyFirstEncounterProfile>())
            .Where(profile => profile != null).Select(profile => profile.EnemyTypeId)
            .Distinct().Count() <= 2,
            "The second wave raises room diversity to no more than two types");
        room.SpawnNextWave();
        Brick[] enemies = room.EnemyCollection.Where(enemy => enemy != null)
            .Select(enemy => enemy.GetComponent<Brick>()).Where(enemy => enemy != null).ToArray();
        Brick lich = enemies.FirstOrDefault(enemy => enemy.GetComponent<LichSummoner>() != null);
        Check(lich != null, "Third wave includes regular Lich; spawned waves "
            + room.SpawnedWaveCount + ", enemies " + enemies.Length);
        Check(lich.Footprint == Vector2Int.one, "Ordinary Gloomy Hollow Lich occupies one cell");
        Check(lich.GetComponent<BoxCollider2D>() != null
            && lich.GetComponent<BoxCollider2D>().size == new Vector2(0.9f, 0.9f),
            "Ordinary Lich has a one-cell collider");
        Check(lich.ExperienceReward == 30, "Ordinary Lich awards its own experience reward");
        int plannedRoomEnemies = Enumerable.Range(1, 3)
            .Sum(wave => LevelProgression.Instance.GetCampaignWaveEnemyCount(1, 1, wave));
        Check(enemies.Length == plannedRoomEnemies
            && room.RemainingWaves == LevelProgression.CampaignWavesPerRoom - 3,
            "Ordinary Lich belongs to the third of four waves without exceeding the plan");
        Check(Object.FindObjectsOfType<LichSummoner>().All(value =>
                value.GetComponent<Brick>().Footprint == Vector2Int.one
                || value.GetComponent<Brick>().levelGenerator.IsCampaignBossRoom),
            "Boss-sized Liches carried from the previous level stay in central boss rooms");

        LichSummoner summoner = lich.GetComponent<LichSummoner>();
        int before = room.EnemyCollection.Count;
        summoner.CompleteEnemyTurn(8765);
        Check(summoner.LastSummonCount == 1 && room.EnemyCollection.Count == before + 1,
            "Ordinary Lich summons one skeleton per enemy turn");
        Brick summoned = room.EnemyCollection.Select(item => item.GetComponent<Brick>())
            .Single(item => item != null && item.IsSummonedVariant);
        Color tint = summoned.GetComponentInChildren<SpriteRenderer>().color;
        Check(summoned.ExperienceReward == 0 && tint.g > tint.r && tint.g > tint.b,
            "Its green summoned skeleton grants no experience");
        Check(Mathf.Approximately(summoned.MaxHp,
                room.OrdinaryEnemyBaseHealth * summoned.HealthCoefficient
                * LevelProgression.Instance.EnemyHealthMultiplier
                * LevelProgression.GetCampaignWaveHealthMultiplier(room.SpawnedWaveCount)),
            "Ordinary Lich summons use the common non-boss HP");

        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LichBossAuthoring.PrefabPath);
        GameObject regularPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GloomyHollowAuthoring.RegularLichPath);
        Check(bossPrefab != null && regularPrefab != null && bossPrefab != regularPrefab,
            "Boss and ordinary Lich remain independent editable prefabs");
        Check(bossPrefab.GetComponent<Brick>().Footprint == new Vector2Int(2, 2)
            && regularPrefab.GetComponent<Brick>().Footprint == Vector2Int.one,
            "Creating the ordinary Lich did not change the Twilight Thicket boss");

        GameObject sorrow = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/GhostlySorrowBoss.prefab");
        Check(LevelProgression.Instance.IsBossRoom(10, 4, 4)
            && sorrow != null && sorrow.GetComponent<Brick>().Footprint == new Vector2Int(3, 3)
            && sorrow.GetComponent<DragonBossIdleAnimator>() != null
            && sorrow.GetComponent<EnemyHitReaction>().DamageSprite != null
            && sorrow.GetComponent<BossEnemyTurnController>() == null
            && sorrow.GetComponent<GhostPhasing>() == null,
            "Ghostly Sorrow is the editable fifth-level center boss without special abilities");

    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Gloomy Hollow validation timed out.");
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator nested) stack.Push(nested);
                return;
            }
            Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("Gloomy Hollow validation failed: " + description);
        report.Add("PASS " + description);
    }

    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        string result = exception == null
            ? "PASS: " + report.Count + " Gloomy Hollow checks."
            : "FAIL: " + exception;
        report.Add(result);
        File.WriteAllLines("Logs/GloomyHollowValidation.txt", report);
        if (exception == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(exception == null ? 0 : 1);
    }
}
