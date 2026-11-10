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
public static class SquirrelEffectsValidation
{
    private const string Running = "ForestAnimalWar.SquirrelEffectsValidation";
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static Camera camera;
    private static LevelGenerator room;
    private static int lastFrame = -1;
    private static double deadline;
    static SquirrelEffectsValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Steps.Clear(); Steps.Push(Checks()); deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use the isolated validation project.");
        Directory.CreateDirectory("Logs"); SquirrelEffectsAuthoring.Bake();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    private static IEnumerator Checks()
    {
        var player = new GameObject("Player"); player.tag = "Player"; player.transform.position = Vector3.down * 8;
        Time.captureDeltaTime = 0.02f;
        camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 2.6f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.075f, 0.13f, 0.10f);
        room = new GameObject("Effects Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        var art = AssetDatabase.LoadAllAssetsAtPath(SquirrelEffectsAuthoring.ExplosionArt).OfType<Sprite>().ToArray();
        Require(art.Length == 16 && art.All(s => s.bounds.size.x <= 2.401f && s.bounds.size.y <= 2.401f),
            "Imagegen explosion has sixteen bounded authored frames");
        var firePrefab = Resources.Load<FireflyBonfire>("Effects/Firefly/Bonfire");
        Require(firePrefab != null && firePrefab.GetComponentInChildren<ParticleSystem>(true) != null,
            "Bonfire uses an editable authored particle prefab");
        var settings = firePrefab.GetComponentInChildren<ParticleSystem>(true).main;
        Require(settings.startLifetime.constantMin >= 0.9f && settings.startLifetime.constantMax >= 1.8f
            && firePrefab.FadeDuration == 3, "Flames live longer and have three seconds of fading embers");
        Brick target = Target(Vector2.zero, 10000), neighbour = Target(Vector2.right, 1000);
        ProjectileDamageStats.BeginLevel();
        var projectile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Squirrel.prefab"));
        projectile.GetComponent<Rigidbody2D>().simulated = false; projectile.GetComponent<Animal>().enabled = false;
        target.ApplyProjectileHit(projectile.GetComponent<Animal>(), Vector2.zero, Vector2.up);
        Object.Destroy(projectile); var acorn = SquirrelAcorn.FindOn(target);
        Require(acorn != null && acorn.BlastDuration == 0.45f, "Existing squirrel projectile uses the faster authored effect prefab");
        Require(Resources.Load<SquirrelAcorn>("Effects/Artifacts/Acorn").BlastDuration == 0.45f,
            "Universal artifact acorn uses the same faster authored explosion");
        target.TakeDamage(target.hp);
        var explosion = acorn.transform.Find("Acorn Fire Animation").GetComponent<SpriteRenderer>();
        var reaction = neighbour.GetComponent<EnemyHitReaction>();
        float previousHealth = neighbour.hp, explosionStarted = Time.time, previousHitAt = -1;
        int observedHits = 0; Sprite first = null;
        float limit = Time.time + 0.8f;
        while (observedHits < 3 && Time.time < limit)
        {
            yield return null;
            if (neighbour.hp == previousHealth) continue;
            float expected = observedHits == 2 ? 34 : 33;
            Require(previousHealth - neighbour.hp == expected, "Blast hit " + (observedHits + 1) + " removes " + expected + " HP");
            Require(reaction.IsReacting && (float)typeof(EnemyHitReaction).GetField("remaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reaction) >= 0.1f,
                "Blast hit " + (observedHits + 1) + " restarts the enemy's shake");
            Require(reaction.VisualRoot.localPosition.sqrMagnitude > 0.000001f,
                "Enemy artwork visibly moves on blast hit " + (observedHits + 1));
            Require(SquirrelAcorn.HasActiveBlasts && Player.HasActiveProjectiles, "Blast keeps the turn through hit " + (observedHits + 1));
            if (previousHitAt >= 0) Require(Time.time - previousHitAt >= 0.129f && Time.time - previousHitAt <= 0.18f,
                "Blast hits have a short visible separation");
            previousHitAt = Time.time; previousHealth = neighbour.hp;
            if (observedHits == 0) { first = explosion.sprite; Capture("Logs/SquirrelExplosionIgnition.png"); }
            if (observedHits == 1) { Require(explosion.sprite != first, "Faster explosion advances its authored frames"); Capture("Logs/SquirrelExplosionPeak.png"); }
            if (observedHits == 2) Capture("Logs/SquirrelExplosionEmbers.png");
            observedHits++;
        }
        Require(observedHits == 3 && Time.time - explosionStarted < 0.4f, "All three blast hits finish quickly");
        Require(explosion.enabled && explosion.sprite != null && explosion.bounds.size.x <= 2.401f,
            "Faster imagegen explosion stays within its local area");
        Require(neighbour.hp == 900 && ProjectileDamageStats.ForAnimal(7) == 200,
            "Three hits total one hundred damage credited to squirrel");
        yield return Seconds(Mathf.Max(0, 0.52f - (Time.time - explosionStarted)));
        Require(acorn == null && !SquirrelAcorn.HasActiveBlasts, "Faster animation cleans up and releases the turn within half a second");
        FireflyBonfire fire = FireflyBonfire.LastSpawned;
        Require(fire != null && fire.RemainingTurns == 1 && !fire.IsFading, "Weak bonfire remains active until its damaging turn");
        var flames = fire.GetComponentInChildren<ParticleSystem>();
        Require(flames.particleCount > 0, "Authored bonfire visibly emits long-lived flame figures");
        Capture("Logs/SquirrelBonfireBurning.png");
        FireflyBonfire.ProcessAllAfterEnemyTurn(1);
        Require(neighbour.hp == 890 && ProjectileDamageStats.ForAnimal(7) == 210,
            "Weak bonfire still deals ten damage for one turn credited to squirrel");
        Require(fire != null && fire.IsFading && fire.RemainingTurns == 0 && FireflyBonfire.ActiveCount == 0,
            "Expired bonfire leaves visible embers and is removed from damage processing");
        FireflyBonfire.ProcessAllAfterEnemyTurn(2);
        Require(neighbour.hp == 890, "Fading embers never deal an additional damage tick");
        yield return Seconds(0.8f);
        Require(fire != null && flames.particleCount > 0, "Flames continue visibly after the final damaging turn");
        Capture("Logs/SquirrelBonfireFading.png");
        Time.timeScale = 0; yield return Frames(5);
        Require(fire != null && fire.IsFading, "Paused game preserves the bonfire fade");
        Time.timeScale = 1; yield return Seconds(2.4f);
        Require(fire == null && FireflyBonfire.ActiveCount == 0, "Longer ember animation finishes and cleans up");
        ProjectileDamageStats.BeginLevel();
        var firefly = FireflyBonfire.Spawn(room, Vector3.right, 7, 1, 1, 1);
        FireflyBonfire.ProcessAllAfterEnemyTurn(3);
        Require(firefly != null && firefly.IsFading && ProjectileDamageStats.ForAnimal(0) == 7
            && ProjectileDamageStats.ForAnimal(7) == 0, "Shared firefly bonfire retains its damage owner and new visual fade");
        firefly.gameObject.SetActive(false); yield return Frames(2);
        Require(firefly == null && FireflyBonfire.ActiveCount == 0, "Leaving a room cleans up spent embers without abandoned coroutines");
    }
    private static Brick Target(Vector2 position, float hp)
    {
        var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/SwampDemon.prefab"),
            position, Quaternion.identity, room.transform).GetComponent<Brick>();
        enemy.levelGenerator = room; enemy.hp = hp; room.EnemyCollection.Add(enemy.gameObject); return enemy;
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator Seconds(float seconds) { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    private static void Capture(string path)
    {
        var render = new RenderTexture(640, 640, 24); render.Create(); camera.targetTexture = render; camera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(640, 640, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(image); render.Release(); Object.Destroy(render);
    }
    private static void Require(bool success, string description) { if (!success) throw new Exception(description); Report.Add("PASS " + description); }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Effect validation timed out");
            while (Steps.Count > 0)
            {
                var step = Steps.Peek(); if (!step.MoveNext()) { Steps.Pop(); continue; }
                if (step.Current is IEnumerator child) { Steps.Push(child); continue; } return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false); Time.timeScale = 1; Time.captureDeltaTime = 0;
        Report.Add(error == null ? "SQUIRREL_EFFECTS_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/SquirrelEffectsValidation.txt", Report);
        if (error != null) Debug.LogError(error); else Debug.Log("SQUIRREL_EFFECTS_PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
