using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ProjectileComboValidation
{
    private const string Running = "ForestAnimalWar.ComboValidation.Running";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly string[] Keys = {
        "ForestAnimalWar.Meta.Gold", "ForestAnimalWar.Meta.Acorns", "ForestAnimalWar.Meta.Victories",
        "ForestAnimalWar.CurrentLevel", "ForestAnimalWar.HighestUnlockedLevel",
        "ForestAnimalWar.MenuSelectedLevel", "ForestAnimalWar.RequestedLevel"
    };
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static readonly List<string> report = new List<string>();
    private static double deadline;
    private static int lastFrame = -1;

    static ProjectileComboValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 100;
            stack.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        Directory.CreateDirectory("Logs");
        SessionState.SetString(Running + ".Prefs", string.Join("|", Keys.Select(key =>
            PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key).ToString() : "-")));
        SessionState.SetBool(Running, true);
        LevelProgression.PrepareLevel(1);
        EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        manager.enabled = false;
        manager.stepPlayer = manager.stepGame = false;
        Player player = Object.FindObjectsOfType<Player>().First(p => p.isActiveAndEnabled);
        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        ProjectileComboUI ui = ProjectileComboUI.Instance;
        Check(ui != null && Object.FindObjectsOfType<ProjectileComboUI>().Length == 1,
            "The gameplay scene already contains one editable combo HUD");
        Check(!ui.IsCounting && ui.VisibleAlpha == 0f, "Combo starts hidden before the first volley");
        Check(ui.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget)
            && ui.GetComponent<GraphicRaycaster>() == null
            && !ui.GetComponentInChildren<CanvasGroup>().blocksRaycasts,
            "Combo cannot intercept aiming, shooting or UI clicks");
        Check(ui.GetComponentsInChildren<TMP_Text>().All(t => t.font == GameTypography.TmpFont),
            "The authored combo uses the game's Cyrillic fairytale font");
        Check(ui.GetComponent<Canvas>().sortingOrder < 240, "Combo is below map, upgrade and pause layers");
        var settings = new SerializedObject(ui);
        settings.FindProperty("resultHold").floatValue = 0.25f;
        settings.FindProperty("fadeDuration").floatValue = 0.15f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Brick target = manager.rooms[0].EnemyCollection[0].GetComponent<Brick>();
        foreach (Brick enemy in Object.FindObjectsOfType<Brick>())
            foreach (Collider2D collider in enemy.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        target.hp = 100000f;
        target.transform.position = player.transform.position + Vector3.up * 1.8f;
        target.GetComponent<Collider2D>().enabled = true;
        Physics2D.SyncTransforms();
        typeof(Player).GetMethod("Shoot", Private).Invoke(player, new object[] { (Vector2)target.transform.position });
        Check(ui.IsCounting && ui.HitCount == 0 && ui.VisibleAlpha > 0.99f,
            "The actual Shoot method shows zero combo immediately, before the first impact");
        float timeout = Time.time + 1f;
        while (ui.HitCount == 0 && Time.time < timeout) yield return null;
        Check(ui.HitCount > 0, "A real projectile collision increments the combo");
        Check(Player.animalsCollect.All(a => a == null || a.ComboVolleyId == ui.VolleyId),
            "All ammunition emitted by the volley shares its combo id");
        player.DropDownBalls();
        while (Player.HasActiveProjectiles) yield return null;
        yield return null;
        Check(!ui.IsCounting && ui.VisibleAlpha > 0f, "Recall finishes the combo and briefly retains its result");
        yield return Wait(0.5f);
        Check(ui.VisibleAlpha == 0f, "The completed result fades without blocking the next action");

        // Controlled damage tests keep one detached effect alive across frames.
        int id = ui.BeginVolley();
        ui.BeginEffect(id);
        target.TakeProjectileDamage(1f, id);
        target.TakeProjectileDamage(1f, id);
        Check(ui.HitCount == 2, "Repeated damaging hits on the same enemy both count");
        target.TakeDamage(5f);
        target.TakeProjectileDamage(0f, id);
        target.TakeProjectileDamage(1f, id - 1);
        Check(ui.HitCount == 2, "Periodic damage, zero damage and unrelated volleys do not inflate combo");
        int beforePulse = ui.HitCount;
        target.GetComponent<Collider2D>().enabled = true;
        HedgehogExplosionPulse pulse = HedgehogExplosionPulse.Create(null, target.transform.position, 1f, 0.7f, 0.12f, id);
        Check(ui.PendingEffects == 2, "Detached quills keep their source combo open");
        while (pulse.gameObject.activeSelf) yield return null;
        Check(ui.HitCount == beforePulse + 1 && ui.PendingEffects == 1,
            "A quill burst counts each enemy once and releases its pending effect");
        target.hp = 1f;
        target.TakeProjectileDamage(2f, id);
        int lethalCount = ui.HitCount;
        target.TakeProjectileDamage(2f, id);
        Check(ui.HitCount == lethalCount && lethalCount == beforePulse + 2,
            "A lethal hit counts once; hits on an already defeated enemy do not count");
        yield return null;
        manager.stepGame = false;

        // A real rabbit collision produces a replica, which must retain the same id.
        LevelGenerator room = manager.rooms[0];
        Brick rabbitTarget = Object.Instantiate(room.brickPrefab,
            player.transform.position + Vector3.up * 2f, Quaternion.identity).GetComponent<Brick>();
        rabbitTarget.hp = 100000f;
        GameObject rabbitPrefab = queue.Animals[2].Prefab;
        Animal rabbit = Object.Instantiate(rabbitPrefab, player.transform.position, Quaternion.identity).GetComponent<Animal>();
        rabbit.SetReturnTarget(player.gameObject);
        rabbit.SetComboVolley(id);
        RabbitReplication replication = rabbit.GetComponent<RabbitReplication>();
        if (replication == null) replication = rabbit.gameObject.AddComponent<RabbitReplication>();
        replication.Configure(rabbitPrefab, 1, 18f);
        ProjectilePhysics2D physics = rabbit.GetComponent<ProjectilePhysics2D>();
        if (physics == null) physics = rabbit.gameObject.AddComponent<ProjectilePhysics2D>();
        Physics2D.SyncTransforms();
        physics.Launch(Vector2.up, 18f);
        timeout = Time.time + 1f;
        while (Object.FindObjectsOfType<RabbitReplication>().Length < 2 && Time.time < timeout) yield return null;
        RabbitReplication[] rabbits = Object.FindObjectsOfType<RabbitReplication>();
        Check(rabbits.Length >= 2 && rabbits.All(r => r.GetComponent<Animal>().ComboVolleyId == id),
            "A physically spawned rabbit replica inherits the original volley's combo");
        foreach (Animal animal in Player.animalsCollect.ToArray())
            if (animal != null) { animal.SetReturnTarget(null); Object.Destroy(animal.gameObject); }
        Object.Destroy(rabbitTarget.gameObject);
        yield return null;
        ui.EndEffect(id);
        while (Player.HasActiveProjectiles) yield return null;
        yield return null;
        Check(!ui.IsCounting, "The counter closes after the last projectile and effect resolve");

        int nextId = ui.BeginVolley();
        ui.BeginEffect(nextId);
        ui.RegisterHit(id);
        ui.EndEffect(id);
        Check(nextId != id && ui.HitCount == 0 && ui.PendingEffects == 1,
            "A new volley resets combo and ignores late callbacks from the previous volley");
        for (int i = 0; i < 24; i++) ui.RegisterHit(nextId);
        manager.isPaused = true;
        Time.timeScale = 0f;
        yield return null;
        Check(ui.VisibleAlpha == 0f && ui.HitCount == 24, "Pause hides combo without losing its hits");
        yield return Wait(0.2f);
        manager.isPaused = false;
        Time.timeScale = 1f;
        yield return null;
        Check(ui.VisibleAlpha > 0.99f && ui.HitCount == 24, "Combo reappears correctly after pause");
        var capture = (IEnumerator)typeof(UIAuthoringValidation).GetMethod("CaptureImage",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "Logs/ProjectileCombo.png" });
        yield return capture;
        ui.EndEffect(nextId);
        yield return Wait(0.6f);
        Check(ui.VisibleAlpha == 0f && !ui.IsCounting, "Final combo fades after its effects finish");
    }

    private static IEnumerator Wait(float seconds)
    {
        double until = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < until) yield return null;
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        report.Add("PASS " + description);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Combo validation timed out");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        Time.timeScale = 1f;
        string[] saved = SessionState.GetString(Running + ".Prefs", "").Split('|');
        for (int i = 0; i < Keys.Length && i < saved.Length; i++)
            if (saved[i] == "-") PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetInt(Keys[i], int.Parse(saved[i]));
        PlayerPrefs.Save();
        string result = error == null ? "PASS: " + report.Count + " combo integration checks." : "FAIL: " + error;
        report.Add(result);
        File.WriteAllLines("Logs/ProjectileComboValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
