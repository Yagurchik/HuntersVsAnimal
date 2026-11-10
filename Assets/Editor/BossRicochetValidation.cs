using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BossRicochetValidation
{
    private const string Running = "ForestAnimalWar.BossRicochetValidation";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Report = new List<string>();
    private static IEnumerator checks;
    private static double deadline;
    private static SimulationMode2D previousSimulation;

    static BossRicochetValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            checks = Checks(); deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run in the isolated validation project.");
        Directory.CreateDirectory("Logs"); Report.Clear();
        BossOctagonValidation.RunBatch();
        Report.Add("PASS All eight authored octagonal bosses and the Mirage square pass geometry checks");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        previousSimulation = Physics2D.simulationMode; Physics2D.simulationMode = SimulationMode2D.Script;
        var player = new GameObject("Player"); player.tag = "Player"; player.transform.position = Vector3.down * 20;
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = Vector3.back * 10; camera.orthographic = true;
        var room = new GameObject("Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        var boss = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/LichBoss.prefab"));
        var brick = boss.GetComponent<Brick>(); brick.levelGenerator = room; brick.hp = 100000; brick.enabled = false;
        var polygon = boss.GetComponent<PolygonCollider2D>();
        var aim = new GameObject("Aim", typeof(BallSpawner)).GetComponent<BallSpawner>(); aim.transform.SetParent(room.transform);
        Vector2 half = polygon.bounds.extents;
        float cut = BossOctagonGeometry.CornerCut * 0.5f;
        // Each native bevel can produce a real upward or downward ricochet.
        Vector2[] corners = {
            new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1),
            new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1),
            new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1)
        };
        Vector2[] incoming = {
            new Vector2(-0.6f, 0.8f), new Vector2(0.6f, 0.8f), new Vector2(0.8f, 0.6f), new Vector2(-0.8f, 0.6f),
            new Vector2(0.6f, 0.8f), new Vector2(-0.6f, 0.8f), new Vector2(-0.6f, -0.8f), new Vector2(0.6f, -0.8f),
            Vector2.up, Vector2.up, Vector2.down, Vector2.down
        };
        float radians = 5f * Mathf.Deg2Rad;
        Vector2[] expected = {
            new Vector2(-0.8f, 0.6f), new Vector2(0.8f, 0.6f), new Vector2(0.6f, 0.8f), new Vector2(-0.6f, 0.8f),
            new Vector2(-0.8f, -0.6f), new Vector2(0.8f, -0.6f), new Vector2(-0.8f, -0.6f), new Vector2(0.8f, -0.6f),
            new Vector2(-Mathf.Cos(radians), Mathf.Sin(radians)), new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)),
            new Vector2(-Mathf.Cos(radians), -Mathf.Sin(radians)), new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians))
        };
        var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/BaseBullet.prefab");
        float radius = ProjectilePhysics2D.GetCollisionRadius(bulletPrefab);
        Require(radius > 0f, "Tests use the authored ordinary projectile radius");
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 point = Vector2.Scale(corners[i], half - Vector2.one * cut);
            Vector2 origin = point + corners[i].normalized * radius - incoming[i] * 0.35f;
            CheckShot(aim, bulletPrefab, polygon, origin, incoming[i], expected[i], "bevel case " + (i + 1));
            yield return null;
        }
        // Flat boss faces still reflect normally, and Mirage retains its intended return shot.
        CheckShot(aim, bulletPrefab, polygon, new Vector2(0, -half.y - radius - 0.35f),
            Vector2.up, Vector2.down, "flat lower boss face");
        Object.Destroy(boss); yield return null;
        var mirage = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/MirageBoss.prefab"));
        var mirrorBrick = mirage.GetComponent<Brick>(); mirrorBrick.enabled = false; mirrorBrick.levelGenerator = room;
        var square = mirage.GetComponent<BoxCollider2D>(); Physics2D.SyncTransforms();
        Vector2 angled = new Vector2(0.6f, 0.8f);
        CheckShot(aim, bulletPrefab, square, (Vector2)square.bounds.center + Vector2.down * (square.bounds.extents.y + radius)
            - angled * 0.35f, angled, -angled, "Mirage return reflection");
        Object.Destroy(mirage); Physics2D.simulationMode = previousSimulation;
    }

    private static void CheckShot(BallSpawner aim, GameObject bulletPrefab, Collider2D bossCollider,
        Vector2 origin, Vector2 incoming, Vector2 expected, string label)
    {
        aim.transform.position = origin;
        Physics2D.SyncTransforms();
        object path = typeof(BallSpawner).GetMethod("CalculateAimPath", Private)
            .Invoke(aim, new object[] { origin, incoming, ProjectilePhysics2D.GetCollisionRadius(bulletPrefab), false, true, false });
        Vector2 hit = (Vector2)path.GetType().GetField("HitPoint").GetValue(path);
        Vector2 end = (Vector2)path.GetType().GetField("ReflectionEnd").GetValue(path);
        Vector2 preview = (end - hit).normalized;
        Require((bool)path.GetType().GetField("HasImpact").GetValue(path)
            && Vector2.Dot(preview, expected) > 0.999f, "Aim preview predicts the correct direction: " + label + ", " + preview);
        var bullet = Object.Instantiate(bulletPrefab, origin, Quaternion.identity);
        var animal = bullet.GetComponent<Animal>(); animal.SetRuntimeHealth(10000); animal.SetRuntimeDamage(0);
        var physics = bullet.GetComponent<ProjectilePhysics2D>(); var body = bullet.GetComponent<Rigidbody2D>();
        physics.Launch(incoming, 3f);
        bool bounced = false;
        for (int step = 0; step < 100; step++)
        {
            physics.SendMessage("FixedUpdate"); Physics2D.Simulate(0.005f);
            if (Vector2.Dot(physics.FlightDirection, incoming) < 0.99f) { bounced = true; break; }
        }
        Require(bounced && Vector2.Dot(physics.FlightDirection, expected) > 0.995f,
            "Actual projectile reflects in the correct direction: " + label + ", " + physics.FlightDirection);
        for (int step = 0; step < 3; step++) { physics.SendMessage("FixedUpdate"); Physics2D.Simulate(0.005f); }
        Require(Vector2.Dot(body.velocity.normalized, preview) > 0.995f && Mathf.Abs(body.velocity.magnitude - 3f) < 0.001f,
            "Ricochet stays stable at launch speed and matches the preview: " + label);
        Object.Destroy(bullet);
    }

    private static void Require(bool success, string message) { if (!success) throw new Exception(message); Report.Add("PASS " + message); }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Boss ricochet validation timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false); Physics2D.simulationMode = previousSimulation;
        Report.Add(error == null ? "BOSS_RICOCHETS_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/BossRicochetValidation.txt", Report);
        if (error != null) Debug.LogException(error); else Debug.Log("BOSS_RICOCHETS_PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
