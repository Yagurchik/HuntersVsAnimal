using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Integration test for an isolated batch-mode project copy.
[InitializeOnLoad]
public static class BossAbsorptionValidation
{
    private const string Running = "ForestAnimalWar.BossAbsorptionValidation.Running";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;

    static BossAbsorptionValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            steps.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run only in an isolated Unity project copy.");
        SessionState.SetBool(Running, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(5);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        Check(manager != null && manager.Front != null, "Defense front is ready");
        manager.enabled = false;
        manager.isPaused = false;
        foreach (Player player in Object.FindObjectsOfType<Player>()) player.enabled = false;
        LevelGenerator room = manager.Front.GetGenerator(0);
        Check(room != null && room.IsInitialized, "Main room is initialized");
        Clear(room);

        // Every safe landing cell is occupied. The resident miniboss blocks its
        // whole 2x2 footprint, while lesser enemies are legal soul sacrifices.
        Brick resident = AddResident(room, "Enemies/DrownedMiniBoss", new Vector2Int(0, 0));
        var grid = new EnemyRoomGrid(room, room.SafeReceivingRows);
        for (int y = 0; y < room.SafeReceivingRows; y++)
        for (int x = 0; x < room.Columns; x++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (!grid.CanPlace(cell, Vector2Int.one)) continue;
            Brick skeleton = AddResident(room, "Enemies/ArmoredSkeleton", cell);
            skeleton.hp = 10f; // Verify current HP, not maximum HP, is stolen.
        }
        int ordinaryBefore = OrdinaryCount(room);
        int playerExperience = manager.CurrentExperience;
        int plannedKills = (int)typeof(GameManager)
            .GetField("defeatedPlannedEnemies", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(manager);
        Brick arriving = NewTravellingBoss("Enemies/BoneDragonBoss");
        float hpBefore = arriving.hp;
        float maxBefore = arriving.MaxHp;
        Check(room.TryReceiveBossByAbsorption(arriving), "Crowded room accepts the boss");
        int absorbedCount = ordinaryBefore - OrdinaryCount(room);
        Check(absorbedCount > 0 && Mathf.Approximately(arriving.hp, hpBefore + absorbedCount * 10f)
            && Mathf.Approximately(arriving.MaxHp, maxBefore + absorbedCount * 10f),
            "Boss steals current HP and increases its maximum HP");
        Check(room.EnemyCollection.Contains(resident.gameObject)
            && !Overlap(room, arriving, resident),
            "Incoming boss never overlaps or consumes a resident miniboss");
        Check(manager.CurrentExperience == playerExperience
            && (int)typeof(GameManager).GetField("defeatedPlannedEnemies",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager) == plannedKills,
            "Absorbed enemies award no experience and do not count as player kills");
        Check(SoulAbsorptionEffect.ActiveCount == absorbedCount,
            "One authored soul-travel effect appears per consumed enemy");

        // Cohorts may contain a boss and escorts. The boss arrives now, while
        // escorts remain in transit instead of blocking the entire cohort.
        manager.Front.ActivateLine(2);
        LevelGenerator source = manager.Front.GetGenerator(DefenseFront.IdFor(2, 1));
        Brick secondBoss = NewTravellingBoss("Enemies/LichBoss");
        Brick escort = NewTravellingBoss("Enemies/ArmoredSkeleton");
        int cohort = manager.Front.CreatePressureCohort();
        manager.Front.SendToInnerRoom(source, secondBoss, 0, cohort);
        manager.Front.SendToInnerRoom(source, escort, 0, cohort);
        manager.Front.DeliverPressure();
        Check(secondBoss.levelGenerator == room && escort.levelGenerator == null
            && manager.Front.PendingTransferEnemies == 1,
            "Boss leaves a crowded transfer cohort; ordinary escort keeps waiting");
        Check(!Overlap(room, secondBoss, resident) && !Overlap(room, secondBoss, arriving),
            "Two bosses and a miniboss never share cells after cohort arrival");

        manager.Front.RemoveTravellingEnemy(escort);
        Object.DestroyImmediate(escort.gameObject);
        Clear(room);
        for (int y = 0; y <= 6; y += 3)
        for (int x = 0; x <= 3; x += 3)
            AddResident(room, "Enemies/BoneDragonMiniBoss", new Vector2Int(x, y));
        Brick blockedBoss = NewTravellingBoss("Enemies/BoneDragonBoss");
        Check(!room.TryReceiveBossByAbsorption(blockedBoss)
            && blockedBoss.levelGenerator == null && !blockedBoss.gameObject.activeInHierarchy,
            "No landing is allowed when every boss footprint overlaps another boss");
        manager.Front.SendToInnerRoom(source, blockedBoss);
        manager.Front.DeliverPressure();
        Check(manager.Front.PendingTransferEnemies == 1 && blockedBoss.levelGenerator == null,
            "A blocked boss stays in the transfer queue until a legal place opens");
        Debug.Log("Boss absorption validation: PASS");
    }

    private static Brick AddResident(LevelGenerator room, string resource, Vector2Int anchor)
    {
        GameObject prefab = Resources.Load<GameObject>(resource);
        Check(prefab != null, resource + " prefab exists");
        Brick brick = Object.Instantiate(prefab, room.transform).GetComponent<Brick>();
        brick.transform.position = new EnemyRoomGrid(room, room.Rows).Center(anchor, brick.Footprint);
        brick.levelGenerator = room;
        room.EnemyCollection.Add(brick.gameObject);
        return brick;
    }

    private static Brick NewTravellingBoss(string resource)
    {
        GameObject prefab = Resources.Load<GameObject>(resource);
        Check(prefab != null, resource + " prefab exists");
        Brick brick = Object.Instantiate(prefab).GetComponent<Brick>();
        brick.levelGenerator = null;
        brick.gameObject.SetActive(false);
        return brick;
    }

    private static int OrdinaryCount(LevelGenerator room) => room.EnemyCollection.Count(enemy =>
        enemy != null && enemy.TryGetComponent(out Brick brick) && !brick.UsesBossHealth);

    private static bool Overlap(LevelGenerator room, Brick first, Brick second)
    {
        var grid = new EnemyRoomGrid(room, room.Rows);
        Vector2Int a = grid.Anchor(first.transform.position, first.Footprint);
        Vector2Int b = grid.Anchor(second.transform.position, second.Footprint);
        return a.x < b.x + second.Footprint.x && a.x + first.Footprint.x > b.x
            && a.y < b.y + second.Footprint.y && a.y + first.Footprint.y > b.y;
    }

    private static void Clear(LevelGenerator room)
    {
        foreach (GameObject enemy in room.EnemyCollection.ToArray())
        {
            room.EnemyCollection.Remove(enemy);
            if (enemy != null) Object.DestroyImmediate(enemy);
        }
    }

    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Boss absorption validation timed out");
            while (steps.Count > 0)
            {
                IEnumerator current = steps.Peek();
                if (!current.MoveNext()) { steps.Pop(); continue; }
                if (current.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Debug.Log("PASS: " + description);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        if (error != null) Debug.LogError("FAIL: " + error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
