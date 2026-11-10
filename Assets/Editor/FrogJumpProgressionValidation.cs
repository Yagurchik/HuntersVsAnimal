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
public static class FrogJumpProgressionValidation
{
    private const string Running = "FrogJumpProgressionValidation.Running";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static LevelGenerator room;
    private static FrogRider prefab;
    private static int lastFrame = -1;
    private static double deadline;

    static FrogJumpProgressionValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Steps.Clear(); Steps.Push(Checks()); deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new InvalidOperationException("Run in the isolated validation project.");
        Directory.CreateDirectory("Logs");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        new GameObject("Player").tag = "Player";
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 6;
        camera.transform.position = new Vector3(0, 0, -20);
        room = new GameObject("Frog Jump Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Frog.prefab").GetComponent<FrogProjectile>().RiderPrefab;
        Check(prefab != null, "Uses the authored frog rider and its 0.1-second jump");
        Brick[] targets = new[] { new Vector2(-1, 0), Vector2.zero, Vector2.right,
            new Vector2(-1, 1), Vector2.up, Vector2.one }.Select(p => Target(p)).ToArray();
        FrogRider trained = Frog(targets[0]); yield return Frames(4);
        Check(trained.IsSeated && trained.DislodgementCount == 0, "Initial landing does not consume a dislodgement");
        foreach (var source in new[] { CombatDamageSource.Fire, CombatDamageSource.Poison, CombatDamageSource.Frog })
            trained.Host.TakeDamage(1f, source);
        Check(trained.IsSeated && trained.DislodgementCount == 0, "Ordinary fire, poison and frog damage do not start a chain");

        for (int episode = 1; episode <= 7; episode++)
        {
            int jumps = Math.Min(episode, 5);
            double previousDamage = ProjectileDamageStats.ForAnimal(AnimalProjectileQueue.FrogIndex);
            trained.Host.TakeProjectileDamage(1f, 101);
            Check(trained.IsJumping && trained.DislodgementCount == jumps, "Dislodgement " + episode + " starts a " + jumps + "-jump chain");
            yield return WaitUntil(() => trained.IsSeated && !trained.IsJumping, 3f);
            double expectedDamage = 30 * (Math.Pow(2, jumps) - 1) + 15 * jumps;
            double realisedDamage = ProjectileDamageStats.ForAnimal(AnimalProjectileQueue.FrogIndex) - previousDamage;
            Check(Math.Abs(realisedDamage - expectedDamage) < 0.01,
                "Chain " + episode + " completes all hops through living enemies and preserves doubling / arrival bites: " + realisedDamage);
            Check(!FrogRider.HasActiveJumps && targets.Sum(t => FrogRider.CountOn(t)) == 1,
                "Only the final landing settles the frog and releases the jump phase: " + episode);
        }

        Brick lastHost = trained.Host;
        foreach (Brick target in targets.Where(t => t != lastHost)) Remove(target);
        yield return Frames(2);
        Vector2 start = lastHost.transform.position;
        Brick[] lethalChain = Enumerable.Range(1, 6).Select(i => Target(start + Vector2.right * (2 * i),
            i <= 5 ? 20f * Mathf.Pow(2, i - 1) : 1000f)).ToArray();
        lastHost.TakeProjectileDamage(2000000f, 101);
        yield return WaitUntil(() => !FrogRider.HasActiveJumps, 3f);
        Check(lethalChain.Take(5).All(t => t == null || t.IsDefeated) && lethalChain[5].hp == 1000f
            && (trained == null || trained.IsFalling), "A trained frog stops after five lethal hops and never attacks a sixth enemy");
        yield return Clear();

        // A small surviving pack must support long series without exhausting a
        // lifetime visited set. Each hit below belongs to the same volley.
        Brick left = Target(Vector2.zero); Brick right = Target(Vector2.right);
        trained = Frog(left); yield return Frames(4);
        for (int episode = 1; episode <= 7; episode++)
        {
            double before = ProjectileDamageStats.ForAnimal(AnimalProjectileQueue.FrogIndex);
            trained.Host.TakeProjectileDamage(1f, 201);
            yield return WaitUntil(() => !trained.IsJumping, 2f);
            int jumps = Math.Min(episode, 5);
            double expected = 30 * (Math.Pow(2, jumps) - 1) + 15 * jumps;
            Check(trained.IsSeated && !trained.IsFalling && trained.DislodgementCount == jumps
                && Math.Abs(ProjectileDamageStats.ForAnimal(AnimalProjectileQueue.FrogIndex) - before - expected) < 0.01,
                "Two surviving enemies support " + jumps + " random hops without dropping the frog: hit " + episode);
        }
        FrogRider.BeginVolley(202);
        Check(trained.DislodgementCount == 0 && trained.IsSeated, "Starting a new volley clears the streak while preserving the seated frog");
        trained.Host.TakeProjectileDamage(1f, 202);
        Check(trained.DislodgementCount == 1, "First hit of the next volley starts at one hop");
        yield return WaitUntil(() => trained.IsSeated && !trained.IsJumping, 2f);
        trained.Host.TakeProjectileDamage(1f, 202);
        Check(trained.DislodgementCount == 2, "Consecutive hit in that volley starts two hops");
        yield return WaitUntil(() => trained.IsSeated && !trained.IsJumping, 2f);
        trained.Host.TakeProjectileDamage(1f, 203);
        Check(trained.DislodgementCount == 1, "A different damage volley also resets the streak without an explicit reset");
        yield return WaitUntil(() => trained.IsSeated && !trained.IsJumping, 2f);
        trained.Host.TakeDamage(1f);
        Check(trained.DislodgementCount == 1, "Damage outside a volley cannot retain a previous five-hop streak");
        yield return WaitUntil(() => trained.IsSeated && !trained.IsJumping, 2f);
        trained.Host.TakeDamage(1f);
        Check(trained.DislodgementCount == 1, "Separate off-volley reactions each remain one hop");
        yield return WaitUntil(() => trained.IsSeated && !trained.IsJumping, 2f);
        yield return Clear();

        // Arrivals must leave resident frogs seated, including repeated visits.
        left = Target(Vector2.zero); right = Target(Vector2.right);
        FrogRider first = Frog(left); FrogRider second = Frog(right); yield return Frames(4);
        left.TakeProjectileDamage(1f, 301);
        yield return WaitUntil(() => !FrogRider.HasActiveJumps, 2f);
        Check(first.IsSeated && second.IsSeated && first.Host == right && second.Host == right
            && first.DislodgementCount == 1 && second.DislodgementCount == 0,
            "A jump lands beside a resident without dislodging it or starting a mutual chain");
        first.Host.TakeProjectileDamage(1f, 301);
        yield return WaitUntil(() => !FrogRider.HasActiveJumps, 3f);
        Check(first.IsSeated && second.IsSeated && first.Host == right && second.Host == right
            && first.DislodgementCount == 2 && second.DislodgementCount == 0,
            "A two-hop return leaves the resident seated and increments only the externally hit frog");
        first.Host.TakeProjectileDamage(1f, 302);
        yield return WaitUntil(() => !FrogRider.HasActiveJumps, 2f);
        Check(first.IsSeated && second.IsSeated && first.DislodgementCount == 1 && second.DislodgementCount == 0,
            "A later volley resets the hit frog to one hop while the other frog remains unprovoked");
        yield return Clear();

        Brick origin = Target(new Vector2(-2, 0)); Brick destination = Target(Vector2.zero);
        var residents = Enumerable.Range(0, 3).Select(i => Frog(destination)).ToArray();
        FrogRider incoming = Frog(origin); yield return Frames(4);
        float healthBeforeLanding = destination.hp;
        origin.TakeProjectileDamage(1f, 351);
        yield return WaitUntil(() => incoming.IsSeated && !incoming.IsJumping, 1f);
        Check(incoming.Host == destination && FrogRider.CountOn(destination) == 4
            && residents.All(f => f.IsSeated && !f.IsJumping && f.DislodgementCount == 0),
            "Landing in the fourth free seat leaves all three residents seated");
        Check(Math.Abs(healthBeforeLanding - destination.hp - 90f) < 0.01f,
            "Jump damage and all four count-change bites remain active without provoking residents");
        yield return Clear();

        origin = Target(new Vector2(-2, 0)); destination = Target(Vector2.zero); Target(new Vector2(2, 0));
        residents = Enumerable.Range(0, 4).Select(i => Frog(destination)).ToArray();
        incoming = Frog(origin); yield return Frames(4);
        healthBeforeLanding = destination.hp;
        origin.TakeDamage(1f);
        yield return WaitUntil(() => incoming.IsSeated && !incoming.IsJumping, 1f);
        Check(residents.All(f => f.DislodgementCount == 0 && f.IsSeated && !f.IsJumping),
            "A full landing target keeps all four residents seated without changing their counters");
        Check(FrogRider.CountOn(destination) == 4 && Math.Abs(healthBeforeLanding - destination.hp - 90f) < 0.01f,
            "A full target receives jump damage and four resident bites without freeing a seat");
        yield return WaitUntil(() => !FrogRider.HasActiveJumps, 3f);
        Check(incoming.Host != destination && incoming.IsSeated && !incoming.IsFalling,
            "Incoming frog redirects to an available living target rather than dislodging a resident");
        yield return Clear();

        origin = Target(Vector2.zero); destination = Target(Vector2.right);
        trained = Frog(origin); yield return Frames(4); origin.TakeProjectileDamage(1f, 401);
        yield return WaitUntil(() => trained.IsSeated && !trained.IsJumping, 1f);
        Remove(origin); yield return Frames(2);
        trained.Host.transform.position = Vector2.zero; room.NotifyEnemyStateChanged();
        Brick intermediate = Target(new Vector2(2, 0)); Brick final = Target(new Vector2(4, 0));
        FrogRider firstResident = Frog(intermediate); FrogRider finalResident = Frog(final);
        yield return Frames(4); trained.Host.TakeProjectileDamage(2000000f, 401);
        yield return WaitUntil(() => trained.IsSeated && !trained.IsJumping && trained.Host == final, 2f);
        Check(trained.DislodgementCount == 2 && firstResident.DislodgementCount == 0 && finalResident.DislodgementCount == 0
            && firstResident.IsSeated && finalResident.IsSeated && !FrogRider.HasActiveJumps,
            "Both intermediate and final landings preserve the seated residents throughout a two-hop chain");
        yield return Clear();

        origin = Target(new Vector2(-2, 0)); destination = Target(Vector2.zero); Brick secondOrigin = Target(new Vector2(2, 0));
        FrogRider firstAirborne = Frog(origin); FrogRider secondAirborne = Frog(secondOrigin);
        typeof(FrogRider).GetField("jumpSeconds", Private).SetValue(secondAirborne, 0.4f);
        yield return Frames(4); origin.TakeDamage(1f); secondOrigin.TakeDamage(1f);
        yield return WaitUntil(() => firstAirborne.IsSeated, 1f);
        Check(secondAirborne.IsJumping && secondAirborne.Host == destination && secondAirborne.DislodgementCount == 1
            && firstAirborne.DislodgementCount == 1, "Airborne seat reservations are not provoked by another landing");
        yield return Clear();

        Brick dying = Target(Vector2.zero, 10f); Brick neighbour = Target(Vector2.right); Brick otherNeighbour = Target(Vector2.up);
        FrogRider released = Frog(dying);
        yield return WaitUntil(() => released.IsSeated && !released.IsJumping, 2f);
        Check(released.DislodgementCount == 1 && Math.Abs(neighbour.hp + otherNeighbour.hp - 1999955f) < 0.01f,
            "Death during the initial landing starts only the first one-hop chain");
        yield return Clear();

        origin = Target(Vector2.zero); var deathRiders = Enumerable.Range(0, 4).Select(i => Frog(origin)).ToArray();
        yield return Frames(4); origin.TakeDamage(2000000f, CombatDamageSource.Fire);
        Check(deathRiders.All(f => f.IsJumping && f.DislodgementCount == 1), "Fatal fire releases every resident as its first dislodgement");
        yield return WaitUntil(() => !FrogRider.HasActiveJumps, 1f);
        Check(deathRiders.All(f => f == null || f.IsFalling), "With no nearby enemy all released frogs fall and release the phase");
        yield return Clear();

        // A death during a shared arrival bite must not restart this frog's chain.
        origin = Target(Vector2.zero); trained = Frog(origin); yield return Frames(4);
        Brick almostDead = Target(Vector2.right, 40f); Brick untouched = Target(new Vector2(3, 0));
        origin.TakeProjectileDamage(2000000f, 501); yield return WaitUntil(() => !FrogRider.HasActiveJumps, 2f);
        Check((almostDead == null || almostDead.IsDefeated) && untouched.hp == 999925f
            && trained.IsSeated && trained.Host == untouched && trained.DislodgementCount == 1,
            "An arrival-bite death redirects within the five-hop safety limit and seats on the next survivor");
        yield return Clear();
    }

    private static Brick Target(Vector2 position, float health = 1000000f)
    {
        var value = new GameObject("Jump Target"); value.tag = "Enemy";
        value.transform.SetParent(room.transform); value.transform.position = position;
        Brick enemy = value.AddComponent<Brick>(); enemy.levelGenerator = room; enemy.hp = health;
        typeof(Brick).GetField("maxHp", Private).SetValue(enemy, health);
        room.EnemyCollection.Add(value); room.NotifyEnemyStateChanged();
        return enemy;
    }
    private static FrogRider Frog(Brick target)
    {
        FrogRider frog = Object.Instantiate(prefab, room.transform);
        frog.Initialize(target, 15f, 2f, 0, false, 0); return frog;
    }
    private static void Remove(Brick target)
    {
        if (target == null) return;
        room.DestroyEnemy(target.gameObject, false); Object.Destroy(target.gameObject);
    }
    private static IEnumerator Clear()
    {
        foreach (FrogRider frog in Object.FindObjectsOfType<FrogRider>()) Object.Destroy(frog.gameObject);
        foreach (Brick target in Object.FindObjectsOfType<Brick>()) Remove(target);
        yield return Frames(3);
        Check(!FrogRider.HasActiveJumps, "Cleanup leaves no active frog chains");
    }
    private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
    {
        float end = Time.time + timeout;
        do
        {
            yield return null;
            foreach (GameObject value in room.EnemyCollection)
                if (value != null && value.TryGetComponent(out Brick target) && FrogRider.CountOn(target) > FrogRider.MaximumPerEnemy)
                    throw new Exception("A concurrent jump reserved more than four frog seats");
            if (Time.time > end) throw new TimeoutException("Frog chain did not settle within " + timeout + " seconds");
        } while (!condition());
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description); Report.Add("PASS " + description);
    }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Frog progression validation timed out");
            while (Steps.Count > 0) { var step = Steps.Peek(); if (!step.MoveNext()) { Steps.Pop(); continue; } if (step.Current is IEnumerator child) { Steps.Push(child); continue; } return; }
            Finish(null);
        } catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false);
        Report.Add(error == null ? "FROG_JUMP_PROGRESSION_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/FrogJumpProgressionValidation.txt", Report);
        if (error != null) Debug.LogException(error); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
