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
public static class EnemyDeathBonfireValidation
{
    private const string Running = "ForestAnimalWar.EnemyDeathBonfireValidation";
    private static IEnumerator checks;
    private static double deadline;
    private static int assertions;

    static EnemyDeathBonfireValidation()
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
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Use the isolated validation project.");
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

        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Enemies" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
        Require(prefabPaths.Length >= 40, "Full authored enemy catalogue is present");
        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponent<Brick>() == null) continue;
            var settings = new SerializedObject(prefab.GetComponent<Brick>());
            Require(settings.FindProperty("leaveFireflyBonfire").boolValue, path + " allows death bonfires");
            LevelGenerator room = CreateRoom();
            Brick enemy = Spawn(room, prefab);
            Vector3 center = enemy.transform.position;
            Vector2Int size = enemy.Footprint;
            var expected = new List<Vector3>();
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                if (enemy.UsesBossHealth || enemy.OccupiesFootprintOffset(new Vector2Int(x, y)))
                    expected.Add(center + new Vector3(x - (size.x - 1) * 0.5f,
                        (size.y - 1) * 0.5f - y, 0));
            Burn(enemy);
            enemy.RecordFireflyBonfireDamage(40f);
            enemy.GetComponent<EnemyIllumination>().Illuminate(2f);
            enemy.TakeDamage(100000f, CombatDamageSource.Rabbit);
            CheckFires(room, expected, 40f, 2f, path);
            // A second hit on the same defeated body cannot produce duplicate fires.
            enemy.TakeDamage(100000f, CombatDamageSource.Rabbit);
            Require(room.GetComponentsInChildren<FireflyBonfire>().Length == expected.Count,
                path + " does not duplicate bonfires after defeat");
            Object.DestroyImmediate(room.gameObject);
        }

        GameObject ordinary = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/SwampDemon.prefab");
        LevelGenerator fixture = CreateRoom();
        Brick direct = Spawn(fixture, ordinary);
        direct.TakeProjectileDamage(100000f, -1, CombatDamageSource.Firefly);
        CheckFires(fixture, new[] { direct.transform.position }, 10f, 1f, "Direct firefly kill without burn");
        Object.DestroyImmediate(fixture.gameObject);

        fixture = CreateRoom();
        Brick noFire = Spawn(fixture, ordinary);
        noFire.TakeDamage(100000f, CombatDamageSource.Rabbit);
        Require(fixture.GetComponentsInChildren<FireflyBonfire>().Length == 0,
            "An unburned enemy killed by another projectile leaves no bonfire");
        Object.DestroyImmediate(fixture.gameObject);

        fixture = CreateRoom();
        Brick finalTick = Spawn(fixture, ordinary);
        Burn(finalTick, 20f);
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(100);
        CheckFires(fixture, new[] { finalTick.transform.position }, 10f, 1f, "Final burn tick kill");
        Object.DestroyImmediate(fixture.gameObject);

        fixture = CreateRoom();
        Brick expired = Spawn(fixture, ordinary);
        expired.hp = 100f;
        Burn(expired, 1f);
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(101);
        Require(!expired.GetComponent<EnemyStatusEffects>().HasPeriodicDamage(FireflyImpactEffect.BurnEffectId),
            "Nonlethal burn expires before later damage");
        expired.TakeDamage(100000f, CombatDamageSource.Rabbit);
        Require(fixture.GetComponentsInChildren<FireflyBonfire>().Length == 0,
            "Expired burn does not create a bonfire on a later non-firefly kill");
        Object.DestroyImmediate(fixture.gameObject);

        fixture = CreateRoom();
        Brick stackTarget = Spawn(fixture, ordinary);
        stackTarget.hp = 1000f;
        var effectObject = new GameObject("Burn stack test");
        var impact = effectObject.AddComponent<FireflyImpactEffect>();
        impact.ConfigureBurnDamage(25f);
        for (int i = 0; i < 4; i++) impact.ApplyTo(stackTarget);
        EnemyStatusEffects stackStatuses = stackTarget.GetComponent<EnemyStatusEffects>();
        Require(stackStatuses.GetStackCount(FireflyImpactEffect.BurnEffectId) == 3
            && stackStatuses.GetRemainingTurns(FireflyImpactEffect.BurnEffectId) == 3,
            "Three capped burn stacks last three turns");
        float expectedHealth = 1000f;
        for (int stacks = 3; stacks >= 1; stacks--)
        {
            int turn = 203 - stacks;
            expectedHealth -= stacks * 25f;
            EnemyStatusEffects.ProcessAllAfterEnemyTurn(turn);
            Require(Mathf.Approximately(stackTarget.hp, expectedHealth)
                && stackStatuses.GetStackCount(FireflyImpactEffect.BurnEffectId) == stacks - 1
                && stackStatuses.GetRemainingTurns(FireflyImpactEffect.BurnEffectId) == stacks - 1
                && Mathf.Approximately(stackStatuses.GetPeriodicDamage(FireflyImpactEffect.BurnEffectId), (stacks - 1) * 25f)
                && stackTarget.GetComponent<EnemyBurnTint>().DisplayedStacks == stacks - 1,
                "Burn tick deals damage and removes exactly one stack: " + stacks + " -> " + (stacks - 1));
            EnemyStatusEffects.ProcessAllAfterEnemyTurn(turn);
            Require(Mathf.Approximately(stackTarget.hp, expectedHealth)
                && stackStatuses.GetStackCount(FireflyImpactEffect.BurnEffectId) == stacks - 1,
                "Repeated processing of one turn neither damages nor removes a second stack");
        }
        impact.ApplyTo(stackTarget);
        impact.ApplyTo(stackTarget);
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(203);
        impact.ApplyTo(stackTarget);
        Require(stackStatuses.GetStackCount(FireflyImpactEffect.BurnEffectId) == 2
            && Mathf.Approximately(stackStatuses.GetPeriodicDamage(FireflyImpactEffect.BurnEffectId), 50f),
            "A new hit replenishes a decayed burn stack and its damage");
        stackStatuses.ClearAll();
        impact.ConfigureBurnDamage(25f, true);
        for (int i = 0; i < 5; i++) impact.ApplyTo(stackTarget);
        float beforeEternal = stackTarget.hp;
        for (int turn = 204; turn <= 206; turn++) EnemyStatusEffects.ProcessAllAfterEnemyTurn(turn);
        Require(stackStatuses.GetStackCount(FireflyImpactEffect.BurnEffectId) == 5
            && stackStatuses.GetRemainingTurns(FireflyImpactEffect.BurnEffectId) == int.MaxValue
            && Mathf.Approximately(stackTarget.hp, beforeEternal - 375f),
            "Eternal flame retains all stacks and deals their damage each turn");
        stackStatuses.ClearAll();
        stackStatuses.ApplyPeriodicDamage("generic_status", 3f, 2);
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(207);
        Require(stackStatuses.GetRemainingTurns("generic_status") == 1,
            "Other periodic statuses retain their duration rule");
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(208);
        Require(!stackStatuses.HasPeriodicDamage("generic_status"), "Other periodic statuses expire normally");
        Object.DestroyImmediate(effectObject);
        Object.DestroyImmediate(fixture.gameObject);

        fixture = CreateRoom();
        Brick custom = Spawn(fixture, ordinary);
        Set(custom, "footprint", new Vector2Int(2, 2));
        Set(custom, "occupiedCellOffsets", new List<Vector2Int>
            { new Vector2Int(0, 0), new Vector2Int(1, 1) });
        Burn(custom);
        Vector3 customCenter = custom.transform.position;
        custom.TakeDamage(100000f);
        CheckFires(fixture, new[] { customCenter + new Vector3(-0.5f, 0.5f),
            customCenter + new Vector3(0.5f, -0.5f) }, 10f, 1f, "Custom ordinary footprint");
        Object.DestroyImmediate(fixture.gameObject);

        fixture = CreateRoom();
        Brick source = Spawn(fixture, ordinary);
        Burn(source);
        source.TakeDamage(100000f);
        Brick neighbor = Spawn(fixture, ordinary);
        neighbor.hp = 100f;
        FireflyBonfire fire = fixture.GetComponentInChildren<FireflyBonfire>();
        FireflyBonfire.ProcessAllAfterEnemyTurn(102);
        Require(Mathf.Approximately(neighbor.hp, 90f) && fire.RemainingTurns == 2,
            "Death bonfire still damages neighbors and consumes one turn");
        FireflyBonfire.ProcessAllAfterEnemyTurn(102);
        Require(Mathf.Approximately(neighbor.hp, 90f) && fire.RemainingTurns == 2,
            "Bonfire cannot tick twice during the same turn");

        // Check the actual renderer tint, including enemies covering a fire
        // with an outer boss cell rather than their center/pivot.
        float waitUntil = Time.time + 0.25f;
        while (Time.time < waitUntil) yield return null;
        Require(Mathf.Abs(FlameAlpha(fire) - 0.45f) < 0.01f,
            "A resident enemy makes existing and new flame particles translucent");
        neighbor.transform.position += Vector3.right;
        waitUntil = Time.time + 0.25f;
        while (Time.time < waitUntil) yield return null;
        Require(Mathf.Abs(FlameAlpha(fire) - 1f) < 0.01f,
            "An adjacent enemy does not cover the fire; opacity restores after leaving");
        Set(neighbor, "footprint", new Vector2Int(3, 3));
        Set(neighbor, "bossHealth", true);
        waitUntil = Time.time + 0.25f;
        while (Time.time < waitUntil) yield return null;
        Require(Mathf.Abs(FlameAlpha(fire) - 0.45f) < 0.01f,
            "A boss outer cell also makes fire translucent");
        Set(neighbor, "bossHealth", false);
        Set(neighbor, "occupiedCellOffsets", new List<Vector2Int> { new Vector2Int(1, 1) });
        waitUntil = Time.time + 0.25f;
        while (Time.time < waitUntil) yield return null;
        Require(Mathf.Abs(FlameAlpha(fire) - 1f) < 0.01f,
            "Empty offsets in a custom footprint do not obscure fire");
        Require(Mathf.Approximately(fire.DamagePerTurn, 10f) && fire.RemainingTurns == 2
            && Mathf.Approximately(fire.LightRadius, 1f),
            "Opacity animation preserves bonfire damage, lifetime and lighting");
        Object.DestroyImmediate(fixture.gameObject);
        yield return null;
        Require(FireflyBonfire.ActiveCount == 0, "All test bonfires unregister cleanly");
        File.WriteAllText("Logs/EnemyDeathBonfireValidation.txt", "PASSED: " + assertions
            + " checks across " + prefabPaths.Length + " enemy prefabs.\n"
            + "Burning deaths, direct firefly kills, final and expired burn ticks, ordinary/boss/miniboss cell positions, "
            + "damage/radius/duration inheritance, duplicate hits, custom footprints and bonfire turn damage.\n"
            + "Translucent fire under enemies and boss outer cells; opacity restores on leaving and ignores footprint holes.\n"
            + "Burn loses one stack per turn with 75/50/25 damage; duplicate turns, replenishment, eternal flame and other statuses checked.\n");
    }

    private static LevelGenerator CreateRoom()
    {
        var room = new GameObject("Bonfire room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        Set(room, "initialized", true);
        return room;
    }

    private static Brick Spawn(LevelGenerator room, GameObject prefab)
    {
        Brick enemy = Object.Instantiate(prefab, new Vector3(3, -3, 0), Quaternion.identity,
            room.transform).GetComponent<Brick>();
        enemy.levelGenerator = room;
        enemy.hp = 10f;
        room.EnemyCollection.Add(enemy.gameObject);
        return enemy;
    }

    private static void Burn(Brick enemy, float damage = 1f) => enemy.GetComponent<EnemyStatusEffects>()
        .ApplyPeriodicDamage(FireflyImpactEffect.BurnEffectId, damage, 1);

    private static void CheckFires(LevelGenerator room, IEnumerable<Vector3> positions,
        float damage, float radius, string label)
    {
        FireflyBonfire[] fires = room.GetComponentsInChildren<FireflyBonfire>();
        Vector3[] expected = positions.ToArray();
        Require(fires.Length == expected.Length, label + ": expected " + expected.Length + " fires, got " + fires.Length);
        foreach (Vector3 position in expected)
            Require(fires.Count(fire => Vector3.Distance(fire.transform.position, position) < 0.001f) == 1,
                label + ": exactly one fire at " + position);
        foreach (FireflyBonfire fire in fires)
            Require(fire.Room == room && fire.RemainingTurns == 3
                && Mathf.Approximately(fire.DamagePerTurn, damage)
                && Mathf.Approximately(fire.DamageRadius, radius)
                && Mathf.Approximately(fire.LightRadius, radius), label + ": inherited fire settings");
    }

    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static float FlameAlpha(FireflyBonfire fire)
    {
        var properties = new MaterialPropertyBlock();
        fire.GetComponentInChildren<ParticleSystemRenderer>().GetPropertyBlock(properties);
        return properties.GetColor("_Color").a;
    }
    private static void Require(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Bonfire validation timed out");
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
            File.WriteAllText("Logs/EnemyDeathBonfireValidation.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        else Debug.Log("ENEMY_DEATH_BONFIRE_PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
