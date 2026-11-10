using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DamDefenseVisibilityValidation
{
    private const string Running = "ForestAnimalWar.DamDefenseVisibilityValidation";
    private static IEnumerator checks;
    private static double deadline;

    static DamDefenseVisibilityValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            checks = Checks();
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        new GameObject("Player").tag = "Player";
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(3, -3, -10);
        camera.orthographic = true;
        camera.orthographicSize = 5;

        // An inactive manager supplies state without starting a full campaign.
        var managerObject = new GameObject("Manager fixture");
        managerObject.SetActive(false);
        var manager = managerObject.AddComponent<GameManager>();
        var front = new GameObject("Front", typeof(DefenseLineRuntime)).GetComponent<DefenseLineRuntime>();
        Set(front, "gameManager", manager);
        var room = new GameObject("Outer room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        room.ConfigureRoomIdentity(2, 2);
        Set(room, "initialized", true);
        room.transform.SetParent(front.transform, false);
        Stage stage = room.GetComponent<Stage>();
        var barricade = room.gameObject.AddComponent<MainRoomBarricade>();
        barricade.Initialize(stage, 400f);
        Set(front, "barricade", barricade);
        var dam = new GameObject("Dam", typeof(BeaverDam)).GetComponent<BeaverDam>();
        dam.AddHealth(0, stage);
        Require(Mathf.Approximately(dam.MaximumHealth, 100f) && dam.Health == 0f,
            "An empty dam has a fixed capacity of 25 percent of barricade maximum HP");
        dam.AddHealth(1000, stage);
        Require(Mathf.Approximately(dam.Health, 100f) && Mathf.Approximately(dam.DefenseMaxHealth, 100f),
            "Reinforcement cannot exceed the dam capacity, including its health bar maximum");
        dam.Absorb(20f);
        Require(Mathf.Approximately(dam.Health, 80f) && Mathf.Approximately(dam.MaximumHealth, 100f),
            "Dam damage reduces current health without changing its capacity");
        barricade.TakeDamage(100f);
        dam.AddHealth(1000, stage);
        Require(Mathf.Approximately(dam.Health, 100f) && Mathf.Approximately(dam.MaximumHealth, 100f),
            "Dam repairs stop at the same maximum even when the barricade is damaged");
        barricade.AddBeaverHealth(10000f);
        dam.AddHealth(1000, stage);
        Require(barricade.CurrentHealth > 10000f && Mathf.Approximately(barricade.MaxHealth, 400f)
            && Mathf.Approximately(dam.Health, 100f),
            "Barricade reinforcement stays unlimited and does not raise dam capacity");
        // The breach visibility scenarios below need a durable dam.
        barricade.Initialize(stage, 4000f);
        dam.AddHealth(1000, stage);
        Require(Mathf.Approximately(dam.Health, 1000f) && Mathf.Approximately(dam.MaximumHealth, 1000f),
            "Dam capacity follows a different barricade base maximum");

        Brick[] enemies = new[] { "SwampDemon", "BogPustulant", "SwampDemon" }.Select((name, i) =>
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/" + name + ".prefab");
            Require(prefab != null, "Missing enemy prefab: " + name);
            var enemy = Object.Instantiate(prefab, new Vector3(i * 2, i == 0 ? -6 : -2, 0),
                Quaternion.identity, room.transform).GetComponent<Brick>();
            enemy.levelGenerator = room;
            enemy.dmg = 10;
            room.EnemyCollection.Add(enemy.gameObject);
            foreach (EnemyVisualHover hover in enemy.GetComponentsInChildren<EnemyVisualHover>()) hover.enabled = false;
            return enemy;
        }).ToArray();
        yield return null;
        // Preserve authored transparency as well as fully opaque sprites.
        var tinted = enemies[2].GetComponentInChildren<SpriteRenderer>();
        Color tint = tinted.color; tint.a = 0.65f; tinted.color = tint;
        SpriteRenderer[] sprites = enemies.SelectMany(e => e.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
        float[] alphas = sprites.Select(s => s.color.a).ToArray();
        Transform[] visuals = enemies.Select(e => e.transform.Find("Visual")).ToArray();
        Vector3[] scales = visuals.Select(v => v.localScale).ToArray();
        Vector3[] positions = enemies.Select(e => e.transform.position).ToArray();

        for (int cycle = 0; cycle < 3; cycle++)
        {
            room.RequestBreach(enemies[0]);
            room.AnimateRequestedBreach();
            // Also cover resolution while the retreat coroutine is still running.
            float end = Time.time + (cycle == 2 ? 0.08f : 0.7f);
            while (Time.time < end) yield return null;
            if (cycle != 2)
                Require(sprites.All(s => s.color.a < 0.001f), "Breach did not hide all resident enemies");
            front.BreachRoom(room, new[] { enemies[0] });
            end = Time.unscaledTime + 0.5f;
            while (Time.unscaledTime < end) yield return null;

            for (int i = 0; i < sprites.Length; i++)
                Require(Mathf.Abs(sprites[i].color.a - alphas[i]) < 0.001f,
                    "Enemy remained invisible after dam held: " + sprites[i].transform.root.name);
            for (int i = 0; i < visuals.Length; i++)
                Require(Vector3.Distance(visuals[i].localScale, scales[i]) < 0.001f,
                    "Enemy retained the retreat scale after dam held");
            for (int i = 1; i < enemies.Length; i++)
                Require(enemies[i].transform.position == positions[i], "Non-attacker was moved");
            Require(enemies[0].transform.position.y >= room.transform.position.y - 2.01f,
                "Attacker did not return to the top three rows");
            Require(Mathf.Abs(dam.Health - (1000 - (cycle + 1) * 10)) < 0.001f,
                "Dam damage changed");
            Require(!room.IsLost && room.EnemyCollection.Count == 3, "Held breach lost the room or enemies");
        }
        var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/BoneDragonBoss.prefab");
        Require(bossPrefab != null, "Bone dragon prefab is available");
        var boss = Object.Instantiate(bossPrefab, room.transform).GetComponent<Brick>();
        boss.levelGenerator = room;
        room.EnemyCollection.Add(boss.gameObject);
        boss.hp = 800f;
        Require(boss.UsesBossHealth && Mathf.Approximately(boss.BreachDamage, 8f)
            && Mathf.Approximately(boss.ProjectileContactDamage, 1f)
            && Mathf.Approximately(enemies[0].ProjectileContactDamage, 10f),
            "Boss breach damage is one percent of HP while projectile damage stays authored");
        float beforeBoss = dam.Health;
        front.BreachRoom(room, new[] { boss });
        Require(Mathf.Abs(dam.Health - (beforeBoss - 8f)) < 0.001f,
            "Boss deals one percent of its current HP to the dam");
        boss.hp = 50f;
        beforeBoss = dam.Health;
        front.BreachRoom(room, new[] { boss });
        Require(Mathf.Abs(dam.Health - (beforeBoss - 0.5f)) < 0.001f,
            "Boss damage to the dam has no one-point floor");
        Require(Mathf.Approximately(boss.StrikeBarricadeAndReturn(boss.transform.position),
                DefenseLineRuntime.BarricadeDamageOf(boss))
            && Mathf.Approximately(DefenseLineRuntime.BarricadeDamageOf(boss), DefenseLineRuntime.DefaultBossBarricadeDamage),
            "Boss deals fixed barricade damage regardless of its current HP");
        Require(Mathf.Approximately(boss.ProjectileContactDamage, 1f),
            "Boss projectile contact damage does not change with its health");
        boss.hp = 800f;
        boss.transform.position = new Vector3(1000f, 1000f, 0f);
        var probeObject = new GameObject("Boss contact probe", typeof(Rigidbody2D),
            typeof(CircleCollider2D), typeof(Animal));
        probeObject.tag = "Animal";
        probeObject.layer = LayerMask.NameToLayer("bullet");
        probeObject.transform.position = boss.transform.position;
        probeObject.GetComponent<Rigidbody2D>().gravityScale = 0f;
        Animal probe = probeObject.GetComponent<Animal>();
        probe.SetRuntimeHealth(100f);
        probe.SetRuntimeDamage(0f);
        float collisionDeadline = Time.time + 0.2f;
        while (Time.time < collisionDeadline) yield return null;
        Require(probe != null && Mathf.Approximately(probe.Health, 99f),
            "A real boss collision removes one projectile HP rather than one percent of boss HP; remaining: "
                + (probe != null ? probe.Health.ToString() : "destroyed"));
        Object.DestroyImmediate(probeObject);
        File.WriteAllText("Logs/DamDefenseVisibilityValidation.txt",
            "PASSED: fixed dam capacity at 25% of barricade maximum, capped reinforcement/repairs, unlimited barricade overhealth.\n"
            + "Three held breaches, current-HP defense damage and authored projectile contact damage.\n"
            + "All resident sprites and visual scales restored, including authored transparency.\n"
            + "Completed and interrupted retreat, repeated defense, attacker return, unchanged non-attacker positions and dam damage.\n");
    }

    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Dam defense test timed out");
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
            File.WriteAllText("Logs/DamDefenseVisibilityValidation.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        else Debug.Log("DAM_DEFENSE_VISIBILITY_PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
