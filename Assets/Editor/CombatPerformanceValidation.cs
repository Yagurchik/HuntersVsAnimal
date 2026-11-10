using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CombatPerformanceValidation
{
    [Serializable] private class Metric { public string Name; public double Milliseconds; public long Bytes; }
    [Serializable] private class Results { public Metric[] Metrics; }
    private const string Running = "CombatPerformanceValidation.Running";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static readonly List<Metric> Metrics = new List<Metric>();
    private static int lastFrame = -1;
    private static double deadline;
    private static bool allocationCounterAvailable;
    private static bool Baseline => SessionState.GetBool("CombatPerformanceValidation.Baseline", false);
    static CombatPerformanceValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Steps.Clear(); Steps.Push(Checks()); deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }
    public static void RunBaseline() => Start(true);
    public static void RunBatch() => Start(false);
    private static void Start(bool baseline)
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation") throw new Exception("Use isolated validation project.");
        Directory.CreateDirectory("Logs");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("CombatPerformanceValidation.Baseline", baseline);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    private static IEnumerator Checks()
    {
        long allocationProbe = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(new byte[2048]);
        allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() - allocationProbe >= 2048;
        new GameObject("Player").tag = "Player";
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -20);
        var room = new GameObject("Benchmark Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        var targets = new List<Brick>();
        for (int i = 0; i < 144; i++)
        {
            var value = new GameObject("Benchmark Enemy"); value.transform.SetParent(room.transform);
            value.transform.position = new Vector3(i % 12, i / 12, 0);
            var enemy = value.AddComponent<Brick>(); enemy.levelGenerator = room; enemy.hp = 100000;
            room.EnemyCollection.Add(value); targets.Add(enemy);
        }
        var riderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Frog.prefab").GetComponent<FrogProjectile>().RiderPrefab;
        var frogs = new List<FrogRider>();
        for (int i = 0; i < 64; i++)
        {
            var frog = Object.Instantiate(riderPrefab, room.transform); frog.Initialize(targets[i / 4], 0, 2, 3, false, 0); frogs.Add(frog);
        }
        yield return Frames(4);
        Require(Math.Abs(FrogRider.DamageMultiplier(room) - 2.92f) < 0.001f, "64 frogs preserve exact chorus multiplier");
        Measure("chorus_50000_queries", () => { for (int i = 0; i < 50000; i++) FrogRider.DamageMultiplier(room); });

        var neighbourMethod = typeof(ArtifactCombatEffects).GetMethod("Neighbours", BindingFlags.NonPublic | BindingFlags.Static);
        var query = (Func<Brick, float, bool, List<Brick>>)Delegate.CreateDelegate(typeof(Func<Brick, float, bool, List<Brick>>), neighbourMethod);
        var releaseMethod = typeof(ArtifactCombatEffects).GetMethod("ReleaseNeighbourBuffer", BindingFlags.Public | BindingFlags.Static);
        var release = releaseMethod != null ? (Action<List<Brick>>)Delegate.CreateDelegate(typeof(Action<List<Brick>>), releaseMethod) : null;
        var anchor = targets[65];
        Measure("neighbours_10000_queries", () => {
            for (int i = 0; i < 10000; i++) { var found = query(anchor, 2.25f, false); if (found.Count == 0) throw new Exception("No neighbours"); release?.Invoke(found); }
        });
        foreach (bool square in new[] { false, true })
        {
            var found = query(anchor, 2.25f, square);
            var reference = targets.Where(t => t != anchor && !t.IsDefeated && t.hp > 0 &&
                (square ? Mathf.Max(Mathf.Abs(t.transform.position.x - anchor.transform.position.x), Mathf.Abs(t.transform.position.y - anchor.transform.position.y)) <= 2.251f
                : Vector2.SqrMagnitude((Vector2)(t.transform.position - anchor.transform.position)) <= 2.25f * 2.25f + 0.001f)).ToArray();
            Require(found.Count == reference.Length && reference.All(found.Contains), "Neighbour query matches exhaustive reference: square=" + square);
            release?.Invoke(found);
        }
        if (!Baseline)
        {
            // Exercise within-frame motion and collection replacement, where stale caches are dangerous.
            targets[0].transform.position = anchor.transform.position + Vector3.right * 0.5f;
            room.NotifyEnemyStateChanged();
            var found = query(anchor, 1f, false); Require(found.Contains(targets[0]), "Movement invalidation updates neighbours in the same frame"); release(found);
            var previousRoom = frogs[0].Host.levelGenerator;
            var otherRoom = new GameObject("Transferred Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
            frogs[0].Host.levelGenerator = otherRoom;
            FrogRider.UpgradeAll(2, 3, false);
            Require(Math.Abs(FrogRider.DamageMultiplier(room) - 2.8f) < 0.001f && Math.Abs(FrogRider.DamageMultiplier(otherRoom) - 1.12f) < 0.001f, "Transferred host changes both room chorus totals");
            frogs[0].Host.levelGenerator = previousRoom; FrogRider.UpgradeAll(2, 3, false);
            Object.Destroy(frogs[0].gameObject); yield return Frames(1);
            Require(Math.Abs(FrogRider.DamageMultiplier(room) - 2.89f) < 0.001f, "Destroyed frog invalidates cached chorus");
        }
        var bar = Object.Instantiate(Resources.Load<EnemyHealthBarView>("UI/EnemyHealthBars/RegularEnemyHealthBar"));
        bar.SetValue("100", 1f); yield return Frames(2);
        var updateBar = (Action)Delegate.CreateDelegate(typeof(Action), bar, typeof(EnemyHealthBarView).GetMethod("Update", Private));
        Measure("settled_health_bar_20000_updates", () => { for (int i = 0; i < 20000; i++) updateBar(); });
        if (!Baseline)
        {
            var animating = typeof(EnemyHealthBarView).GetField("animating", Private);
            Require(!(bool)animating.GetValue(bar), "Settled HP bar is idle");
            bar.SetValue("50", 0.5f); Require((bool)animating.GetValue(bar), "Damage starts HP animation");
            for (int i = 0; i < 2000; i++) updateBar();
            Require(!(bool)animating.GetValue(bar) && Math.Abs((float)typeof(EnemyHealthBarView).GetField("displayedFraction", Private).GetValue(bar) - 0.5f) < 0.0001f, "Damage animation reaches exact HP and stops");
            bar.SetValue("175 / 100", 1f); for (int i = 0; i < 2000; i++) updateBar();
            Require(!(bool)animating.GetValue(bar) && bar.GetComponentInChildren<TextMeshProUGUI>().text == "175 / 100", "Healing and excess shield HP preserve numbers and stop animation");
            yield return CheckDamageAndLighting(room, anchor);
        }

        // Deterministic moving-light mesh workload; all generated vertex alphas must remain identical.
        var graphicObject = new GameObject("Light Mesh", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoomDarknessGraphic));
        var graphic = graphicObject.GetComponent<RoomDarknessGraphic>(); graphic.rectTransform.sizeDelta = new Vector2(8, 10);
        var populate = (Action<VertexHelper>)Delegate.CreateDelegate(typeof(Action<VertexHelper>), graphic, typeof(RoomDarknessGraphic).GetMethod("OnPopulateMesh", Private, null, new[] { typeof(VertexHelper) }, null));
        var lights = new List<RoomDarknessGraphic.LightDisc>();
        using (var mesh = new VertexHelper())
        {
            Measure("moving_light_mesh_80_updates", () => {
                for (int frame = 0; frame < 80; frame++)
                {
                    lights.Clear(); for (int i = 0; i < 12; i++) lights.Add(new RoomDarknessGraphic.LightDisc(new Vector2(i % 4 - 1.5f + frame * 0.01f, i / 4 - 1), 1f, 0.2f));
                    graphic.Configure(0.3f, 0.5f, new Color(0.025f, 0.035f, 0.065f, 0.55f), lights);
                    populate(mesh);
                }
            });
            UIVertex vertex = default;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                if (Math.Abs(vertex.color.a / 255f - graphic.GetAlphaAt(vertex.position)) > 1.1f / 255f)
                    throw new Exception("Darkness vertex differs from exact alpha: " + i);
            }
            Require(mesh.currentVertCount > 0, "All " + mesh.currentVertCount + " darkness vertices match original visibility alpha");
        }
        var sawdustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/BeaverSawdust.prefab").GetComponent<BeaverSawdustParticle>();
        var spawnMethod = typeof(BeaverSawdustParticle).GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
        var spawn = spawnMethod != null ? (Func<BeaverSawdustParticle, Vector2, Vector2, float, BeaverSawdustParticle>)Delegate.CreateDelegate(typeof(Func<BeaverSawdustParticle, Vector2, Vector2, float, BeaverSawdustParticle>), spawnMethod) : null;
        var age = typeof(BeaverSawdustParticle).GetField("age", Private);
        var elapsed = typeof(ArtifactLightningEffect).GetField("elapsed", Private);
        var sawdustUpdate = typeof(BeaverSawdustParticle).GetMethod("Update", Private);
        var lightningUpdate = typeof(ArtifactLightningEffect).GetMethod("Update", Private);
        double spawnTime = 0; long spawnBytes = 0; var instances = new HashSet<int>();
        var lightningInstances = new HashSet<int>();
        for (int cycle = 0; cycle < 24; cycle++)
        {
            var burst = new List<BeaverSawdustParticle>();
            long before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            for (int i = 0; i < 16; i++)
            {
                var particle = spawn != null ? spawn(sawdustPrefab, Vector2.zero, Vector2.up, 3)
                    : Object.Instantiate(sawdustPrefab);
                if (spawn == null) particle.Launch(Vector2.zero, Vector2.up, 3);
                burst.Add(particle);
                ArtifactLightningEffect.Flash(Vector3.zero, Vector3.one, i % 2 == 0);
            }
            watch.Stop(); if (cycle > 0) { spawnTime += watch.Elapsed.TotalMilliseconds; spawnBytes += GC.GetAllocatedBytesForCurrentThread() - before; }
            foreach (var particle in burst) { instances.Add(particle.GetInstanceID());
                if (!Baseline)
                {
                    var sprite = particle.GetComponent<SpriteRenderer>();
                    Require(Math.Abs((float)age.GetValue(particle)) < 0.0001f && sprite.color.a == sawdustPrefab.GetComponent<SpriteRenderer>().color.a
                        && particle.transform.localScale.x >= sawdustPrefab.transform.localScale.x * 0.75f
                        && particle.transform.localScale.x <= sawdustPrefab.transform.localScale.x * 1.2f,
                        "Sawdust launch resets age, opacity and scale");
                }
age.SetValue(particle, 1f); sawdustUpdate.Invoke(particle, null); }
            foreach (var effect in Object.FindObjectsOfType<ArtifactLightningEffect>()) { lightningInstances.Add(effect.GetInstanceID()); elapsed.SetValue(effect, 1f); lightningUpdate.Invoke(effect, null); }
            yield return Frames(1);
        }
        Metrics.Add(new Metric { Name = "effects_368_sawdust_and_lightning", Milliseconds = spawnTime, Bytes = allocationCounterAvailable ? spawnBytes : -1 });
        Report.Add("Sawdust instances for 384 emissions: " + instances.Count);
        Report.Add("Lightning instances for 384 flashes: " + lightningInstances.Count);
        if (!Baseline) Require(instances.Count == 16, "Sawdust reuses the same 16 objects across 24 bursts");
        if (!Baseline)
        {
            Require(lightningInstances.Count == 16, "Lightning reuses the same 16 objects across 24 bursts");
            ArtifactLightningEffect.Flash(Vector3.zero, Vector3.one, true);
            var effect = Object.FindObjectOfType<ArtifactLightningEffect>(); int identifier = effect.GetInstanceID();
            elapsed.SetValue(effect, 1f); lightningUpdate.Invoke(effect, null);
            ArtifactLightningEffect.Flash(Vector3.left, Vector3.up, false);
            effect = Object.FindObjectOfType<ArtifactLightningEffect>(); var beam = effect.GetComponent<LineRenderer>();
            Require(effect.GetInstanceID() == identifier, "Normal lightning reuses the preceding arc object");
            Require(Math.Abs((float)typeof(ArtifactLightningEffect).GetField("duration", Private).GetValue(effect) - 0.18f) < 0.0001f, "Normal lightning resets the arc's shortened duration");
            Report.Add("Reused lightning color: " + beam.startColor + "; endpoints: " + beam.GetPosition(0) + " / " + beam.GetPosition(beam.positionCount - 1));
            Require(Math.Abs(beam.startColor.r - 0.45f) <= 1f / 255f && Math.Abs(beam.startColor.g - 0.87f) <= 1f / 255f
                && beam.startColor.b == 1f && beam.startColor.a == 1f && beam.GetPosition(0) == Vector3.left
                && beam.GetPosition(beam.positionCount - 1) == Vector3.up, "Reused lightning resets color and endpoints within renderer color precision");
        }
        foreach (var metric in Metrics) Report.Add(metric.Name + ": " + metric.Milliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " ms"
            + (metric.Bytes >= 0 ? ", " + metric.Bytes + " managed bytes" : "; allocation counter unavailable"));
        var baselinePath = "Logs/CombatPerformanceBaseline.json";
        if (!Baseline && File.Exists(baselinePath))
        {
            var previous = JsonUtility.FromJson<Results>(File.ReadAllText(baselinePath));
            foreach (var metric in Metrics)
            {
                var old = previous.Metrics.First(m => m.Name == metric.Name);
                Report.Add("COMPARE " + metric.Name + ": " + old.Milliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " -> " + metric.Milliseconds.ToString("0.###", CultureInfo.InvariantCulture)
                    + " ms");
            }
        }
        File.WriteAllText(Baseline ? baselinePath : "Logs/CombatPerformanceOptimized.json", JsonUtility.ToJson(new Results { Metrics = Metrics.ToArray() }, true));
    }
    private static IEnumerator CheckDamageAndLighting(LevelGenerator room, Brick anchor)
    {
        var ui = EnemyCombatTextUI.Ensure(); Canvas.ForceUpdateCanvases();
        var release = typeof(EnemyCombatTextUI).GetMethod("ReleaseDamageAt", Private);
        while (ui.ActiveDamageNumberCount > 0) release.Invoke(ui, new object[] { 0 });
        var entries = (IList)typeof(EnemyCombatTextUI).GetField("damageEntries", Private).GetValue(ui);
        ui.ShowDamage(anchor, 5f, CombatDamageSource.Beaver); ui.ShowDamage(anchor, 5f, CombatDamageSource.Beaver);
        Require(ui.ActiveDamageNumberCount == 1, "Two rapid beaver bites share one damage number");
        var entryType = entries[0].GetType();
        var labelField = entryType.GetField("Label");
        Require(((TextMeshProUGUI)labelField.GetValue(entries[0])).text == "-10", "Grouped number preserves both bites' total damage");
        ui.ShowDamage(anchor, 7f, CombatDamageSource.Poison);
        Require(ui.ActiveDamageNumberCount == 2 && ((TextMeshProUGUI)labelField.GetValue(entries[1])).text == "ЯД -7", "Different sources keep separate numbers and poison prefix");
        entryType.GetField("Elapsed").SetValue(entries[0], 0.13f);
        ui.ShowDamage(anchor, 3f, CombatDamageSource.Beaver);
        Require(ui.ActiveDamageNumberCount == 3, "Sustained bites start a fresh number after the short grouping window");
        release.Invoke(ui, new object[] { 0 }); ui.ShowDamage(anchor, 2f, CombatDamageSource.Beaver);
        Require(ui.ActiveDamageNumberCount == 2 && ((TextMeshProUGUI)labelField.GetValue(entries[1])).text == "-5", "Expiring an older number retains the newer grouping entry");
        while (ui.ActiveDamageNumberCount > 0) release.Invoke(ui, new object[] { 0 });
        Require(((IDictionary)typeof(EnemyCombatTextUI).GetField("recentDamage", Private).GetValue(ui)).Count == 0, "Damage number cleanup releases grouping references");

        var darkness = room.GetComponent<RoomDarkness>();
        if (darkness == null) darkness = room.gameObject.AddComponent<RoomDarkness>();
        typeof(RoomDarkness).GetField("useLineLighting", Private).SetValue(darkness, false);
        typeof(RoomDarkness).GetField("litFraction", Private).SetValue(darkness, 0.3f);
        var illumination = anchor.GetComponent<EnemyIllumination>();
        var dirty = typeof(RoomDarkness).GetField("needsRefresh", Private);
        var tickLight = typeof(EnemyIllumination).GetMethod("LateUpdate", Private);
        illumination.Illuminate(1f); darkness.Refresh();
        Require(darkness.ActiveLightCount == 1 && darkness.IsVisible(anchor.transform.position), "Illuminated enemy is visible inside darkness");
        tickLight.Invoke(illumination, null); illumination.Illuminate(1f);
        Require(!(bool)dirty.GetValue(darkness), "Repeated unchanged illumination does not rebuild the mesh");
        anchor.transform.position += Vector3.right * 0.1f; tickLight.Invoke(illumination, null);
        Require((bool)dirty.GetValue(darkness), "Moving light marks room shading dirty before the room update");
        darkness.Refresh(); illumination.Illuminate(2f);
        Require((bool)dirty.GetValue(darkness), "Increased light radius refreshes room shading");
        darkness.Refresh(); illumination.Extinguish();
        Require((bool)dirty.GetValue(darkness) && darkness.ActiveLightCount == 0, "Extinguished light is removed from visibility and shading");
        darkness.Refresh(); Object.Destroy(darkness); yield return Frames(1);
    }

    private static void Measure(string name, Action action)
    {
        action(); var samples = new List<Metric>();
        for (int pass = 0; pass < 3; pass++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew(); action(); watch.Stop();
            samples.Add(new Metric { Name = name, Milliseconds = watch.Elapsed.TotalMilliseconds, Bytes = allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() - before : -1 });
        }
        Metrics.Add(samples.OrderBy(m => m.Milliseconds).ElementAt(1));
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static void Require(bool okay, string message) { if (!okay) throw new Exception(message); Report.Add("PASS " + message); }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Performance validation timed out");
            while (Steps.Count > 0) { var step = Steps.Peek(); if (!step.MoveNext()) { Steps.Pop(); continue; } if (step.Current is IEnumerator child) { Steps.Push(child); continue; } return; }
            Finish(null);
        } catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false); Time.timeScale = 1;
        Report.Add(error == null ? "COMBAT_PERFORMANCE_PASSED" : "FAIL " + error);
        File.WriteAllLines(Baseline ? "Logs/CombatPerformanceBaseline.txt" : "Logs/CombatPerformanceValidation.txt", Report);
        if (error != null) UnityEngine.Debug.LogException(error); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
