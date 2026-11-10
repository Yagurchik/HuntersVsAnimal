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
public static class FireflyRepeatTintValidation
{
    private const string Running = "ForestAnimalWar.FireflyRepeatValidation.Running";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] Keys = {
        "ForestAnimalWar.Meta.Gold", "ForestAnimalWar.Meta.Acorns", "ForestAnimalWar.Meta.Victories",
        "ForestAnimalWar.CurrentLevel", "ForestAnimalWar.HighestUnlockedLevel",
        "ForestAnimalWar.MenuSelectedLevel", "ForestAnimalWar.RequestedLevel"
    };
    private static readonly List<string> report = new List<string>();
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;
    private static bool roomSwitchVerified;

    static FireflyRepeatTintValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 110;
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
        roomSwitchVerified = false;
        manager.enabled = false;
        manager.stepGame = manager.stepPlayer = false;
        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        LevelGenerator room = manager.rooms[0];
        Brick enemy = room.EnemyCollection[0].GetComponent<Brick>();
        enemy.hp = 100000f;
        EnemyBurnTint tint = enemy.GetComponent<EnemyBurnTint>();
        SpriteRenderer renderer = enemy.GetComponentInChildren<SpriteRenderer>();
        Color baseColor = renderer.color;
        FireflyImpactEffect firefly = queue.Animals[0].Prefab.GetComponent<FireflyImpactEffect>();
        Animal fireflyAnimal = queue.Animals[0].Prefab.GetComponent<Animal>();
        Check(tint != null && AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Brick.prefab")
                .GetComponent<EnemyBurnTint>() != null
            && Resources.Load<GameObject>("Enemies/LichBoss").GetComponent<EnemyBurnTint>() != null,
            "Skeleton and Lich prefabs expose editable burn tint settings");
        Check(tint.DisplayedStacks == 0 && Approximately(renderer.color, baseColor),
            "An unburned enemy keeps its authored colors");
        Color previous = renderer.color;
        float previousOrange = OrangeWeight(previous);
        for (int stack = 1; stack <= 3; stack++)
        {
            GameObject projectile = Object.Instantiate(queue.Animals[0].Prefab);
            Animal animal = projectile.GetComponent<Animal>();
            animal.SetRuntimeDamage(fireflyAnimal.dmg);
            projectile.GetComponent<FireflyImpactEffect>().ApplyTo(enemy);
            Check(tint.DisplayedStacks == stack && OrangeWeight(renderer.color) > previousOrange,
                "Burn stack " + stack + " makes the enemy visibly more orange");
            previous = renderer.color;
            previousOrange = OrangeWeight(previous);
            animal.SetReturnTarget(null);
            Object.Destroy(projectile);
            yield return null;
        }
        GameObject cappedProjectile = Object.Instantiate(queue.Animals[0].Prefab);
        cappedProjectile.GetComponent<FireflyImpactEffect>().ApplyTo(enemy);
        Check(tint.DisplayedStacks == 3 && Approximately(renderer.color, previous),
            "Burn tint caps with the existing maximum of three stacks");
        cappedProjectile.GetComponent<Animal>().SetReturnTarget(null);
        Object.Destroy(cappedProjectile);
        yield return null;
        manager.Front.FocusRoom(room.RoomId, true);
        while (manager.Front.IsCameraTransitioning) yield return null;
        IEnumerator preview = (IEnumerator)typeof(UIAuthoringValidation).GetMethod("CaptureImage",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { "Logs/FireflyBurnTint.png" });
        yield return preview;
        EnemyDarknessVisibility visibility = enemy.GetComponent<EnemyDarknessVisibility>();
        var darkness = new SerializedObject(room.Darkness);
        darkness.FindProperty("useLineLighting").boolValue = false;
        darkness.FindProperty("litFraction").floatValue = 0f;
        darkness.ApplyModifiedPropertiesWithoutUndo();
        // Firefly illumination normally and intentionally reveals this enemy.
        // Disable only that light to isolate the tint from visibility behavior.
        enemy.GetComponent<EnemyIllumination>().enabled = false;
        room.Darkness.Refresh();
        visibility.RefreshVisibility();
        yield return null;
        Check(visibility.IsHidden && renderer.forceRenderingOff && tint.DisplayedStacks == 3,
            "Orange tint does not reveal an enemy through darkness");
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(manager.TurnNumber + 20);
        yield return null;
        Check(tint.DisplayedStacks == 0 && Approximately(renderer.color, baseColor),
            "Enemy returns to its exact authored colors when burn expires");

        Check(queue.GetManualVolleyCount(0) == 1
            && Mathf.Approximately(queue.GetManualVolleyDamageMultiplier(0), 1f),
            "Base firefly has one manually aimed full-damage volley");
        Check(queue.GetAvailableUpgradeOptions().Any(o => o.Kind == AnimalProjectileQueue.UpgradeKind.RepeatFire
            && o.Title.Contains("повторный залп") && o.Description.Contains("2 ручных залпа по 50%")),
            "Upgrade card explains that repeat volleys are launched manually");
        yield return VerifyVolleys(manager, queue, 0, 1, 1f);
        Check(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire), "First repeat upgrade can be selected");
        yield return VerifyVolleys(manager, queue, 0, 2, 0.5f);
        Check(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire), "Second repeat upgrade can be selected");
        yield return VerifyVolleys(manager, queue, 0, 3, 0.33f);
        Check(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire), "Third repeat upgrade can be selected");
        yield return VerifyVolleys(manager, queue, 0, 3, 0.5f);
        Check(!queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire)
            && queue.GetAvailableUpgradeOptions().All(o => !o.Title.Contains("траект")),
            "Repeat-fire branch ends at the fourth total state and trajectory is gone");
    }

    private static IEnumerator VerifyVolleys(GameManager manager, AnimalProjectileQueue queue, int index,
        int volleys, float damageMultiplier)
    {
        // Every invocation represents a fresh player turn in this focused test.
        manager.stepGame = false;
        manager.stepPlayer = false;
        foreach (Brick brick in Object.FindObjectsOfType<Brick>())
            foreach (Collider2D collider in brick.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        foreach (Animal old in Player.animalsCollect.ToArray())
            if (old != null) { old.SetReturnTarget(null); Object.Destroy(old.gameObject); }
        yield return null;
        queue.ReloadAll();
        int ammunition = queue.GetRemainingAmmo(index);
        Check(queue.GetRemainingVolleys(index) == volleys,
            "The turn starts with " + volleys + " manual firefly volley opportunity/opportunities");
        float expectedDamage = queue.Animals[index].Prefab.GetComponent<Animal>().dmg
            * damageMultiplier;

        for (int volleyNumber = 0; volleyNumber < volleys; volleyNumber++)
        {
            Player player = Object.FindObjectsOfType<Player>().First(component => component.isActiveAndEnabled);
            IEnumerator volley = (IEnumerator)typeof(Player).GetMethod("ShootQueueCoroutine", Private)
                .Invoke(player, new object[] { Vector2.up, index });
            player.StartCoroutine(volley);
            int maximumLiving = 0;
            float timeout = Time.time + 2f;
            while ((bool)typeof(Player).GetField("isShooting", Private).GetValue(player) && Time.time < timeout)
            {
                maximumLiving = Mathf.Max(maximumLiving, Player.animalsCollect.Count(a => a != null));
                yield return null;
            }
            Animal[] shots = Player.animalsCollect.Where(a => a != null).ToArray();
            maximumLiving = Mathf.Max(maximumLiving, shots.Length);
            Check(maximumLiving == ammunition,
                "Manual volley " + (volleyNumber + 1) + "/" + volleys
                + " emits exactly " + ammunition + " fireflies without automatic duplicates");
            Check(shots.All(a => Mathf.Abs(a.dmg - expectedDamage) < 0.01f),
                "Every firefly in manual volley " + (volleyNumber + 1) + " uses the configured "
                + Mathf.RoundToInt(damageMultiplier * 100f) + "% damage");
            Check(queue.GetRemainingAmmo(index) == 0
                && queue.GetRemainingVolleys(index) == volleys - volleyNumber,
                "The next volley is withheld while launched projectiles still exist");

            foreach (Animal shot in shots) Object.Destroy(shot.gameObject);
            timeout = Time.time + 3f;
            while (Player.HasActiveProjectiles && Time.time < timeout) yield return null;

            int remaining = volleys - volleyNumber - 1;
            Check(queue.GetRemainingVolleys(index) == remaining
                && queue.GetRemainingAmmo(index) == (remaining > 0 ? ammunition : 0),
                remaining > 0
                    ? "Fireflies reload only after the preceding volley disappears"
                    : "The final firefly volley is fully spent");
            if (remaining > 0)
            {
                Check(manager.CanSwitchRooms,
                    "Room switching is unlocked between manually aimed firefly volleys"
                    + " [ended=" + manager.GameEnded
                    + ", resolving=" + manager.IsResolvingEnemyTurn
                    + ", activeProjectiles=" + Player.HasActiveProjectiles
                    + ", upgrade=" + manager.IsChoosingUpgrade
                    + ", stepGame=" + manager.stepGame
                    + ", paused=" + manager.isPaused
                    + ", camera=" + manager.Front.IsCameraTransitioning + "]");
                if (!roomSwitchVerified)
                {
                    if (manager.Front.Model.OpenedLine < 2) manager.Front.ActivateLine(2);
                    int roomBefore = manager.Front.CurrentRoomId;
                    DefenseFront.Room destination = manager.Front.Model.Rooms.FirstOrDefault(candidate =>
                        candidate.CanSelect && candidate.Id != roomBefore);
                    bool switched = destination != null && manager.Front.FocusRoom(destination.Id);
                    yield return null;
                    Check(switched && manager.Front.CurrentRoomId == destination.Id,
                        "A later firefly volley can be manually aimed from another room");
                    foreach (Brick brick in Object.FindObjectsOfType<Brick>())
                        foreach (Collider2D collider in brick.GetComponentsInChildren<Collider2D>())
                            collider.enabled = false;
                    roomSwitchVerified = true;
                }
            }
            yield return null;
        }
    }

    private static float OrangeWeight(Color color) => color.r * 2f - color.g - color.b;
    private static bool Approximately(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.001f
        && Mathf.Abs(a.g - b.g) < 0.001f && Mathf.Abs(a.b - b.b) < 0.001f
        && Mathf.Abs(a.a - b.a) < 0.001f;

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        report.Add("PASS " + description);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Firefly repeat validation timed out");
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
        string result = error == null ? "PASS: " + report.Count + " repeat-fire and tint checks." : "FAIL: " + error;
        report.Add(result);
        File.WriteAllLines("Logs/FireflyRepeatTintValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
