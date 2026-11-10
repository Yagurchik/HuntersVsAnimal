using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class UniversalBossValidation
{
    private const string Running = "UniversalBossValidation.Running";
    private static IEnumerator checks;
    private static readonly List<string> report = new List<string>();
    private static double deadline;

    static UniversalBossValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            checks = CheckGameplay();
            deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run only in an isolated validation project.");
        Directory.CreateDirectory("Logs");
        report.Clear();
        GameRunMode.SetDeveloperMode(false);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(2);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckGameplay()
    {
        for (int i = 0; i < 4; i++) yield return null;
        GameManager manager = GameManager.Instance;
        Check(manager != null && manager.Front != null, "Campaign scene opened");
        manager.enabled = false;
        manager.stepPlayer = manager.stepGame = manager.isPaused = false;
        foreach (Player player in UnityEngine.Object.FindObjectsOfType<Player>(true))
            player.enabled = false;
        foreach (FirstLevelTutorial tutorial in UnityEngine.Object.FindObjectsOfType<FirstLevelTutorial>(true))
            tutorial.enabled = false;
        for (int line = 2; line <= 4; line++) manager.Front.ActivateLine(line);
        for (int i = 0; i < 2; i++) yield return null;

        LevelGenerator room = manager.Front.GetGenerator(DefenseFront.IdFor(4, 4));
        Brick bell = room.EnemyCollection.Where(value => value != null)
            .Select(value => value.GetComponent<Brick>())
            .Single(value => value != null && value.UsesBossHealth);
        UniversalBossAbility ability = bell.GetComponent<UniversalBossAbility>();
        Vector3 bellPosition = bell.transform.position;
        Check(bell.name.StartsWith("BellRingerBoss") && ability != null
            && ability.Kind == UniversalBossAbility.AbilityKind.BellRinger,
            "Bell Ringer appears as the level-two outer boss");
        Check(room.TrySummonNear(bell, room.brickPrefab, out Brick first),
            "First ordinary target can enter the bell room");
        Check(room.TrySummonNear(bell, room.brickPrefab, out Brick second),
            "Second ordinary target can enter the bell room");
        Check(!ability.BellPulsePending, "No hit means no pending extra step");

        SetLastVolley(41);
        GameObject shot = new GameObject("Universal boss test projectile");
        Animal projectile = shot.AddComponent<Animal>();
        projectile.enabled = false;
        Player.animalsCollect.Remove(projectile);
        projectile.SetComboVolley(41);
        projectile.SetCombatSource(CombatDamageSource.Other);
        projectile.dmg = 100f;
        float firstY = first.transform.position.y;
        float secondY = second.transform.position.y;
        int turn = manager.TurnNumber;
        bell.ApplyProjectileHit(projectile, bell.transform.position, Vector2.up);
        Check(ability.BellPulsePending && !bell.IsDefeated,
            "A surviving direct hit marks its volley");
        Check(Mathf.Approximately(first.transform.position.y, firstY)
            && Mathf.Approximately(second.transform.position.y, secondY),
            "Ordinary enemies do not step before the volley resolves");

        manager.TryCompletePlayerTurn();
        Check(manager.IsResolvingEnemyTurn,
            "Completing the hit volley starts the immediate bell pulse");
        // Keep the normal turn closed while measuring this separate bonus step.
        manager.stepGame = true;
        double pulseDeadline = EditorApplication.timeSinceStartup + 8d;
        while (manager.IsResolvingEnemyTurn && EditorApplication.timeSinceStartup < pulseDeadline)
            yield return null;
        report.Add("STATE bell pulse: turn=" + manager.TurnNumber
            + " firstY=" + first.transform.position.y + " secondY=" + second.transform.position.y);
        Check(!manager.IsResolvingEnemyTurn && !ability.BellPulsePending
            && manager.TurnNumber == turn,
            "Bell pulse finishes without consuming a normal enemy turn");
        Check(Mathf.Approximately(first.transform.position.y, firstY - 1f)
            && Mathf.Approximately(second.transform.position.y, secondY - 1f),
            "Every ordinary ally moves one additional cell after the volley");

        // A later hit followed by death cannot schedule another pulse.
        manager.stepGame = false;
        manager.isPaused = true;
        SetLastVolley(42);
        projectile.SetComboVolley(42);
        ability.ModifyDamage(bell, projectile, bell.transform.position, 100f);
        Check(ability.BellPulsePending, "Second volley marks the surviving bell");
        bell.TakeDamage(bell.hp + 1f);
        Check(bell.IsDefeated, "Bell can die during the marked volley");
        manager.isPaused = false;
        float afterDeathY = first.transform.position.y;
        manager.TryCompletePlayerTurn();
        for (int i = 0; i < 5; i++) yield return null;
        Check(!manager.IsResolvingEnemyTurn
            && Mathf.Approximately(first.transform.position.y, afterDeathY),
            "A dead Bell Ringer never grants the extra step");

        GameObject knotObject = UnityEngine.Object.Instantiate(
            Resources.Load<GameObject>("Enemies/KnotBinderBoss"), bellPosition,
            Quaternion.identity, room.transform);
        Brick knotBrick = knotObject.GetComponent<Brick>();
        knotBrick.levelGenerator = room;
        room.EnemyCollection.Add(knotObject);
        room.InvalidateEnemyQueries();
        UniversalBossAbility knot = knotObject.GetComponent<UniversalBossAbility>();
        float firstHealth = first.hp;
        float secondHealth = second.hp;
        float keptDamage = knot.ModifyDamage(knotBrick, projectile, bellPosition, 100f);
        Check(Mathf.Approximately(keptDamage, 60f)
            && first.hp < firstHealth && second.hp < secondHealth,
            "Knot Binder redirects forty percent of a direct hit to two allies");
        yield return null;
        Check(knotObject.GetComponentsInChildren<LineRenderer>(true).Length == 2,
            "Knot Binder has two authored damage links");
        room.EnemyCollection.Remove(knotObject);
        UnityEngine.Object.Destroy(knotObject);
        room.InvalidateEnemyQueries();

        GameObject impostorObject = UnityEngine.Object.Instantiate(
            Resources.Load<GameObject>("Enemies/ImpostorBoss"), bellPosition,
            Quaternion.identity, room.transform);
        Brick impostorBrick = impostorObject.GetComponent<Brick>();
        impostorBrick.levelGenerator = room;
        impostorBrick.hp = impostorBrick.MaxHp * 0.49f;
        room.EnemyCollection.Add(impostorObject);
        room.InvalidateEnemyQueries();
        UniversalBossAbility impostor = impostorObject.GetComponent<UniversalBossAbility>();
        impostor.ModifyDamage(impostorBrick, projectile, bellPosition, 100f);
        for (int i = 0; i < 3; i++) yield return null;
        Brick[] decoys = room.EnemyCollection.Where(value => value != null
            && value.name.StartsWith("ImpostorDecoy"))
            .Select(value => value.GetComponent<Brick>()).ToArray();
        Check(impostor.SplitDone && decoys.Length == 2
            && decoys.All(value => value.Footprint == Vector2Int.one
                && value.ExperienceReward == 0 && Mathf.Approximately(value.MaxHp, 1f)),
            "Impostor creates two fragile no-reward decoy heads below half HP");
        Check(impostorObject.transform.Find("Visual/True Eye") != null
            && decoys.All(value => value.transform.Find("Visual/True Eye") == null),
            "Only the real Impostor has the lit eye marker");

        foreach (string name in new[] { "KnotBinderBoss", "VolleyEaterBoss",
            "ThreeSealIdolBoss", "ImpostorBoss" })
        {
            GameObject prefab = Resources.Load<GameObject>("Enemies/" + name);
            Check(prefab != null && prefab.GetComponent<UniversalBossAbility>() != null
                && prefab.GetComponent<EnemyHitReaction>()?.DamageSprite != null,
                name + " has an editable prefab, ability and damage art");
        }
        GameObject volleyObject = UnityEngine.Object.Instantiate(
            Resources.Load<GameObject>("Enemies/VolleyEaterBoss"));
        UniversalBossAbility volley = volleyObject.GetComponent<UniversalBossAbility>();
        Brick volleyBrick = volleyObject.GetComponent<Brick>();
        volleyBrick.hp = volleyBrick.MaxHp * 0.5f;
        Animal firstShot = new GameObject("First swallowed shot").AddComponent<Animal>();
        firstShot.enabled = false;
        firstShot.SetComboVolley(43);
        firstShot.dmg = 100f;
        firstShot.gameObject.AddComponent<FireflyImpactEffect>();
        Animal secondShot = new GameObject("Second swallowed shot").AddComponent<Animal>();
        secondShot.enabled = false;
        secondShot.SetComboVolley(43);
        secondShot.dmg = 100f;
        Animal laterShot = new GameObject("Next volley shot").AddComponent<Animal>();
        laterShot.enabled = false;
        laterShot.SetComboVolley(44);
        laterShot.dmg = 100f;
        float initialHp = volleyBrick.hp;
        Check(volleyBrick.ApplyProjectileHit(firstShot, volleyObject.transform.position, Vector2.up)
            && !firstShot.gameObject.activeSelf
            && Mathf.Approximately(volleyBrick.hp, initialHp + volleyBrick.MaxHp * 0.03f)
            && (volleyBrick.GetComponent<EnemyStatusEffects>()?.GetStackCount(
                FireflyImpactEffect.BurnEffectId) ?? 0) == 0,
            "First hit is swallowed, heals three percent and applies no burn");
        volleyBrick.ApplyProjectileHit(firstShot, volleyObject.transform.position, Vector2.up);
        Check(volley.SwallowedProjectileCount == 1
            && Mathf.Approximately(volleyBrick.hp, initialHp + volleyBrick.MaxHp * 0.03f),
            "Repeat contact with the same projectile cannot heal twice");
        volleyBrick.ApplyProjectileHit(secondShot, volleyObject.transform.position, Vector2.up);
        Check(!secondShot.gameObject.activeSelf && volley.SwallowedProjectileCount == 2
            && Mathf.Approximately(volleyBrick.hp, initialHp + volleyBrick.MaxHp * 0.06f),
            "Every distinct projectile in the first volley heals three percent");
        float healedHp = volleyBrick.hp;
        volleyBrick.ApplyProjectileHit(laterShot, volleyObject.transform.position, Vector2.up);
        Check(laterShot.gameObject.activeSelf && volley.SwallowedProjectileCount == 2
            && Mathf.Approximately(volleyBrick.hp, healedHp - laterShot.dmg),
            "The next volley deals its full normal damage");

        GameObject idolObject = UnityEngine.Object.Instantiate(
            Resources.Load<GameObject>("Enemies/ThreeSealIdolBoss"));
        UniversalBossAbility idol = idolObject.GetComponent<UniversalBossAbility>();
        Brick idolBrick = idolObject.GetComponent<Brick>();
        Check(idol.ModifyDamage(idolBrick, projectile, idolObject.transform.position, 100f) == 0f
            && idol.ModifyDamage(idolBrick, projectile, idolObject.transform.position, 100f) == 0f
            && idol.ModifyDamage(idolBrick, projectile, idolObject.transform.position, 100f) == 0f
            && idol.SealsRemaining == 0
            && Mathf.Approximately(idol.ModifyDamage(idolBrick, projectile,
                idolObject.transform.position, 100f), 130f),
            "Three seals absorb hits, then the idol takes bonus direct damage");
    }

    private static void SetLastVolley(int id)
    {
        typeof(Player).GetField("currentCombatVolleyId",
            BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, id);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        report.Add("PASS " + description);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Universal boss validation timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        report.Add(error == null ? "UNIVERSAL_BOSSES_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/UniversalBossValidation.txt", report);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
