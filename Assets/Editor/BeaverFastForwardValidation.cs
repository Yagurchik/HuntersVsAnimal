using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BeaverFastForwardValidation
{
    private const string Running = "ForestAnimalWar.BeaverFastForwardValidation";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Report = new List<string>();
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly MethodInfo Move = typeof(BeaverProjectile).GetMethod("FixedUpdate", Private);
    private static readonly MethodInfo UpdateSpeed = typeof(GameManager).GetMethod("UpdateProjectileFastForward", Private);
    private static int lastFrame = -1;
    private static double deadline;
    private static GameManager manager;
    private static Player player;
    private static Stage stage;
    private static LevelGenerator room;
    private static GameObject beaverPrefab;
    private static GameObject enemyPrefab;
    private static Vector2 origin;

    static BeaverFastForwardValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Steps.Clear(); Steps.Push(Checks()); deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run in the isolated validation project.");
        Directory.CreateDirectory("Logs"); Report.Clear();
        GameRunMode.SetDeveloperMode(false); GameRunMode.PrepareCampaign(); LevelProgression.PrepareLevel(2);
        AnimalProjectileQueue.PrepareRunLoadout(new[] { 0, 1, 2, 5, 6 }, 1);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    private static IEnumerator Checks()
    {
        yield return Frames(5);
        manager = GameManager.Instance; manager.enabled = false;
        player = Object.FindObjectsOfType<Player>().First(p => p.isActiveAndEnabled); player.enabled = false;
        stage = player.GetComponentInParent<Stage>(); room = stage.GetComponentInChildren<LevelGenerator>(true);
        origin = player.transform.position;
        manager.stepPlayer = manager.stepGame = manager.isPaused = false; Time.timeScale = 1;
        foreach (Collider2D collider in room.GetComponentsInChildren<Collider2D>())
            if (collider.GetComponentInParent<Brick>() != null) collider.enabled = false;
        beaverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Beaver.prefab");
        enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Brick.prefab");
        Check(Mathf.Approximately(manager.ProjectileFastForwardScale, 2f) && Mathf.Approximately(manager.ProjectileFastForwardDelay, 5f),
            "Uses the authored twofold fast-forward and existing five-second fallback");

        Brick target = Target(origin + Vector2.up * 1.8f);
        BeaverProjectile beaver = Launch(origin + Vector2.up * 0.5f, Vector2.up);
        StepUntil(beaver, () => beaver.DamageDealt > 0);
        Check(!beaver.HasPassedFirstEnemy && !manager.IsProjectileFastForwardActive && Time.timeScale == 1,
            "Entering and biting the first enemy does not accelerate the game");
        StepUntil(beaver, () => beaver.HasPassedFirstEnemy);
        Check(!manager.IsProjectileFastForwardActive && Time.timeScale == 1, "Leaving the first enemy waits for the following wall");
        Animal companion = Object.Instantiate(AnimalProjectileQueue.Instance.Animals[0].Prefab,
            player.transform.position + Vector3.up, Quaternion.identity).GetComponent<Animal>();
        companion.SetReturnTarget(player.gameObject); companion.GetComponent<Rigidbody2D>().simulated = false;
        StepUntil(beaver, () => Heading(beaver).y < 0);
        Check(manager.IsProjectileFastForwardActive && Time.timeScale == 2 && manager.ProjectileFlightElapsed < 5,
            "The first actual upper-wall bounce after passage immediately accelerates without waiting five seconds");
        beaver.GetComponent<Animal>().DropDown(); Object.Destroy(beaver.gameObject); yield return Frames(2);
        Check(manager.IsProjectileFastForwardActive && Time.timeScale == 2 && companion != null,
            "The accelerated shared volley continues while another projectile is still flying");
        companion.DropDown(); Object.Destroy(companion.gameObject); yield return Frames(2);
        Check(!manager.IsProjectileFastForwardActive && Time.timeScale == 1, "The last projectile restores normal speed through its usual cleanup");
        yield return Clear();

        beaver = Launch(origin + Vector2.up * 0.5f, new Vector2(0.8f, 0.6f));
        StepUntil(beaver, () => Heading(beaver).x < 0);
        Check(!beaver.HasPassedFirstEnemy && !manager.IsProjectileFastForwardActive, "A side-wall bounce before any enemy does not request early acceleration");
        target = Target((Vector2)beaver.transform.position + Heading(beaver) * 1.2f);
        StepUntil(beaver, () => beaver.HasPassedFirstEnemy);
        Check(!manager.IsProjectileFastForwardActive, "An earlier wall is not reused when the first enemy is subsequently passed");
        StepUntil(beaver, () => manager.IsProjectileFastForwardActive);
        Check(Heading(beaver).x > 0 && Time.timeScale == 2, "The next actual side-wall bounce after passage triggers fast-forward");
        yield return Clear();

        target = Target(origin + Vector2.up * 1.8f, 1f);
        beaver = Launch(origin + Vector2.up * 0.5f, Vector2.up);
        StepUntil(beaver, () => beaver.HasPassedFirstEnemy);
        Check(target == null || target.IsDefeated, "Killing the first enemy also completes passage");
        Check(!manager.IsProjectileFastForwardActive, "A lethal bite still waits for the wall");
        StepUntil(beaver, () => Heading(beaver).y < 0);
        Check(manager.IsProjectileFastForwardActive && Time.timeScale == 2, "A wall after a lethal first bite accelerates normally");
        yield return Clear();

        float top = stage.transform.position.y + stage.BattlefieldSize.y * 0.5f - 0.24f;
        target = Target(new Vector2(origin.x, top - 0.26f));
        beaver = Launch(new Vector2(origin.x, top - 1f), Vector2.up, 50f, true);
        Move.Invoke(beaver, null);
        Check(beaver.transform.position.y < top - 0.1f && Heading(beaver).y > 0 && !manager.IsProjectileFastForwardActive,
            "A sweep that stops in an enemy before a predicted wall neither reflects nor accelerates prematurely");
        StepUntil(beaver, () => Heading(beaver).y < 0);
        Check(!beaver.HasPassedFirstEnemy && !manager.IsProjectileFastForwardActive,
            "An actual wall reached while still inside the first enemy does not accelerate early");
        StepUntil(beaver, () => beaver.HasPassedFirstEnemy);
        StepUntil(beaver, () => Heading(beaver).y > 0);
        Check(!manager.IsProjectileFastForwardActive, "The ultimate pumpkin-field bounce is distinct from a wall");
        StepUntil(beaver, () => manager.IsProjectileFastForwardActive);
        Check(Heading(beaver).y < 0 && Time.timeScale == 2, "The following upper wall accelerates after the first passage");
        yield return Clear();

        beaver = Launch(origin + Vector2.up * 0.5f, Vector2.up);
        StepUntil(beaver, () => Heading(beaver).y < 0);
        Check(!manager.IsProjectileFastForwardActive, "An empty upper-wall bounce does not request early acceleration");
        typeof(GameManager).GetField("projectileFlightWasActive", Private).SetValue(manager, true);
        typeof(GameManager).GetField("projectileFlightElapsed", Private).SetValue(manager, manager.ProjectileFastForwardDelay);
        UpdateSpeed.Invoke(manager, null);
        Check(manager.IsProjectileFastForwardActive && Time.timeScale == 2, "The existing elapsed-time fallback still accelerates other long flights");
        yield return Clear();

        beaver = Launch(origin + Vector2.up * 0.5f, Vector2.up);
        LevelTransition panels = Object.FindObjectOfType<LevelTransition>();
        panels.PauseButtonPressed(); manager.RequestProjectileFastForward();
        Check(manager.isPaused && Time.timeScale == 0 && !manager.IsProjectileFastForwardActive,
            "An early request cannot unpause the actual pause window");
        panels.ContinueButtonPressed(); UpdateSpeed.Invoke(manager, null);
        Check(!manager.isPaused && manager.IsProjectileFastForwardActive && Time.timeScale == 2,
            "A pending request resumes only after the pause window is closed");
        yield return Clear();

        Time.timeScale = 0.75f;
        beaver = Launch(origin + Vector2.up * 0.5f, Vector2.up);
        manager.RequestProjectileFastForward(); manager.RequestProjectileFastForward();
        Check(manager.IsProjectileFastForwardActive && Time.timeScale == 2, "Repeated requests keep one shared acceleration state");
        beaver.GetComponent<Animal>().DropDown(); Object.Destroy(beaver.gameObject); yield return Frames(2);
        Check(!manager.IsProjectileFastForwardActive && Mathf.Approximately(Time.timeScale, 0.75f),
            "Repeated requests retain and restore the original game speed rather than saving twofold speed");
        Time.timeScale = 1;
        beaver = Launch(origin + Vector2.up * 0.5f, Vector2.up); UpdateSpeed.Invoke(manager, null);
        Check(!manager.IsProjectileFastForwardActive && Time.timeScale == 1, "A later volley does not inherit the earlier fast-forward request");
        yield return Clear();
        manager.RequestProjectileFastForward();
        Check(!manager.IsProjectileFastForwardActive && Time.timeScale == 1, "A request without flying projectiles has no effect");
    }
    private static BeaverProjectile Launch(Vector2 position, Vector2 direction, float speed = 20f, bool ultimate = false)
    {
        var beaver = Object.Instantiate(beaverPrefab, position, Quaternion.identity).GetComponent<BeaverProjectile>();
        beaver.enabled = false; // Exercise deterministic native FixedUpdate steps.
        var animal = beaver.GetComponent<Animal>(); animal.SetReturnTarget(player.gameObject); animal.SetCombatSource(CombatDamageSource.Beaver);
        beaver.ConfigureRunUpgrades(0, 0, ultimate); beaver.Launch(direction, speed);
        Physics2D.SyncTransforms(); return beaver;
    }
    private static Brick Target(Vector2 position, float hp = 100000f)
    {
        var enemy = Object.Instantiate(enemyPrefab, new Vector3(position.x, position.y, room.transform.position.z), Quaternion.identity, room.transform).GetComponent<Brick>();
        enemy.name = "Beaver Speed Target"; enemy.enabled = false; enemy.levelGenerator = room; enemy.hp = hp;
        typeof(Brick).GetField("maxHp", Private).SetValue(enemy, hp);
        typeof(Brick).GetField("experienceReward", Private).SetValue(enemy, 0);
        room.EnemyCollection.Add(enemy.gameObject); Physics2D.SyncTransforms(); return enemy;
    }
    private static Vector2 Heading(BeaverProjectile beaver) => (Vector2)typeof(BeaverProjectile).GetField("heading", Private).GetValue(beaver);
    private static void StepUntil(BeaverProjectile beaver, Func<bool> condition)
    {
        for (int step = 0; step < 1000 && beaver != null && beaver.IsFlying; step++)
        {
            if (condition()) return;
            Move.Invoke(beaver, null);
        }
        if (!condition()) throw new Exception("The beaver never reached the expected passage or wall state.");
    }
    private static IEnumerator Clear()
    {
        foreach (Animal animal in Player.animalsCollect.ToArray())
            if (animal != null) { animal.DropDown(); Object.Destroy(animal.gameObject); }
        foreach (Brick enemy in room.GetComponentsInChildren<Brick>())
            if (enemy.name == "Beaver Speed Target") Object.Destroy(enemy.gameObject);
        yield return Frames(2);
        Check(!manager.IsProjectileFastForwardActive && !Player.HasProjectileBodies && Time.timeScale == 1,
            "Cleanup resets the shared acceleration and restores normal speed");
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description); Report.Add("PASS " + description);
    }
    private static void Tick()
    {
        if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
        try {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Beaver fast-forward validation timed out");
            while (Steps.Count > 0) { var step = Steps.Peek(); if (!step.MoveNext()) { Steps.Pop(); continue; }
                if (step.Current is IEnumerator child) { Steps.Push(child); continue; } return; }
            Finish(null);
        } catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false); Time.timeScale = 1;
        Report.Add(error == null ? "BEAVER_FAST_FORWARD_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/BeaverFastForwardValidation.txt", Report);
        if (error != null) Debug.LogException(error); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
