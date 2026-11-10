using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FrogMechanicsValidation
{
    private const string Running = "ForestAnimalWar.FrogValidation.Running";
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static LevelGenerator room;
    private static Camera camera;
    private static GameObject shotPrefab;
    private static FrogRider riderPrefab;
    private static int lastFrame = -1;
    private static double deadline;

    static FrogMechanicsValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            Steps.Clear(); Steps.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated Unity project copy.");
        Directory.CreateDirectory("Logs");
        FrogProjectileAuthoring.UpdateCombatVisuals();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        var player = new GameObject("Player"); player.tag = "Player"; player.transform.position = Vector3.down * 8;
        camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true; camera.orthographicSize = 1.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.1f, 0.17f, 0.14f);
        var stage = new GameObject("Frog Test Room", typeof(Stage), typeof(LevelGenerator));
        room = stage.GetComponent<LevelGenerator>();
        Check(GameTypography.TmpFont != null && GameTypography.TmpFont.material != null,
            "Isolated test has the game's combat text font and material");
        shotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Frog.prefab");
        riderPrefab = shotPrefab.GetComponent<FrogProjectile>().RiderPrefab;
        Check(shotPrefab.GetComponent<Animal>().dmg == 15f && riderPrefab != null,
            "Authored frog projectile deals 15 base damage and references its authored rider");
        var riderData = new SerializedObject(riderPrefab);
        Check(Mathf.Approximately(riderData.FindProperty("jumpSeconds").floatValue, 0.1f),
            "Authored frog jump lasts 0.1 seconds");
        Sprite seatedSprite = ((SpriteRenderer)riderData.FindProperty("frogVisual").objectReferenceValue).sprite;
        Check(AssetDatabase.GetAssetPath(seatedSprite).EndsWith("frog-seated.png")
                && seatedSprite != shotPrefab.GetComponent<SpriteRenderer>().sprite,
            "Seated frog uses a separate overhead sprite");
        Check(ProjectilePhysics2D.GetCollisionRadius(shotPrefab)
                < ProjectilePhysics2D.GetCollisionRadius(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/BaseBullet.prefab")),
            "Frog collider is smaller than the ordinary projectile");

        Brick host = Target(Vector2.zero, 1000f);
        for (int i = 0; i < 4; i++) FireAt(host);
        yield return Frames(4);
        Check(FrogRider.CountOn(host) == 4 && Mathf.Approximately(host.hp, 790f),
            "Four initial landings add 60 impact damage plus 150 from seated count-change bites, within four seats");
        FrogRider[] sitting = Object.FindObjectsOfType<FrogRider>().Where(f => f.Host == host).ToArray();
        Check(sitting.All(f => f.DislodgementCount == 0 && !f.IsJumping),
            "Actual frog projectile arrivals and count-change bites never provoke seated frogs");
        Check(sitting.All(f => f.IsSeated) && sitting.Select(f => f.transform.position).Distinct().Count() == 4,
            "Four frogs occupy distinct inset corners: " + string.Join("; ", sitting.Select(f =>
                f.IsSeated + ":" + f.transform.position + ":slot="
                + typeof(FrogRider).GetField("seatSlot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f))));
        Check(sitting.All(f => Mathf.Abs(f.transform.position.x) < 0.3f
                && Mathf.Abs(f.transform.position.y) < 0.3f)
                && !Player.HasActiveProjectiles && Player.animalsCollect.Count == 0,
            "Seated frogs stay inside the enemy and release the projectile phase");
        float[] seatAngles = sitting.Select(f => f.GetComponentsInChildren<SpriteRenderer>()
            .First(r => r.sprite == seatedSprite).transform.eulerAngles.z).OrderBy(a => a).ToArray();
        Check(seatAngles.Zip(new[] { 45f, 135f, 225f, 315f },
                (actual, expected) => Mathf.Abs(Mathf.DeltaAngle(actual, expected)) < 0.01f).All(v => v),
            "Four seated frogs face different diagonals at 45, 135, 225 and 315 degrees");
        yield return Seconds(0.7f);
        CaptureBattle("Logs/FrogCorners.png");

        Transform body = host.GetComponentInChildren<SpriteRenderer>().transform;
        var seatOffsets = sitting.ToDictionary(f => f,
            f => body.InverseTransformPoint(f.transform.position + Vector3.forward * 0.02f));
        Vector3 bodyScale = body.lossyScale;
        EnemyVisualHover hover = body.gameObject.AddComponent<EnemyVisualHover>();
        yield return Frames(20);
        Check(sitting.All(f => Vector3.Distance(body.InverseTransformPoint(
                f.transform.position + Vector3.forward * 0.02f), seatOffsets[f]) < 0.0001f),
            "Seated frogs follow the actual enemy hover and sway in the same frame");
        hover.enabled = false;
        Object.Destroy(hover);
        host.transform.position = new Vector3(0.12f, 0.3f, 0f);
        host.transform.localScale = new Vector3(0.85f, 1.15f, 1f);
        body.localPosition = new Vector3(0.04f, 0.09f, 0f);
        body.localRotation = Quaternion.Euler(0f, 0f, 20f);
        yield return Frames(3);
        Check(sitting.All(f => Vector3.Distance(body.InverseTransformPoint(
                f.transform.position + Vector3.forward * 0.02f), seatOffsets[f]) < 0.0001f),
            "Seat offsets follow enemy movement, rotation and step squash without sliding");
        Check(sitting.Select(f => Mathf.Repeat(f.GetComponentsInChildren<SpriteRenderer>()
                .First(r => r.sprite == seatedSprite).transform.eulerAngles.z - body.eulerAngles.z - 45f,
                90f)).All(angle => angle < 0.01f || angle > 89.99f),
            "Frog corner rotations follow the enemy's visual rotation");
        Check(sitting.All(f => Mathf.Approximately(f.GetComponentsInChildren<SpriteRenderer>()
                .First(r => r.sprite == seatedSprite).transform.localScale.x,
                0.4f * body.lossyScale.x / bodyScale.x)),
            "Frogs share the enemy's step stretch and squash");
        host.transform.position = Vector3.zero; host.transform.localScale = Vector3.one;
        body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity;
        yield return Frames(3);

        var fifth = Object.Instantiate(shotPrefab, new Vector3(0, -1.2f, 0), Quaternion.identity);
        fifth.GetComponent<ProjectilePhysics2D>().Launch(Vector2.up, 4f);
        yield return Seconds(0.4f);
        Check(FrogRider.CountOn(host) == 4 && Mathf.Approximately(host.hp, 790f)
                && fifth.GetComponent<Rigidbody2D>().velocity.y < 0f,
            "The fifth frog physically ricochets from a full enemy without damage");
        Object.Destroy(fifth);
        FrogRider.ProcessAllAfterEnemyTurn(1);
        Check(Mathf.Approximately(host.hp, 730f), "Four seated frogs deal 60 damage per turn");
        FrogRider.ProcessAllAfterEnemyTurn(1);
        Check(Mathf.Approximately(host.hp, 730f), "A repeated turn number cannot tick frogs twice");
        host.TakeDamage(1f, CombatDamageSource.Fire);
        host.TakeDamage(1f, CombatDamageSource.Poison);
        host.TakeDamage(1f, CombatDamageSource.Frog);
        Check(FrogRider.CountOn(host) == 4 && sitting.All(f => f.IsSeated),
            "Fire, poison and frog damage do not trigger jumps");

        Brick occupiedWeak = Target(new Vector2(1, 0), 45f);
        FireAt(occupiedWeak); yield return Frames(3);
        Brick unoccupied = Target(new Vector2(0, 1), 200f);
        Brick distant = Target(new Vector2(2, 2), 500f);
        Brick occupiedFull = Target(new Vector2(-1, 0), 1500f);
        for (int i = 0; i < 4; i++) FireAt(occupiedFull);
        Brick outsideRadius = Target(new Vector2(3, 0), 100f);
        yield return Frames(4);
        Brick[] randomTargets = { occupiedWeak, unoccupied, distant, occupiedFull };
        int[] selectedCounts = new int[randomTargets.Length];
        var excluded = new HashSet<Brick> { host };
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(3187);
        for (int i = 0; i < 256; i++)
        {
            Brick chosen = sitting[0].FindTarget(room, host.transform.position, excluded);
            int choice = Array.IndexOf(randomTargets, chosen);
            if (choice < 0) throw new Exception("Random jump selected an enemy outside the radius or an excluded target");
            selectedCounts[choice]++;
        }
        UnityEngine.Random.state = randomState;
        Check(selectedCounts.All(count => count > 24 && count < 110),
            "Random jumps can choose weak, strong, distant and fully occupied enemies equally: "
            + string.Join(", ", selectedCounts));
        foreach (Brick extra in new[] { occupiedWeak, distant, occupiedFull, outsideRadius })
            Object.Destroy(extra.gameObject);
        yield return Frames(3);
        var positions = sitting.ToDictionary(f => f, f => f.transform.position);
        host.TakeDamage(1f);
        Check(FrogRider.CountOn(host) == 3 && sitting.Count(f => f.IsJumping) == 1,
            "Exactly one frog leaves on one ordinary damage event");
        Check(Mathf.Approximately(host.hp, 681f), "The three remaining frogs immediately bite for 45 when one leaves");
        yield return Seconds(0.05f);
        FrogRider jumper = sitting.First(f => f.IsJumping);
        TrailRenderer jumpTrail = jumper.GetComponentInChildren<TrailRenderer>();
        Check(jumpTrail != null && jumpTrail.positionCount >= 2,
            "A jump draws an authored trail along the actual frog path");
        CaptureBattle("Logs/FrogJumpTrail.png");
        yield return Seconds(0.3f);
        Check(FrogRider.CountOn(unoccupied) == 1 && Mathf.Approximately(unoccupied.hp, 155f),
            "Jump deals doubled damage and the newly seated frog adds its count-change bite");
        yield return Seconds(0.1f);
        Check(!jumpTrail.emitting && jumpTrail.positionCount == 0,
            "Jump trail completely disappears after landing and never follows seated enemy movement");
        Check(sitting.Where(f => f.Host == host).All(f => f.transform.position == positions[f]),
            "Remaining frogs keep their original corners after one jumps");
        yield return ClearTargets();

        host = Target(Vector2.zero, 1000f);
        for (int i = 0; i < 4; i++) FireAt(host);
        yield return Frames(4);
        Brick chorusDestination = Target(Vector2.right, 1000f);
        host.hp = 40f;
        host.TakeDamage(1f);
        Check(host.IsDefeated && Object.FindObjectsOfType<FrogRider>().Count(f => f.IsJumping) == 4,
            "A departure chorus can kill the host and safely release all four frogs without recursive nonlethal jumps");
        yield return Seconds(0.4f);
        Check(FrogRider.CountOn(chorusDestination) == 4 && Mathf.Approximately(chorusDestination.hp, 730f)
            && !FrogRider.HasActiveJumps,
            "The four death releases share a reaction, all settle and do not repeatedly dislodge one another");
        yield return ClearTargets();

        foreach (CombatDamageSource source in new[] { CombatDamageSource.Other, CombatDamageSource.Fire,
            CombatDamageSource.Poison, CombatDamageSource.Frog })
        {
            host = Target(Vector2.zero, 1000f);
            for (int i = 0; i < 4; i++) FireAt(host);
            yield return Frames(4);
            Brick[] neighbours = new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left }
                .Select((position, i) => Target(position, 400f + i * 100f)).ToArray();
            host.TakeDamage(10000f, source);
            Check(Object.FindObjectsOfType<FrogRider>().Count(f => f.IsJumping) == 4,
                "Every seated frog jumps immediately on host death from " + source);
            float settleDeadline = Time.time + 5f;
            while (FrogRider.HasActiveJumps && Time.time < settleDeadline)
            {
                if (neighbours.Where(e => e != null).Any(e => FrogRider.CountOn(e) > FrogRider.MaximumPerEnemy))
                    throw new Exception("Concurrent death releases exceeded the four-seat limit: " + source);
                yield return null;
            }
            Check(!FrogRider.HasActiveJumps && Object.FindObjectsOfType<FrogRider>().All(f => f.IsFalling
                || (f.IsSeated && f.Host != null && !f.Host.IsDefeated && f.DislodgementCount >= 1)),
                "Concurrent death-release chains settle or fall without stranding the turn: " + source);
            yield return ClearTargets();
        }

        host = Target(Vector2.zero, 1000f);
        for (int i = 0; i < 4; i++) FireAt(host);
        yield return Frames(4);
        FrogRider[] stranded = Object.FindObjectsOfType<FrogRider>();
        host.TakeDamage(10000f);
        yield return Frames(4);
        Check(!FrogRider.HasActiveJumps && stranded.All(f => f == null || f.IsFalling),
            "All frogs fall and release the turn when the dead host has no nearby targets");
        yield return ClearTargets();

        host = Target(Vector2.zero, 1000f);
        Brick full = Target(new Vector2(1, 0), 1000f);
        for (int i = 0; i < 4; i++) FireAt(full);
        FireAt(host); yield return Frames(4);
        Brick free = Target(new Vector2(3, 0), 1000f);
        host.TakeDamage(1f); yield return Seconds(0.5f);
        Check(FrogRider.CountOn(full) == 4 && Mathf.Approximately(full.hp, 700f)
                && FrogRider.CountOn(host) + FrogRider.CountOn(free) == 1,
            "A first jump damages a full target, triggers four resident bites and redirects without dislodging residents");
        yield return ClearTargets();

        // Exercise the actual authored 3x3 boss, not just an ordinary test target.
        for (int residentCount = 3; residentCount <= 4; residentCount++)
        {
            // This boss has no random phasing, so all setup shots reliably land.
            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/BoneDragonBoss.prefab");
            Brick boss = Object.Instantiate(bossPrefab, Vector3.zero, Quaternion.identity, room.transform).GetComponent<Brick>();
            boss.levelGenerator = room; boss.hp = 1000f;
            typeof(Brick).GetField("maxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boss, 1000f);
            room.EnemyCollection.Add(boss.gameObject);
            Check(boss.UsesBossHealth && boss.Footprint == new Vector2Int(3, 3), "Uses the authored three-cell Bone Dragon boss");
            for (int i = 0; i < residentCount; i++) FireAt(boss);
            yield return Frames(4);
            FrogRider[] bossResidents = Object.FindObjectsOfType<FrogRider>().Where(f => f.Host == boss).ToArray();
            host = Target(new Vector2(-2, 0), 1000f); FireAt(host); yield return Frames(4);
            FrogRider arriving = Object.FindObjectsOfType<FrogRider>().Single(f => f.Host == host);
            float previousBossHealth = boss.hp;
            host.TakeProjectileDamage(1f, 701 + residentCount); yield return Seconds(0.5f);
            Check(bossResidents.Length == residentCount && bossResidents.All(f => f.IsSeated && !f.IsJumping
                    && f.Host == boss && f.DislodgementCount == 0),
                "Arrival leaves all boss residents seated with unchanged counters: " + residentCount
                    + "; residents=" + bossResidents.Length + "; hp=" + boss.hp + "; states="
                    + string.Join(", ", bossResidents.Select(f => f == null ? "destroyed" :
                        f.IsSeated + "/" + f.IsJumping + "/" + f.IsFalling + "/count="
                        + f.DislodgementCount + "/host=" + (f.Host == boss ? "boss" : "other"))));
            Check(Mathf.Approximately(previousBossHealth - boss.hp, 90f),
                "Boss receives one 30-damage jump and exactly four 15-damage bites: " + residentCount + " prior residents");
            Check(arriving.IsSeated && !arriving.IsFalling && !FrogRider.HasActiveJumps
                    && (residentCount == 3 ? arriving.Host == boss : arriving.Host == host)
                    && FrogRider.CountOn(boss) == 4,
                "Incoming frog takes the free boss seat or redirects from the full boss without dislodging anyone: " + residentCount);
            yield return ClearTargets();
        }

        host = Target(Vector2.zero, 1000f); FireAt(host); yield return Frames(3);
        Brick[] chain = Enumerable.Range(1, 6).Select(i => Target(new Vector2(i * 2, 0),
            i == 6 ? 1000f : 20f * Mathf.Pow(2, i - 1))).ToArray();
        host.TakeDamage(10000f); yield return Seconds(1.1f);
        Check(chain.Take(5).All(e => e == null || e.IsDefeated) && chain[5].hp == 1000f
                && !FrogRider.HasActiveJumps && !Object.FindObjectsOfType<FrogRider>().Any(f => f.IsSeated),
            "Fatal destinations redirect up to five actual hops, without attacking a sixth enemy");
        yield return ClearTargets();

        host = Target(Vector2.zero, 1000f); FireAt(host); yield return Frames(3);
        Brick[] chorusChain = Enumerable.Range(1, 6).Select(i => Target(new Vector2(i * 2, 0),
            i == 6 ? 1000f : 15f * Mathf.Pow(2, i) + 10f)).ToArray();
        host.TakeDamage(10000f); yield return Seconds(1.1f);
        Check(chorusChain.Take(5).All(e => e == null || e.IsDefeated)
                && chorusChain[5].hp == 1000f && !FrogRider.HasActiveJumps,
            "Arrival-bite deaths redirect within the same five-hop cap without recursively restarting the chain");
        yield return ClearTargets();

        host = Target(Vector2.zero, 1000f); FireAt(host); FireAt(host); yield return Frames(3);
        FrogRider.UpgradeAll(2f, 3, false);
        Check(Mathf.Approximately(FrogRider.DamageMultiplier(room), 1.06f),
            "Chorus upgrades affect existing frogs, adding 3 percent for each of two frogs");
        var otherRoom = new GameObject("Other Room", typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        Brick local = Target(new Vector2(2, 0), 1000f);
        Brick other = Target(new Vector2(3, 0), 1000f, otherRoom);
        local.TakeDamage(100f); other.TakeDamage(100f);
        Check(Mathf.Approximately(local.hp, 894f) && Mathf.Approximately(other.hp, 900f),
            "Chorus buffs actual room damage and cannot affect another room");
        Object.Destroy(Object.FindObjectsOfType<FrogRider>().First().gameObject); yield return Frames(2);
        Check(Mathf.Approximately(FrogRider.DamageMultiplier(room), 1.03f),
            "Removing a frog removes its room damage bonus");
        yield return ClearTargets();

        host = Target(Vector2.zero, 1000f);
        Brick near = Target(Vector2.right, 1000f);
        Brick outside = Target(new Vector2(1, 1), 1000f);
        FireAt(host, true); yield return Frames(3);
        Check(Mathf.Approximately(host.hp, 970f) && Mathf.Approximately(near.hp, 970f)
                && Mathf.Approximately(outside.hp, 1000f),
            "KVA landing hits the target and enemies within a circular radius of one cell");
        FrogRider.ProcessAllAfterEnemyTurn(2);
        Check(Mathf.Approximately(host.hp, 955f) && Mathf.Approximately(near.hp, 955f),
            "KVA also applies area damage to the periodic bite");
        yield return ClearTargets();

        var queue = new GameObject("Upgrade Queue").AddComponent<AnimalProjectileQueue>();
        queue.enabled = false;
        var entries = new List<AnimalProjectileQueue.AnimalProjectileEntry>();
        for (int i = 0; i <= AnimalProjectileQueue.FrogIndex; i++)
        {
            var entry = new AnimalProjectileQueue.AnimalProjectileEntry();
            typeof(AnimalProjectileQueue.AnimalProjectileEntry).GetField("prefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entry, shotPrefab);
            entries.Add(entry);
        }
        typeof(AnimalProjectileQueue).GetField("animals", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(queue, entries);
        queue.ReloadAll();
        int index = AnimalProjectileQueue.FrogIndex;
        Check(!queue.GetAvailableUpgradeOptions().Any(o => o.Kind == AnimalProjectileQueue.UpgradeKind.FrogUltimate),
            "KVA isn't offered before the three frog branches are complete");
        for (int rank = 1; rank <= 3; rank++)
        {
            Check(queue.Upgrade(index, AnimalProjectileQueue.UpgradeKind.FrogRadius)
                    && queue.Upgrade(index, AnimalProjectileQueue.UpgradeKind.FrogChorus)
                    && Mathf.Approximately(queue.GetFrogJumpRadius(index), rank + 2f)
                    && queue.GetFrogChorusRank(index) == rank,
                "Frog radius and chorus upgrade rank " + rank);
        }
        queue.Upgrade(index, AnimalProjectileQueue.UpgradeKind.Ammo);
        queue.Upgrade(index, AnimalProjectileQueue.UpgradeKind.Ammo);
        Check(queue.Animals[index].AmmoPerTurn == 3
                && queue.GetAvailableUpgradeOptions().Any(o => o.Kind == AnimalProjectileQueue.UpgradeKind.FrogUltimate)
                && queue.Upgrade(index, AnimalProjectileQueue.UpgradeKind.FrogUltimate),
            "Three frog projectiles unlock KVA after completing radius and chorus");
        for (int i = 0; i <= AnimalProjectileQueue.FrogIndex; i++) queue.Unlock(i);
        queue.Upgrade(AnimalProjectileQueue.FireflyIndex, AnimalProjectileQueue.UpgradeKind.RepeatFire);
        queue.ReloadAll();
        var volleyPlayer = player.AddComponent<Player>();
        volleyPlayer.enabled = false;
        typeof(Player).GetField("animalQueue", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(volleyPlayer, queue);
        host = Target(Vector2.zero, 100000f); Target(Vector2.right, 100000f);
        FireAt(host); yield return Frames(4);
        FrogRider veteran = Object.FindObjectsOfType<FrogRider>().Single();
        for (int hit = 0; hit < 2; hit++)
        {
            veteran.Host.TakeProjectileDamage(1f, 501);
            yield return Seconds(0.6f);
        }
        Check(veteran.IsSeated && veteran.DislodgementCount == 2, "Pre-existing frog has a two-hop streak before the native combined volley");
        var launchedTypes = new List<int>();
        Player.VolleyStarted += launchedTypes.Add;
        try
        {
            var combined = (IEnumerator)typeof(Player).GetMethod("ShootAllQueueCoroutine",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(volleyPlayer, new object[] { Vector2.up });
            volleyPlayer.StartCoroutine(combined);
            Check(veteran.DislodgementCount == 0 && veteran.IsSeated && Player.CurrentVolleyId > 0,
                "Native combined volley resets resident streaks exactly once at its start");
            FieldInfo shooting = typeof(Player).GetField("isShooting", BindingFlags.Instance | BindingFlags.NonPublic);
            float limit = Time.time + 4f;
            while ((bool)shooting.GetValue(volleyPlayer) && Time.time < limit)
            {
                foreach (Animal animal in Player.animalsCollect)
                    if (animal != null) animal.GetComponent<Rigidbody2D>().simulated = false;
                yield return null;
            }
            Check(launchedTypes.SequenceEqual(new[] {
                    AnimalProjectileQueue.FrogIndex,
                    AnimalProjectileQueue.FireflyIndex, AnimalProjectileQueue.FireflyIndex,
                    AnimalProjectileQueue.BoarIndex, AnimalProjectileQueue.RabbitIndex,
                    AnimalProjectileQueue.HedgehogIndex, AnimalProjectileQueue.HoneyBadgerIndex,
                    AnimalProjectileQueue.BeaverIndex }),
                "Real combined volley launches frog immediately before firefly, including firefly repeats: "
                + string.Join(", ", launchedTypes));
            Check(Player.animalsCollect.Count(a => a != null && a.CombatSource == CombatDamageSource.Frog) == 3
                    && Player.animalsCollect.Count(a => a != null) == 10,
                "Combined volley actually creates the three frog bodies and every other queued projectile");
            Check(Enumerable.Range(0, 7).All(i => queue.GetRemainingAmmo(i) == 0
                    && queue.GetRemainingVolleys(i) == 0),
                "Combined volley consumes every available type, including all three frog projectiles");
            Animal realShot = Player.animalsCollect.First(a => a != null && a.CombatSource != CombatDamageSource.Frog);
            Check(Player.animalsCollect.Where(a => a != null).All(a => a.ComboVolleyId == realShot.ComboVolleyId),
                "All types and repeated shots in the native combined volley share one streak identity");
            int comboBefore = ProjectileComboUI.Instance.HitCount;
            veteran.Host.TakeProjectileDamage(1f, realShot.ComboVolleyId);
            yield return Seconds(0.4f);
            Check(veteran.IsSeated && veteran.DislodgementCount == 1
                    && ProjectileComboUI.Instance.HitCount - comboBefore == 3,
                "Old frog reacts once and credits its jump and landing bite to the attacking volley");
            veteran.Host.TakeProjectileDamage(1f, realShot.ComboVolleyId);
            yield return Seconds(0.6f);
            Check(veteran.IsSeated && veteran.DislodgementCount == 2,
                "A second actual volley hit grows the same resident streak to two hops");
        }
        finally { Player.VolleyStarted -= launchedTypes.Add; }
        foreach (Animal animal in Player.animalsCollect.ToArray())
            if (animal != null) Object.Destroy(animal.gameObject);
        yield return Frames(3);
        Check(!Player.HasActiveProjectiles, "Combined volley with frogs releases the input lock after cleanup");
        typeof(Player).GetMethod("BeginVolley", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(volleyPlayer, new object[] { -1 });
        Check(veteran.IsSeated && veteran.DislodgementCount == 0,
            "Native separate volley also clears the previous combined volley's streak");
        typeof(Player).GetMethod("ReleaseVolley", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(volleyPlayer, null);
        yield return ClearTargets();
        Check(AnimalProjectileQueue.PrepareRunLoadout(new[] { 0, 1, 2, 5, 6 }),
            "Five distinct loadout types can include the new frog and the required firefly");
        Check(queue.ConfigureRunLoadout(new[] { 0, 1, 2, 5, 6 }) && !queue.IsAvailable(6),
            "A selected frog is initially locked and unlocked through battle upgrades");

        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/UIscene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        yield return Frames(4);
        ProjectileLoadoutUI dialog = Object.FindObjectOfType<ProjectileLoadoutUI>(true);
        Check(dialog != null && dialog.Show(selected => {}), "Authored projectile choices open in the menu");
        var data = new SerializedObject(dialog);
        var choices = data.FindProperty("choices");
        Check(choices.arraySize == AnimalProjectileQueue.AnimalTypeCount, "Frog has its own authored menu choice");
        Check(AssetDatabase.GetAssetPath(((Image)choices.GetArrayElementAtIndex(6)
                .FindPropertyRelative("icon").objectReferenceValue).sprite).EndsWith("frog-ui.png"),
            "Menu uses the dedicated frog portrait icon");
        for (int i = 0; i < choices.arraySize; i++)
        {
            var choice = choices.GetArrayElementAtIndex(i);
            RectTransform rect = ((Button)choice.FindPropertyRelative("button").objectReferenceValue).GetComponent<RectTransform>();
            Check(rect.anchoredPosition.y + rect.sizeDelta.y / 2f < 244f
                    && rect.anchoredPosition.y - rect.sizeDelta.y / 2f > -351f,
                "Loadout card fits between the selected count and footer: " + i);
        }
        foreach (int i in new[] { 1, 2, 5, 6 })
            ((Button)choices.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue).onClick.Invoke();
        Check(((Button)data.FindProperty("startButton").objectReferenceValue).interactable,
            "A five-type set containing the frog enables Start");
        yield return Frames(4);
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas))
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
        }
        Canvas.ForceUpdateCanvases();
        CaptureBattle("Logs/FrogLoadout.png", 540, 960);
        dialog.Close(); Check(!dialog.IsOpen, "The frog loadout dialog can be closed without starting");
    }

    private static Brick Target(Vector2 position, float hp, LevelGenerator targetRoom = null)
    {
        targetRoom = targetRoom != null ? targetRoom : room;
        var value = new GameObject("Frog target", typeof(BoxCollider2D)); value.tag = "Enemy";
        value.transform.SetParent(targetRoom.transform); value.transform.position = position;
        var visual = new GameObject("Enemy art").AddComponent<SpriteRenderer>();
        visual.transform.SetParent(value.transform, false);
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/SwampDemon.prefab");
        value.layer = enemyPrefab.layer;
        visual.sprite = enemyPrefab.GetComponentsInChildren<SpriteRenderer>(true).First(r => r.sprite != null && r.enabled).sprite;
        visual.transform.localScale = Vector3.one * (0.9f / visual.sprite.bounds.size.x);
        Brick enemy = value.AddComponent<Brick>(); enemy.levelGenerator = targetRoom; enemy.hp = hp;
        typeof(Brick).GetField("maxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, hp);
        targetRoom.EnemyCollection.Add(value); return enemy;
    }

    private static void FireAt(Brick target, bool ultimate = false)
    {
        var shot = Object.Instantiate(shotPrefab, target.transform.position, Quaternion.identity);
        shot.GetComponent<FrogProjectile>().ConfigureRunUpgrades(2f, 0, ultimate);
        target.ApplyProjectileHit(shot.GetComponent<Animal>(), target.transform.position, Vector2.down);
    }

    private static IEnumerator ClearTargets()
    {
        foreach (FrogRider frog in Object.FindObjectsOfType<FrogRider>()) Object.Destroy(frog.gameObject);
        foreach (Brick enemy in Object.FindObjectsOfType<Brick>()) Object.Destroy(enemy.gameObject);
        yield return Frames(3); room.EnemyCollection.Clear();
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator Seconds(float duration) { float end = Time.time + duration; while (Time.time < end) yield return null; }
    private static void CaptureBattle(string path, int width = 640, int height = 640)
    {
        var render = new RenderTexture(width, height, 24); render.Create();
        camera.targetTexture = render; camera.Render();
        RenderTexture old = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null;
        Object.Destroy(image); render.Release(); Object.Destroy(render);
    }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Report.Add("PASS " + description);
    }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Frog validation timed out");
            while (Steps.Count > 0)
            {
                IEnumerator current = Steps.Peek();
                if (!current.MoveNext()) { Steps.Pop(); continue; }
                if (current.Current is IEnumerator child) { Steps.Push(child); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false);
        string result = error == null ? "FROG_MECHANICS_PASSED" : "FAIL " + error;
        Report.Add(result); File.WriteAllLines("Logs/FrogValidation.txt", Report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
