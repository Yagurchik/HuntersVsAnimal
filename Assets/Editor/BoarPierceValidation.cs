using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in an isolated editor copy: -executeMethod BoarPierceValidation.RunBatch
[InitializeOnLoad]
public static class BoarPierceValidation
{
    private const string Running = "ForestAnimalWar.BoarPierceValidation.Running";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;

    static BoarPierceValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            steps.Clear();
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
        LevelProgression.PrepareLevel(1);
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
        Stage stage = room != null ? room.GetComponentInParent<Stage>() : null;
        Check(stage != null, "Main battlefield exists");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/AnimalsBullet/Boar.prefab");
        Check(prefab != null, "Boar prefab exists");
        GameObject projectile = Object.Instantiate(prefab);
        BoarProjectile boar = projectile.GetComponent<BoarProjectile>();
        Check(boar != null, "Boar projectile is installed");
        typeof(BoarProjectile).GetField("currentStage",
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boar, stage);
        MethodInfo clamp = typeof(BoarProjectile).GetMethod("ClampVisiblePosition",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo travel = typeof(BoarProjectile).GetMethod("TravelAlongSegment",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(clamp != null && travel != null, "Boar has its visible-path sweep");

        Vector2 center = stage.transform.position;
        float top = center.y + stage.BattlefieldSize.y * 0.5f;
        Vector2 logicalStart = new Vector2(center.x - 2.5f, top);
        Vector2 logicalEnd = new Vector2(center.x + 2.5f, top);
        Vector2 visibleMiddle = (Vector2)clamp.Invoke(boar,
            new object[] { new Vector2(center.x, top) });
        Check(top - visibleMiddle.y > boar.DamageSweepRadius + 0.1f,
            "Boar visual path differs materially from its wall ray");

        Brick first = MakeTarget(new Vector2(center.x - 1f, visibleMiddle.y));
        Brick second = MakeTarget(new Vector2(center.x + 1f, visibleMiddle.y));
        Physics2D.SyncTransforms();
        IEnumerator segment = (IEnumerator)travel.Invoke(boar,
            new object[] { logicalStart, logicalEnd });
        while (segment.MoveNext()) yield return null;
        Check(first.hp < 1000f && second.hp < 1000f,
            "Boar damages every enemy it visibly passes through near a wall");
        float firstHp = first.hp;
        float secondHp = second.hp;
        segment = (IEnumerator)travel.Invoke(boar,
            new object[] { logicalStart, logicalEnd });
        while (segment.MoveNext()) yield return null;
        Check(Mathf.Approximately(first.hp, firstHp)
            && Mathf.Approximately(second.hp, secondHp),
            "Each enemy is damaged only once on the same charge segment");
    }

    private static Brick MakeTarget(Vector2 position)
    {
        GameObject target = new GameObject("Boar sweep target");
        target.transform.position = position;
        BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.16f, 0.16f);
        Brick brick = target.AddComponent<Brick>();
        brick.hp = 1000f;
        typeof(Brick).GetField("maxHp", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(brick, 1000f);
        return brick;
    }

    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Boar pierce validation timed out");
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
