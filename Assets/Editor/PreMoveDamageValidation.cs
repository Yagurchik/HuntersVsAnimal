using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch-only gameplay regression. Run from an isolated project copy.
[InitializeOnLoad]
public static class PreMoveDamageValidation
{
    private const string Running = "ForestAnimalWar.PreMoveDamageValidation.Running";
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static readonly MethodInfo ResolveTurn = typeof(GameManager).GetMethod(
        "ResolveEnemyTurn", BindingFlags.Instance | BindingFlags.NonPublic);
    private static double deadline;
    private static int lastFrame = -1;

    static PreMoveDamageValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 60d;
            Steps.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Directory.CreateDirectory("Logs");
        Report.Clear();
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
        Check(manager != null && manager.Front != null && ResolveTurn != null,
            "Gameplay turn is available");
        manager.enabled = false;
        manager.isPaused = false;
        manager.stepGame = false;
        manager.stepPlayer = false;
        Time.timeScale = 1f;

        LevelGenerator room = manager.Front.GetGenerator(0);
        Check(room != null && room.EnemyCollection.Count > 0,
            "First room contains an enemy");
        Brick enemy = room.EnemyCollection[0].GetComponent<Brick>();
        enemy.transform.position = new Vector3(room.transform.position.x,
            room.transform.position.y + 2.25f, enemy.transform.position.z);
        enemy.hp = 1000f;
        EnemyStatusEffects statuses = enemy.GetComponent<EnemyStatusEffects>();
        Check(statuses != null && statuses.ApplyPeriodicDamage(
            FireflyImpactEffect.BurnEffectId, 25f, 1) == 1,
            "Burn status is active");
        FireflyBonfire bonfire = FireflyBonfire.Spawn(room, enemy.transform.position,
            10f, 0.1f, 1f, 2);
        HoneyBadgerPoisonCloud cloud = HoneyBadgerPoisonCloud.Spawn(
            room, enemy.transform.position, 15f, 0.1f, 2);
        Check(bonfire != null && cloud != null, "Both room hazards are active");

        Vector3 beforeMove = enemy.transform.position;
        int damageLabelsBefore = EnemyCombatTextUI.Ensure().ActiveDamageNumberCount;
        int turn = manager.TurnNumber + 1;
        ResolveTurn.Invoke(manager, null);
        Check(manager.TurnNumber == turn && statuses.LastProcessedTurn == turn
            && Mathf.Approximately(enemy.hp, 950f)
            && (enemy.transform.position - beforeMove).sqrMagnitude < 0.001f,
            "Burn, bonfire and poison deal 50 damage before movement starts");
        Check(bonfire.RemainingTurns == 1 && cloud.RemainingTurns == 1,
            "Each hazard consumes exactly one turn");
        Check(EnemyCombatTextUI.Instance.ActiveDamageNumberCount == damageLabelsBefore + 2,
            "Bonfire and burn share one fire number; poison gets one separate number");
        yield return WaitForTurn(manager);
        Check(Mathf.Approximately(enemy.hp, 950f)
            && enemy.transform.position.y < beforeMove.y,
            "Effects do not tick a second time after movement");

        enemy.hp = 5f;
        statuses.ApplyPeriodicDamage(FireflyImpactEffect.BurnEffectId, 10f, 1);
        int enemiesBefore = room.EnemyCollection.Count;
        ResolveTurn.Invoke(manager, null);
        Check(enemy != null && enemy.IsDefeated && !enemy.IsStepping
            && room.EnemyCollection.Count == enemiesBefore - 1,
            "Lethal burn removes the enemy before it can step or breach");
        yield return WaitForTurn(manager);
    }

    private static IEnumerator WaitForTurn(GameManager manager)
    {
        float limit = Time.realtimeSinceStartup + 5f;
        while (manager != null && manager.IsResolvingEnemyTurn
            && Time.realtimeSinceStartup < limit) yield return null;
        Check(manager != null && !manager.IsResolvingEnemyTurn,
            "Enemy turn finishes without a stuck movement animation");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Pre-move damage validation timed out");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            while (Steps.Count > 0)
            {
                IEnumerator current = Steps.Peek();
                if (!current.MoveNext()) { Steps.Pop(); continue; }
                if (current.Current is IEnumerator nested) { Steps.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Report.Add("PASS " + description);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        Time.timeScale = 1f;
        string result = error == null ? "PASS: pre-movement damage validation."
            : "FAIL: " + error;
        Report.Add(result);
        File.WriteAllLines("Logs/PreMoveDamageValidation.txt", Report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
