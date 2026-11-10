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
public static class SquirrelMechanicsValidation
{
    private const string Running = "ForestAnimalWar.SquirrelValidation";
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static LevelGenerator room;
    private static Camera camera;
    private static GameObject shotPrefab;
    private static SquirrelAcorn seedPrefab;
    private static int lastFrame = -1;
    private static double deadline;

    static SquirrelMechanicsValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Steps.Clear(); Steps.Push(Checks()); deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use an isolated project copy.");
        Directory.CreateDirectory("Logs"); SquirrelProjectileAuthoring.Bake();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        Time.captureDeltaTime = 0.02f;
        var player = new GameObject("Player"); player.tag = "Player"; player.transform.position = Vector3.down * 8;
        camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 1.6f;
        camera.backgroundColor = new Color(0.1f, 0.17f, 0.14f); camera.clearFlags = CameraClearFlags.SolidColor;
        room = new GameObject("Acorn Test Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        shotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Squirrel.prefab");
        seedPrefab = shotPrefab.GetComponent<SquirrelProjectile>().AcornPrefab;
        Require(seedPrefab != null && shotPrefab.GetComponent<Animal>().dmg
                == AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/BaseBullet.prefab").GetComponent<Animal>().dmg,
            "Squirrel has ordinary projectile damage and its authored acorn");
        Require(AssetDatabase.LoadAllAssetsAtPath("Assets/MonsterSprite/ProjectileArt/Squirrel/vine-binding-sheet.png")
            .OfType<Sprite>().Count() == 8, "Vine animation uses eight authored imagegen frames");

        Brick target = Target(Vector2.zero, 10000);
        Brick near = Target(Vector2.right, 1000);
        Brick outside = Target(new Vector2(1, 1), 1000);
        var otherRoom = new GameObject("Other Room", typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        Brick other = Target(Vector2.zero, 1000, otherRoom);
        foreach (SpriteRenderer sprite in other.GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = false;
        Hit(target);
        SquirrelAcorn seed = SquirrelAcorn.FindOn(target);
        Require(seed != null && !seed.IsDetonated && Mathf.Approximately(seed.EarlyExplosionChance, 0.05f),
            "First squirrel hit plants one acorn with five percent chance and never rolls an early explosion");
        for (int i = 0; i < 30; i++) target.TakeDamage(1);
        target.TakeDamage(1, CombatDamageSource.Fire); target.TakeDamage(1, CombatDamageSource.Poison);
        target.TakeFrogDamage(1, 0, true); target.TakeFrogDamage(1, 0, false);
        Require(SquirrelAcorn.FindOn(target) == seed && !seed.IsDetonated,
            "Other projectiles, frog damage, fire and poison never roll an early explosion or accumulate hits");
        yield return Seconds(0.7f);
        var acornArt = seed.GetComponentInChildren<SpriteRenderer>();
        var body = target.GetComponent<EnemyHitReaction>().VisualRoot.GetComponentInChildren<SpriteRenderer>();
        float ratio = acornArt.bounds.size.y / body.bounds.size.y;
        Require(Vector2.Distance(seed.transform.position, body.transform.position) < 0.001f
                && ratio > 0.7f && ratio < 0.9f && acornArt.color.a >= 0.34f && acornArt.color.a <= 0.76f,
            "Acorn is centred on the enemy, approximately twenty percent smaller and translucent");
        Require(seed.GetComponentInChildren<TMPro.TextMeshPro>() == null, "Acorn has no obsolete insertion counter");
        Capture("Logs/SquirrelAcornPulse.png");
        Color pulseColor = acornArt.color; Vector3 pulseScale = acornArt.transform.localScale;
        yield return Seconds(0.14f);
        Require(Mathf.Abs(pulseColor.a - acornArt.color.a) > 0.01f
                && Vector3.Distance(pulseScale, acornArt.transform.localScale) > 0.01f,
            "Acorn visibly pulses its orange colour, opacity and size");
        Capture("Logs/SquirrelAcornPulseDim.png");

        target.TakeDamage(target.hp);
        Require(seed.IsDetonated && Player.HasActiveProjectiles,
            "Enemy death always detonates its acorn and holds the turn while the effect resolves");
        yield return Seconds(0.08f); Capture("Logs/SquirrelAcornExplosion.png");
        yield return WaitForAcornBlasts();
        Require(Mathf.Approximately(near.hp, 900) && Mathf.Approximately(outside.hp, 1000) && Mathf.Approximately(other.hp, 1000),
            "Death explosion deals 100 within a circular one-cell radius, confined to its room");
        Require(Object.FindObjectsOfType<TMPro.TextMeshProUGUI>().Any(label => label.text.Contains("ОГОНЬ -")),
            "Explosion uses the fire damage source and displays fire combat feedback");
        var bonfire = FireflyBonfire.LastSpawned;
        Require(bonfire != null && bonfire.Room == room && Mathf.Approximately(bonfire.DamagePerTurn, 10)
                && Mathf.Approximately(bonfire.DamageRadius, 1) && bonfire.RemainingTurns == 1,
            "Explosion leaves a weak ten-damage one-turn bonfire");
        Require(!Player.HasActiveProjectiles, "Explosion releases the turn after its bounded fire animation");
        FireflyBonfire.ProcessAllAfterEnemyTurn(100);
        Require(Mathf.Approximately(near.hp, 890) && Mathf.Approximately(outside.hp, 1000) && Mathf.Approximately(other.hp, 1000),
            "Weak bonfire deals ten fire damage to neighbours in its room");
        yield return Frames(2);
        Require(FireflyBonfire.ActiveCount == 0, "Weak bonfire expires after one damaging turn");
        yield return Clear();

        foreach (CombatDamageSource source in new[] { CombatDamageSource.Fire, CombatDamageSource.Poison })
        {
            target = Target(Vector2.zero, 10000); Set(target, "immuneToPoison", false);
            Hit(target); seed = SquirrelAcorn.FindOn(target);
            target.hp = 1; target.TakeAfterTurnDamage(1, source);
            Require(seed.IsDetonated, "Periodic " + source + " death detonates the acorn");
            yield return Clear();
        }
        target = Target(Vector2.zero, 50); near = Target(Vector2.right, 1000);
        Hit(target); yield return WaitForAcornBlasts();
        Require(Mathf.Approximately(near.hp, 900), "Lethal planting hit still causes the death explosion");
        yield return Clear();

        foreach (float chance in new[] { 0.05f, 0.10f, 0.15f, 0.20f })
        {
            target = Target(Vector2.zero, 10000);
            GameObject shot = MakeShot(chance);
            var effect = shot.GetComponent<SquirrelProjectile>();
            UnityEngine.Random.InitState(RollSeed(chance, true));
            effect.ApplyTo(target); seed = SquirrelAcorn.FindOn(target);
            Require(seed != null && !seed.IsDetonated && Mathf.Approximately(seed.EarlyExplosionChance, chance),
                "Planting never rolls, including a random value below " + chance * 100 + " percent");
            UnityEngine.Random.InitState(RollSeed(chance, false));
            effect.ApplyTo(target);
            Require(SquirrelAcorn.FindOn(target) == seed && !seed.IsDetonated
                    && Object.FindObjectsOfType<SquirrelAcorn>().Length == 1,
                "Failed repeat-squirrel roll keeps the same acorn: " + chance * 100 + " percent");
            UnityEngine.Random.InitState(RollSeed(chance, true));
            effect.ApplyTo(target);
            Require(seed.IsDetonated && target.hp == 10000,
                "Successful repeat-squirrel roll detonates before enemy death: " + chance * 100 + " percent");
            yield return WaitForAcornBlasts();
            Require(Mathf.Approximately(target.hp, 9900), "Premature explosion also deals 100 fire damage to its living host");
            Object.Destroy(shot);
            yield return Seconds(0.6f);
            Hit(target);
            Require(SquirrelAcorn.FindOn(target) != null && !SquirrelAcorn.FindOn(target).IsDetonated,
                "A later squirrel plants a fresh acorn after an early explosion");
            yield return Clear();
        }

        target = Target(Vector2.zero, 10000); near = Target(Vector2.right, 10000);
        Brick beyond = Target(Vector2.right * 2, 1000);
        Hit(target); Hit(near); near.hp = 50;
        target.TakeDamage(target.hp); yield return WaitForAcornBlasts();
        Require(Mathf.Approximately(beyond.hp, 900) && FireflyBonfire.ActiveCount == 2,
            "An explosion killing another acorn host triggers its death explosion exactly once");
        yield return Clear();

        target = Target(Vector2.zero, 10000); Hit(target, 0.05f, 3); seed = SquirrelAcorn.FindOn(target);
        Require(Mathf.Approximately(seed.BindingChance, 0.09f), "Third vine rank has nine percent chance");
        yield return Frames(2);
        seed.Detonate(); UnityEngine.Random.InitState(RollSeed(0.09f, true));
        yield return Frames(2);
        var stun = target.GetComponent<EnemyTurnStun>();
        var binding = target.GetComponentInChildren<SquirrelVineBinding>();
        Require(stun != null && stun.RemainingTurns == 1 && binding != null,
            "Successful independent explosion roll binds the surviving enemy for one turn");
        Sprite first = binding.GetComponentInChildren<SpriteRenderer>().sprite;
        yield return Seconds(0.7f);
        Require(binding.GetComponentInChildren<SpriteRenderer>().sprite != first, "Vines visibly animate around the enemy");
        Capture("Logs/SquirrelVineBinding.png");
        Vector3 before = target.transform.position; target.Step();
        Require(target.transform.position == before && !target.IsStepping && !stun.IsStunned,
            "Bound enemy skips exactly one movement turn");
        yield return Seconds(0.2f);
        Require(target.GetComponentInChildren<SquirrelVineBinding>() == null, "Vines fade away after the skipped turn");
        yield return Clear();

        var queue = new GameObject("Queue").AddComponent<AnimalProjectileQueue>(); queue.enabled = false;
        var entries = new List<AnimalProjectileQueue.AnimalProjectileEntry>();
        for (int i = 0; i < AnimalProjectileQueue.AnimalTypeCount; i++)
        {
            var entry = new AnimalProjectileQueue.AnimalProjectileEntry(); Set(entry, "prefab", shotPrefab); entries.Add(entry);
        }
        Set(queue, "animals", entries); queue.ReloadAll();
        Require(AnimalProjectileQueue.PrepareRunLoadout(new[] { 0, 1, 2, 6, 7 })
                && queue.ConfigureRunLoadout(new[] { 0, 1, 2, 6, 7 }) && !queue.IsAvailable(7),
            "Five-type loadout can include squirrel, which must be unlocked through battle upgrades");
        Require(queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.Ammo) && queue.Animals[7].AmmoPerTurn == 1,
            "Squirrel unlock starts with one projectile");
        for (int rank = 1; rank <= 3; rank++)
            Require(queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.SquirrelDetonation)
                && queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.SquirrelBinding)
                && Mathf.Approximately(queue.GetSquirrelEarlyExplosionChance(7), (rank + 1) * 0.05f) && queue.GetSquirrelBindingRank(7) == rank,
                "Early explosion 10/15/20 and binding 3/6/9 percent: rank " + rank);
        queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.Ammo); queue.Upgrade(7, AnimalProjectileQueue.UpgradeKind.Ammo);
        Require(queue.Animals[7].AmmoPerTurn == 3, "Squirrel count upgrades reach three projectiles");
        Require(Enumerable.Range(0, 8).Select(AnimalProjectileQueue.GetAllVolleyAnimalIndex).Distinct().Count() == 8
            && AnimalProjectileQueue.GetAllVolleyAnimalIndex(7) == 7 && AnimalProjectileQueue.GetAllVolleyAnimalIndex(8) == -1,
            "Combined volley includes squirrel and preserves frog immediately before firefly");

        // Run the actual launch coroutine as well as checking its type mapping.
        // The earlier queue fixture uses squirrel prefabs for every slot, so
        // set non-squirrel slots to the ordinary projectile for this check.
        for (int i = 0; i < entries.Count - 1; i++)
            Set(entries[i], "prefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/BaseBullet.prefab"));
        foreach (int i in new[] { 1, 2, 6 }) queue.Upgrade(i, AnimalProjectileQueue.UpgradeKind.Ammo);
        queue.ReloadAll();
        var shooter = player.AddComponent<Player>(); Set(shooter, "animalQueue", queue);
        var launches = new List<int>(); Player.VolleyStarted += launches.Add;
        var firing = (IEnumerator)typeof(Player).GetMethod("ShootAllQueueCoroutine",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shooter, new object[] { Vector2.up });
        shooter.StartCoroutine(firing);
        float launchDeadline = Time.time + 5f;
        do
        {
            foreach (Animal shot in Player.animalsCollect)
                if (shot != null) shot.GetComponent<Rigidbody2D>().simulated = false;
            yield return null;
        } while ((bool)typeof(Player).GetField("isShooting", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(shooter) && Time.time < launchDeadline);
        Player.VolleyStarted -= launches.Add;
        Require(launches.SequenceEqual(new[] { 6, 0, 2, 1, 7 }),
            "Actual combined volley launches squirrel and frog remains immediately before firefly");
        var squirrels = Player.animalsCollect.Where(shot => shot != null && shot.GetComponent<SquirrelProjectile>() != null).ToArray();
        Require(squirrels.Length == 3 && !queue.HasRemainingAmmo, "Actual combined volley launches three squirrels and consumes all ammunition");
        target = Target(Vector2.zero, 10000);
        target.ApplyProjectileHit(squirrels[0], target.transform.position, Vector2.up);
        Require(Mathf.Approximately(SquirrelAcorn.FindOn(target).EarlyExplosionChance, 0.20f) && Mathf.Approximately(SquirrelAcorn.FindOn(target).BindingChance, 0.09f),
            "Launched squirrels receive their current early-explosion and binding upgrades");
        foreach (Animal shot in Player.animalsCollect.ToArray()) if (shot != null) Object.Destroy(shot.gameObject);
        yield return Clear();
        Require(!Player.HasActiveProjectiles, "Actual combined volley releases the input after cleanup");

        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/UIscene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        yield return Frames(3);
        var dialog = Object.FindObjectOfType<ProjectileLoadoutUI>(true);
        Require(dialog != null && dialog.Show(selected => {}), "Eight authored choices open in the menu");
        var data = new SerializedObject(dialog); var choices = data.FindProperty("choices");
        Require(choices.arraySize == 8, "Squirrel has an authored eighth card");
        foreach (int i in new[] { 1, 2, 6, 7 })
            ((Button)choices.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue).onClick.Invoke();
        Require(((Button)data.FindProperty("startButton").objectReferenceValue).interactable,
            "Selecting squirrel alongside four other types enables Start");
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases(); Capture("Logs/SquirrelLoadout.png", 540, 960);
        dialog.Close(); Require(!dialog.IsOpen, "Loadout can still close without starting");
    }

    private static Brick Target(Vector2 position, float hp, LevelGenerator owner = null)
    {
        owner = owner != null ? owner : room;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/SwampDemon.prefab");
        var enemy = Object.Instantiate(prefab, position, Quaternion.identity, owner.transform).GetComponent<Brick>();
        enemy.levelGenerator = owner; enemy.hp = hp; owner.EnemyCollection.Add(enemy.gameObject); return enemy;
    }
    private static void Hit(Brick enemy, float chance = 0.05f, int vines = 0)
    {
        var shot = Object.Instantiate(shotPrefab); shot.GetComponent<Rigidbody2D>().simulated = false;
        shot.GetComponent<Animal>().enabled = false;
        shot.GetComponent<SquirrelProjectile>().ConfigureRunUpgrades(chance, vines, false);
        Require(enemy.ApplyProjectileHit(shot.GetComponent<Animal>(), enemy.transform.position, Vector2.up), "Actual squirrel hit resolved");
        Object.Destroy(shot);
    }

    private static GameObject MakeShot(float chance)
    {
        var shot = Object.Instantiate(shotPrefab); shot.GetComponent<Rigidbody2D>().simulated = false;
        shot.GetComponent<Animal>().enabled = false;
        shot.GetComponent<SquirrelProjectile>().ConfigureRunUpgrades(chance, 0, false);
        return shot;
    }
    private static int RollSeed(float chance, bool success)
    {
        for (int i = 0; i < 10000; i++)
        {
            UnityEngine.Random.InitState(i);
            if ((UnityEngine.Random.value < chance) == success) return i;
        }
        throw new Exception("No deterministic random sample found");
    }

    private static IEnumerator Clear()
    {
        foreach (SquirrelAcorn seed in Object.FindObjectsOfType<SquirrelAcorn>()) Object.Destroy(seed.gameObject);
        foreach (Brick enemy in Object.FindObjectsOfType<Brick>()) Object.Destroy(enemy.gameObject);
        foreach (FireflyBonfire fire in Object.FindObjectsOfType<FireflyBonfire>()) Object.Destroy(fire.gameObject);
        yield return Frames(3); room.EnemyCollection.Clear();
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator WaitForAcornBlasts()
    {
        float limit = Time.time + 3f;
        while (SquirrelAcorn.HasActiveBlasts && Time.time < limit) yield return null;
        Require(!SquirrelAcorn.HasActiveBlasts, "All three timed blast hits and chained acorns finish");
    }
    private static IEnumerator Seconds(float seconds) { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool success, string description)
    { if (!success) throw new Exception(description); Report.Add("PASS " + description); }
    private static void Capture(string path, int width = 640, int height = 640)
    {
        var render = new RenderTexture(width, height, 24); render.Create(); camera.targetTexture = render; camera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(image); render.Release(); Object.Destroy(render);
    }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Squirrel validation timed out");
            while (Steps.Count > 0)
            {
                var current = Steps.Peek(); if (!current.MoveNext()) { Steps.Pop(); continue; }
                if (current.Current is IEnumerator child) { Steps.Push(child); continue; } return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false); Time.captureDeltaTime = 0;
        string result = error == null ? "SQUIRREL_MECHANICS_PASSED" : "FAIL " + error;
        Report.Add(result); File.WriteAllLines("Logs/SquirrelValidation.txt", Report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
