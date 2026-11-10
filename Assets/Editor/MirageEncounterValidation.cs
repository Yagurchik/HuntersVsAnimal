using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MirageEncounterValidation
{
    private const string RunningKey = "ForestAnimalWar.MirageEncounterValidation.Running";
    private static IEnumerator checks;
    private static double deadline;

    static MirageEncounterValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(RunningKey, false)) return;
            checks = CheckEncounter();
            deadline = EditorApplication.timeSinceStartup + 60d;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
        SessionState.SetBool(RunningKey, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(6);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckEncounter()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        LevelProgression progression = LevelProgression.Instance;
        Require(manager != null && progression != null && manager.Front != null,
            "Campaign services are ready");
        Require(progression.CurrentLevel == 6
            && progression.IsMirageRoom(6, 4, 4)
            && !progression.IsMirageRoom(6, 4, 1)
            && !progression.IsMirageRoom(6, 4, 7)
            && !progression.IsMirageRoom(6, 4, 2)
            && !progression.IsMirageRoom(7, 4, 2)
            && progression.IsMirageRoom(7, 3, 3) && progression.IsMirageRoom(8, 2, 2),
            "Mirage starts in 4-4 then moves to 3-3 and 2-2 on the next two levels");

        GameObject miragePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            BossOctagonAuthoring.MiragePrefab);
        GameObject miniBossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/BoneDragonMiniBoss.prefab");
        Require(miragePrefab != null && miniBossPrefab != null
            && Mathf.Approximately(miragePrefab.GetComponent<Brick>().hp, 5550f)
            && Mathf.Approximately(miragePrefab.GetComponent<Brick>().hp,
                miniBossPrefab.GetComponent<Brick>().hp),
            "Mirage shares the mini-boss base health");

        manager.enabled = false;
        manager.isPaused = false;
        Time.timeScale = 1f;
        manager.Front.ActivateLine(2);
        manager.Front.ActivateLine(3);
        manager.Front.ActivateLine(4);
        foreach (int index in new[] { 4 })
        {
            LevelGenerator room = manager.Front.GetGenerator(DefenseFront.IdFor(4, index));
            Require(room != null && room.IsInitialized && room.IsCampaignBossRoom
                && room.SpawnedWaveCount == 1 && room.EnemyCollection.Count == 6,
                "Room 4-" + index + " starts with one boss and five ordinary enemies");
            GameObject enemy = room.EnemyCollection.Find(candidate => candidate != null
                && candidate.GetComponent<Brick>().UsesBossHealth);
            Brick brick = enemy != null ? enemy.GetComponent<Brick>() : null;
            Require(brick != null && enemy.GetComponent<MirageCornerMirror>() != null
                && enemy.GetComponent<BoxCollider2D>() != null
                && enemy.GetComponent<PolygonCollider2D>() == null
                && brick.Footprint == new Vector2Int(3, 3)
                && Mathf.Approximately(brick.MaxHp,
                    5550f * progression.EnemyHealthMultiplier),
                "Mirage replaces the mini-boss in room 4-" + index);
        }
        LevelGenerator mirrorRoom = manager.Front.GetGenerator(DefenseFront.IdFor(4, 4));
        GameObject mirrorBoss = mirrorRoom.EnemyCollection.Find(candidate => candidate != null
            && candidate.GetComponent<Brick>().UsesBossHealth);
        BoxCollider2D mirror = mirrorBoss.GetComponent<BoxCollider2D>();
        GameObject probe = new GameObject("Mirage return reflection probe");
        probe.layer = AnimalProjectileQueue.Instance.Animals[0].Prefab.layer;
        probe.transform.position = new Vector3(mirror.bounds.min.x + 0.25f,
            mirror.bounds.min.y - 0.7f, 0f);
        probe.AddComponent<Rigidbody2D>();
        probe.AddComponent<CircleCollider2D>().radius = 0.1f;
        ProjectilePhysics2D flight = probe.AddComponent<ProjectilePhysics2D>();
        flight.Launch(Vector2.up, 8f);
        bool reflectedBack = false;
        float reflectionDeadline = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < reflectionDeadline)
        {
            if (flight.FlightDirection.y < -0.98f)
            {
                reflectedBack = true;
                break;
            }
            yield return null;
        }
        UnityEngine.Object.Destroy(probe);
        Require(reflectedBack,
            "A real upward projectile returns downward from Mirage's lower-left corner");
        LevelGenerator ordinaryRoom = manager.Front.GetGenerator(DefenseFront.IdFor(4, 2));
        Require(ordinaryRoom != null && ordinaryRoom.IsInitialized && !ordinaryRoom.IsCampaignBossRoom,
            "Room 4-2 is open with ordinary enemies on this level");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Mirage encounter validation timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(RunningKey, false);
        if (error == null) Debug.Log("Mirage encounter validation passed: room 4-4.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
