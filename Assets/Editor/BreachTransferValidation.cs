using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Uses the authored scene and prefabs in an isolated Unity project copy.
[InitializeOnLoad]
public static class BreachTransferValidation
{
    private const string Running = "ForestAnimalWar.BreachTransferValidation";
    private static IEnumerator checks;
    private static double deadline;
    private static int lastFrame = -1;
    private static readonly List<string> passed = new List<string>();

    static BreachTransferValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            checks = Checks();
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        Directory.CreateDirectory("Logs");
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(6);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        float[][] distances = {
            new[] { 0f, 6f, 2f, 1f, 4f, 3f },
            new[] { 0f, 0f, 5f, 2f, 1f, 4f },
            new[] { 0f, 5f, 0f, 2f, 0f, 4f },
            new[] { 0f, 0f, 0f, 0f, 0f, 5f },
            new[] { 0f, 4f, 2f },
            new[] { 0f, 1.2f, 1.3f, 1.1f, 4f, 0.9f },
            new[] { 0f, 6f, 2f, 1f, 4f, 3f }
        };
        for (int scenario = 0; scenario < distances.Length; scenario++)
        {
            if (scenario > 0)
                EditorSceneManager.LoadSceneInPlayMode("Assets/Asset.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            GameManager manager = GameManager.Instance;
            Require(manager != null && manager.Front != null, "Native scene starts");
            manager.enabled = false;
            manager.isPaused = false;
            foreach (Player player in Object.FindObjectsOfType<Player>()) player.enabled = false;
            foreach (FirstLevelTutorial tutorial in Object.FindObjectsOfType<FirstLevelTutorial>()) tutorial.enabled = false;
            DefenseLineRuntime front = manager.Front;
            for (int line = 2; line <= 4; line++) front.ActivateLine(line);
            foreach (DefenseFront.Room modelRoom in front.Model.Rooms) Clear(front.GetGenerator(modelRoom.Id));
            LevelGenerator source = front.GetGenerator(DefenseFront.IdFor(3, 2));
            LevelGenerator sibling = front.GetGenerator(DefenseFront.IdFor(3, 1));
            LevelGenerator target = front.GetGenerator(DefenseFront.IdFor(2, 1));
            var enemies = new List<Brick>();
            for (int i = 0; i < distances[scenario].Length; i++)
                enemies.Add(Add(source, distances[scenario][i], i,
                    scenario == 5 && i == 5 ? "DrownedMiniBoss" : "SwampDemon"));
            Brick siblingEnemy = Add(sibling, 5f, 0, "SwampDemon");
            // Only enemies standing on the breach row leave the room.
            var expected = enemies.Where(e => source.GetTurnsUntilBreach(e) == 0).ToHashSet();
            if (scenario == 5)
                Require(source.GetTurnsUntilBreach(enemies[5]) <= source.GetTurnsUntilBreach(enemies[2])
                    && enemies[5].transform.position.y > enemies[2].transform.position.y,
                    "Boss breach distance uses its nearest occupied edge rather than its pivot");
            var health = enemies.ToDictionary(e => e, e => e.hp);
            int xp = manager.CurrentExperience;
            var delays = enemies.ToDictionary(e => e, e => source.GetTurnsUntilBreach(e));
            if (scenario == 6)
            {
                var grid = new EnemyRoomGrid(target, target.Rows);
                for (int row = 0; row < target.Rows; row++)
                for (int column = 0; column < target.Columns; column++)
                {
                    Brick blocker = Add(target, 1f, 0, "SwampDemon");
                    blocker.transform.position = grid.Center(new Vector2Int(column, row), Vector2Int.one);
                }
            }
            Brick[] attackers = enemies.Where(e => delays[e] == 0).ToArray();
            front.BreachRoom(source, attackers);
            var queue = (DefensePressureQueue<Brick>)typeof(DefenseLineRuntime)
                .GetField("pressure", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(front);
            Require(!source.IsLost && !sibling.IsLost
                && front.Model.GetRoom(source.RoomId).CanSelect && front.Model.GetRoom(sibling.RoomId).CanSelect,
                "A breach never loses or locks the source room or its sibling");
            Require(enemies.Where(e => !expected.Contains(e))
                    .All(e => e.levelGenerator == source && source.EnemyCollection.Contains(e.gameObject)),
                "Residents that did not reach the breach row stay in the source room");
            Require(queue.Passages.Select(p => p.Enemy).ToHashSet().SetEquals(expected)
                && queue.Passages.All(p => p.ReadyTurn == manager.TurnNumber),
                "Only the crossing enemies are queued, all ready in this phase");
            front.DeliverPressure();
            if (scenario == 6 && expected.Count > 0)
            {
                Require(expected.All(e => e.levelGenerator == null) && front.PendingTransferEnemies == expected.Count,
                    "A full destination retains the crossing enemies without overlap or loss");
                Clear(target);
                front.DeliverPressure();
            }
            Require(enemies.Where(e => e.levelGenerator == target).ToHashSet().SetEquals(expected),
                "Exactly the crossing enemies arrive in the same enemy phase: scenario " + scenario);
            Require(siblingEnemy.gameObject.activeSelf && siblingEnemy.levelGenerator == sibling,
                "Sibling residents stay in their own room");
            Require(enemies.All(e => Mathf.Approximately(e.hp, health[e])) && manager.CurrentExperience == xp,
                "Transfer preserves enemy HP and grants no kill experience");
            var occupied = new HashSet<Vector2Int>();
            var targetGrid = new EnemyRoomGrid(target, target.Rows);
            foreach (Brick enemy in expected)
            {
                Vector2Int anchor = targetGrid.Anchor(enemy.transform.position, enemy.Footprint);
                for (int y = 0; y < enemy.Footprint.y; y++)
                for (int x = 0; x < enemy.Footprint.x; x++)
                    Require(occupied.Add(anchor + new Vector2Int(x, y)), "Arrival footprints never overlap");
            }
            passed.Add("PASS scenario " + scenario + ": " + expected.Count + " crossing arrivals; rooms stay open, residents stay, HP/XP and occupancy verified.");
        }
        File.WriteAllLines("Logs/BreachTransferValidation.txt", passed);
    }

    private static Brick Add(LevelGenerator room, float distance, int column, string resource)
    {
        var prefab = Resources.Load<GameObject>("Enemies/" + resource);
        Require(prefab != null, "Missing prefab: " + resource);
        Brick enemy = Object.Instantiate(prefab, room.transform).GetComponent<Brick>();
        enemy.levelGenerator = room;
        enemy.transform.position = new Vector3(room.transform.position.x + column,
            room.DefenseBoundaryY + distance, room.transform.position.z);
        enemy.transform.position += Vector3.up * (enemy.transform.position.y - enemy.LowestCellY);
        room.EnemyCollection.Add(enemy.gameObject);
        return enemy;
    }

    private static void Clear(LevelGenerator room)
    {
        if (room == null) return;
        foreach (GameObject enemy in room.EnemyCollection.ToArray())
            if (enemy != null) Object.DestroyImmediate(enemy);
        room.EnemyCollection.Clear();
        room.InvalidateEnemyQueries();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Tick()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Breach transfer validation timed out");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        if (error != null)
        {
            File.WriteAllText("Logs/BreachTransferValidation.txt", string.Join("\n", passed) + "\nFAIL: " + error);
            Debug.LogException(error);
        }
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
