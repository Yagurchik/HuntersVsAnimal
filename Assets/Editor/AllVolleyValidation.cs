using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AllVolleyValidation
{
    private const string Running = "ForestAnimalWar.AllVolleyValidation.Running";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> report = new List<string>();
    private static IEnumerator checks;
    private static double deadline;
    private static int lastFrame = -1;

    static AllVolleyValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            checks = CheckRuntime();
            deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Directory.CreateDirectory("Logs");
        report.Clear();
        SessionState.SetBool(Running, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(2);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckRuntime()
    {
        yield return null;
        yield return null;

        GameManager manager = GameManager.Instance;
        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        Player player = Object.FindObjectsOfType<Player>().First(value => value.isActiveAndEnabled);
        AnimalSelectionWheel wheel = Object.FindObjectOfType<AnimalSelectionWheel>(true);
        Check(manager != null && queue != null && player != null && wheel != null,
            "Gameplay services and editable projectile wheel are available");
        if (manager.IsChoosingUpgrade)
        {
            AnimalProjectileQueue.UpgradeOption startingChoice =
                queue.GetAvailableUpgradeOptions().First();
            manager.SelectProjectileUpgrade(startingChoice.AnimalIndex, startingChoice.Kind);
        }
        manager.enabled = false;
        manager.isPaused = false;
        manager.stepGame = false;
        manager.stepPlayer = false;

        int[] expectedTypeOrder = {
            AnimalProjectileQueue.FrogIndex,
            AnimalProjectileQueue.FireflyIndex,
            AnimalProjectileQueue.BoarIndex,
            AnimalProjectileQueue.RabbitIndex,
            AnimalProjectileQueue.HedgehogIndex,
            AnimalProjectileQueue.HoneyBadgerIndex,
            AnimalProjectileQueue.BeaverIndex,
            AnimalProjectileQueue.SquirrelIndex
        };
        Check(Enumerable.Range(0, expectedTypeOrder.Length)
                .Select(AnimalProjectileQueue.GetAllVolleyAnimalIndex)
                .SequenceEqual(expectedTypeOrder),
            "Combined volley uses frog, firefly, boar, rabbit, hedgehog, honey badger, beaver, squirrel order");

        for (int i = 0; i < queue.Animals.Count; i++) queue.Unlock(i);
        Check(queue.Upgrade(AnimalProjectileQueue.FireflyIndex,
                AnimalProjectileQueue.UpgradeKind.RepeatFire),
            "Firefly repeat volley is enabled for the integration check");
        queue.ReloadAll();

        foreach (Brick enemy in Object.FindObjectsOfType<Brick>())
            foreach (Collider2D collider in enemy.GetComponentsInChildren<Collider2D>())
                collider.enabled = false;

        Button toggle = wheel.AllVolleyButton;
        Check(toggle != null && toggle.transform.Find("Label")?.GetComponent<Text>() != null,
            "Combined-volley button is authored in the gameplay scene");
        toggle.onClick.Invoke();
        Check(queue.AllVolleyEnabled
                && toggle.transform.Find("Label").GetComponent<Text>().text.Contains("ВКЛ"),
            "Button enables combined-volley mode and updates its label");

        int expectedProjectiles = expectedTypeOrder.Sum(index =>
            queue.Animals[index].AmmoPerTurn * queue.GetManualVolleyCount(index));
        var started = new List<int>();
        Player.VolleyStarted += started.Add;
        try
        {
            MethodInfo shoot = typeof(Player).GetMethod("Shoot", Private);
            Check(shoot != null, "Player exposes the normal aimed-shot entry point");
            shoot.Invoke(player, new object[] { (Vector2)player.transform.position + Vector2.up * 5f });

            FieldInfo isShooting = typeof(Player).GetField("isShooting", Private);
            float timeout = Time.time + 8f;
            int maximumLiving = 0;
            while ((bool)isShooting.GetValue(player) && Time.time < timeout)
            {
                foreach (Animal projectile in Player.animalsCollect)
                {
                    if (projectile == null) continue;
                    Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
                    if (body != null) body.simulated = false;
                }
                maximumLiving = Mathf.Max(maximumLiving,
                    Player.animalsCollect.Count(projectile => projectile != null));
                yield return null;
            }

            maximumLiving = Mathf.Max(maximumLiving,
                Player.animalsCollect.Count(projectile => projectile != null));
            int[] expectedVolleyEvents = {
                AnimalProjectileQueue.FrogIndex,
                AnimalProjectileQueue.FireflyIndex,
                AnimalProjectileQueue.FireflyIndex,
                AnimalProjectileQueue.BoarIndex,
                AnimalProjectileQueue.RabbitIndex,
                AnimalProjectileQueue.HedgehogIndex,
                AnimalProjectileQueue.HoneyBadgerIndex,
                AnimalProjectileQueue.BeaverIndex,
                AnimalProjectileQueue.SquirrelIndex
            };
            Check(started.SequenceEqual(expectedVolleyEvents),
                "Repeat volleys finish before the next projectile type starts; actual: "
                + string.Join(",", started));
            Check(maximumLiving == expectedProjectiles,
                "One aimed action emits every projectile from every available volley");
            Check(expectedTypeOrder.All(index => queue.GetRemainingAmmo(index) == 0
                    && queue.GetRemainingVolleys(index) == 0),
                "Combined volley consumes the complete turn ammunition exactly once");
        }
        finally
        {
            Player.VolleyStarted -= started.Add;
        }

        foreach (Animal projectile in Player.animalsCollect.ToArray())
            if (projectile != null) Object.Destroy(projectile.gameObject);
        float cleanupTimeout = Time.time + 4f;
        while (Player.HasActiveProjectiles && Time.time < cleanupTimeout) yield return null;
        Check(!Player.HasActiveProjectiles, "Combined volley releases the input lock after cleanup");

        toggle.onClick.Invoke();
        Check(!queue.AllVolleyEnabled
                && toggle.transform.Find("Label").GetComponent<Text>().text.Contains("ВЫКЛ"),
            "The same button returns to ordinary single-type firing");
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
                throw new TimeoutException("Combined-volley validation timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        Time.timeScale = 1f;
        string result = error == null
            ? "PASS: " + report.Count + " combined-volley checks."
            : "FAIL: " + error;
        report.Add(result);
        File.WriteAllLines("Logs/AllVolleyValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
