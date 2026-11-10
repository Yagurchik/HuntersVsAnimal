using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// RunBatch is intended for an isolated Unity project copy, never the open editor.
[InitializeOnLoad]
public static class BossBreachSummonValidation
{
    private const string Running = "ForestAnimalWar.BossBreachSummonValidation.Running";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;

    static BossBreachSummonValidation()
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
            throw new InvalidOperationException("Run only in an isolated batch-mode project.");
        Directory.CreateDirectory("Logs");
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
        Check(manager != null && manager.Front != null, "Campaign front is ready");
        manager.enabled = false;
        manager.isPaused = false;
        foreach (Player player in Object.FindObjectsOfType<Player>()) player.enabled = false;
        LevelGenerator room = manager.Front.GetGenerator(0);
        Check(room != null && room.IsInitialized, "Main room is initialized");
        Clear(room);

        Brick lich = AddBoss(room, "Enemies/LichBoss");
        PlaceBeforeBreach(room, lich);
        LichSummoner lichSummoner = lich.GetComponent<LichSummoner>();
        Check(lichSummoner != null, "Lich has its summoner");
        lichSummoner.CompleteEnemyTurn(1);
        Check(lichSummoner.LastSummonCount > 0 && Summons(room, lich).All(enemy =>
            enemy.LowestCellY > room.DefenseBoundaryY),
            "Lich summons in free safe cells at the breach");
        Clear(room, lich);
        Check(room.ClosestBreachInTurns == 1,
            "Lich appears in the one-turn breach warning");
        float barricadeBeforeLich = manager.Front.BarricadeHealth;
        lich.Step();
        while (lich.IsStepping) yield return null;
        Check(lich.LowestCellY <= room.DefenseBoundaryY + 0.0001f,
            "Lich reaches the breach row");
        room.ResolveRequestedBreach();
        Check(manager.Front.BarricadeHealth < barricadeBeforeLich
            && lich.LowestCellY > room.DefenseBoundaryY,
            "Lich damages the barricade and returns to the upper field");
        Clear(room);

        Brick dragon = AddBoss(room, "Enemies/BoneDragonBoss");
        PlaceBeforeBreach(room, dragon);
        BossEnemyTurnController controller = dragon.GetComponent<BossEnemyTurnController>();
        Check(controller != null, "Bone dragon has its turn controller");
        for (int turn = 1; turn <= 3; turn++) controller.CompleteEnemyTurn(turn);
        Check(controller.LastSummonCount > 0 && Summons(room, dragon).All(enemy =>
            enemy.LowestCellY > room.DefenseBoundaryY && Mathf.Approximately(enemy.hp, 1f)),
            "Bone dragon summons 1-HP skeletons into safe cells at the breach");
        Clear(room, dragon);
        Check(room.ClosestBreachInTurns == 1,
            "Two-step dragon appears in the one-turn breach warning");
        float barricadeBeforeDragon = manager.Front.BarricadeHealth;
        dragon.Step();
        while (dragon.IsStepping) yield return null;
        Check(dragon.LowestCellY <= room.DefenseBoundaryY + 0.0001f,
            "Two-step dragon takes its last legal step onto the breach row");
        room.ResolveRequestedBreach();
        Check(manager.Front.BarricadeHealth < barricadeBeforeDragon
            && dragon.LowestCellY > room.DefenseBoundaryY,
            "Bone dragon breaches and returns without leaving the board");

        Clear(room, dragon);
        var grid = new EnemyRoomGrid(room, room.Rows);
        dragon.transform.position = grid.Center(Vector2Int.zero, dragon.Footprint);
        Brick adjacent = AddOrdinary(room, grid.Center(new Vector2Int(1, 3), Vector2Int.one));
        Vector3 dragonStart = dragon.transform.position;
        room.StepEnemy();
        Check(adjacent.IsStepping && !dragon.IsStepping
            && dragon.transform.position == dragonStart,
            "Bone dragon stops before an adjacent enemy already moving this turn");
        while (adjacent.IsStepping) yield return null;

        Clear(room, dragon);
        dragon.transform.position = grid.Center(Vector2Int.zero, dragon.Footprint);
        Brick twoCellsAhead = AddOrdinary(room,
            grid.Center(new Vector2Int(1, 4), Vector2Int.one));
        dragonStart = dragon.transform.position;
        room.StepEnemy();
        while (dragon.IsStepping || twoCellsAhead.IsStepping) yield return null;
        Check(dragon.transform.position == dragonStart + Vector3.down
            && twoCellsAhead.transform.position == grid.Center(
                new Vector2Int(1, 5), Vector2Int.one),
            "Bone dragon uses only its first legal step when an enemy blocks the second");
        Clear(room);

        for (int line = 2; line <= 4; line++) manager.Front.ActivateLine(line);
        LevelGenerator outerRoom = manager.Front.GetGenerator(DefenseFront.IdFor(4, 1));
        Check(outerRoom != null && outerRoom.IsInitialized, "Outer room is initialized");
        Clear(outerRoom);
        Brick outerLich = AddBoss(outerRoom, "Enemies/LichBoss");
        PlaceBeforeBreach(outerRoom, outerLich);
        outerLich.Step();
        while (outerLich.IsStepping) yield return null;
        outerRoom.ResolveRequestedBreach();
        manager.Front.DeliverPressure();
        Check(!outerRoom.IsLost && manager.Front.Model.GetRoom(outerRoom.RoomId).CanSelect
                && outerLich.levelGenerator != outerRoom,
            "Large boss crosses toward the next line while its outer room stays playable");
        Debug.Log("Boss breach and summon validation: PASS");
    }

    private static Brick AddBoss(LevelGenerator room, string resource)
    {
        GameObject prefab = Resources.Load<GameObject>(resource);
        Check(prefab != null, resource + " prefab is available");
        Brick boss = Object.Instantiate(prefab, room.transform).GetComponent<Brick>();
        boss.levelGenerator = room;
        boss.dmg = 5f;
        room.EnemyCollection.Add(boss.gameObject);
        return boss;
    }

    private static Brick AddOrdinary(LevelGenerator room, Vector3 position)
    {
        Brick enemy = Object.Instantiate(room.brickPrefab, position,
            Quaternion.identity, room.transform).GetComponent<Brick>();
        enemy.levelGenerator = room;
        room.EnemyCollection.Add(enemy.gameObject);
        return enemy;
    }

    private static void PlaceBeforeBreach(LevelGenerator room, Brick boss)
    {
        float lowest = room.DefenseBoundaryY + 0.5f;
        boss.transform.position = room.transform.position + new Vector3(
            (boss.Footprint.x - 1) * 0.5f,
            lowest - room.transform.position.y + (boss.Footprint.y - 1) * 0.5f, 0f);
    }

    private static Brick[] Summons(LevelGenerator room, Brick boss) => room.EnemyCollection
        .Where(enemy => enemy != null && enemy != boss.gameObject)
        .Select(enemy => enemy.GetComponent<Brick>()).Where(enemy => enemy != null).ToArray();

    private static void Clear(LevelGenerator room, Brick keep = null)
    {
        foreach (GameObject enemy in room.EnemyCollection.ToArray())
        {
            if (enemy == null || enemy == keep?.gameObject) continue;
            room.EnemyCollection.Remove(enemy);
            Object.DestroyImmediate(enemy);
        }
    }

    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Boss breach validation timed out");
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

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
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
