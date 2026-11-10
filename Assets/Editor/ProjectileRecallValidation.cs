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

// Run only in an isolated copy: exercises the real scene button and turn pipeline.
[InitializeOnLoad]
public static class ProjectileRecallValidation
{
    private const string Running = "ForestAnimalWar.RecallValidation.Running";
    private const string SavedPrefs = Running + ".Prefs";
    private static readonly string[] Keys = {
        "ForestAnimalWar.Meta.Gold", "ForestAnimalWar.Meta.Acorns", "ForestAnimalWar.Meta.Victories",
        "ForestAnimalWar.CurrentLevel", "ForestAnimalWar.HighestUnlockedLevel",
        "ForestAnimalWar.MenuSelectedLevel", "ForestAnimalWar.RequestedLevel"
    };
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> report = new List<string>();
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static int lastFrame = -1;
    private static double deadline;

    static ProjectileRecallValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            stack.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Directory.CreateDirectory("Logs");
        SessionState.SetString(SavedPrefs, string.Join("|", Keys.Select(k =>
            PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "-")));
        SessionState.SetBool(Running, true);
        LevelProgression.PrepareLevel(1);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        Player player = Object.FindObjectsOfType<Player>().First(p => p.isActiveAndEnabled);
        Button recall = Object.FindObjectsOfType<Button>(true).First(b =>
            Enumerable.Range(0, b.onClick.GetPersistentEventCount())
                .Any(i => b.onClick.GetPersistentMethodName(i) == "DropDownBalls"));
        Check(manager.HasActiveEnemies && !Player.HasActiveProjectiles, "Real battle is ready");
        manager.enabled = false; // Observe turn scheduling before allowing the enemy Update.
        manager.stepPlayer = false;
        int turn = manager.TurnNumber;
        Vector3 origin = player.transform.position;

        PlayerVisualProfile profile = player.VisualProfile;
        Check(profile != null && profile.IdleSprite != null
                && profile.ShootingAnimation != null,
            "Player profile contains an idle sprite and the authored shooting AnimationClip");
        Check(player.CharacterRenderer != null
                && player.CharacterRenderer.sprite == profile.IdleSprite
                && Mathf.Abs(player.CharacterRenderer.bounds.size.x - profile.WorldWidth) < 0.02f,
            "The replacement player sprite keeps the same world width in every room template");
        Transform eyeFire = player.CharacterRenderer.transform.Find("Pumpkin Eye Fire");
        Check(profile.EyeFirePrefab != null && eyeFire != null
                && eyeFire.GetComponent<PumpkinEyeFire>() != null
                && eyeFire.GetComponentsInChildren<SpriteRenderer>().Length == 2,
            "The pumpkin has two editable fire glows on its button eyes");
        MethodInfo shootVisual = typeof(Player).GetMethod("PlayShootingVisual", Private);
        Check(shootVisual != null, "Cardboard shooting animation is available");
        shootVisual.Invoke(player, null);
        yield return null;
        Check(player.CharacterRenderer.sprite != null && player.CharacterRenderer.sprite != profile.IdleSprite,
            "The shooting AnimationClip controls the character sprite");
        Sprite firstShotSprite = player.CharacterRenderer.sprite;
        yield return Wait(0.13f);
        Check(player.CharacterRenderer.sprite != null && player.CharacterRenderer.sprite != firstShotSprite
                && player.CharacterRenderer.sprite != profile.IdleSprite,
            "The authored shooting clip advances to subsequent sprite keys");
        Sprite pausedSprite = player.CharacterRenderer.sprite;
        Time.timeScale = 0f;
        double animationResumeAt = EditorApplication.timeSinceStartup + 0.25;
        while (EditorApplication.timeSinceStartup < animationResumeAt) yield return null;
        Check(player.CharacterRenderer.sprite == pausedSprite && Player.HasActiveProjectiles,
            "Pause freezes the shooting clip and retains the input lock");
        Time.timeScale = 1f;
        yield return Wait(profile.ShootingAnimation.length + 0.1f);
        Check(player.CharacterRenderer.sprite == profile.IdleSprite,
            "Shooting AnimationClip finishes and returns to idle");
        shootVisual.Invoke(player, null);
        yield return null;
        Check(player.CharacterRenderer.sprite != null && player.CharacterRenderer.sprite != profile.IdleSprite,
            "The shooting clip can play again after returning to idle");
        player.gameObject.SetActive(false);
        Check(!Player.HasActiveProjectiles, "Disabling the player releases the animation input lock");
        player.gameObject.SetActive(true);
        yield return null;
        Check(player.CharacterRenderer.sprite == profile.IdleSprite && !Player.HasActiveProjectiles,
            "Re-enabling the player shows the original idle sprite without autoplay");

        MethodInfo beginVolley = typeof(Player).GetMethod("BeginVolley", Private);
        MethodInfo releaseVolley = typeof(Player).GetMethod("ReleaseVolley", Private);
        Stage stage = player.GetComponentInParent<Stage>();
        Check(beginVolley != null && releaseVolley != null && stage != null,
            "Projectile landing pipeline is available in the active room");

        beginVolley.Invoke(player, new object[] { 0 });
        bool landed = player.TryCommitProjectileLanding(stage.transform.position.x + 100f);
        releaseVolley.Invoke(player, null);
        Check(landed && Player.HasActiveProjectiles,
            "Landing starts a locked stop-motion relocation instead of teleporting");
        yield return Wait(profile.RelocationFrameDuration * 1.2f);
        Check(Mathf.Abs(Mathf.DeltaAngle(0f,
                player.CharacterRenderer.transform.localEulerAngles.z))
                <= profile.RelocationTiltDegrees + 0.1f,
            "Cardboard relocation rocks the pumpkin without spinning it");
        yield return Wait(profile.RelocationSteps * profile.RelocationFrameDuration + 0.1f);
        float visualHalfWidth = player.VisualHalfWidth;
        float maximumPlayerX = stage.transform.position.x + stage.BattlefieldSize.x * 0.5f - visualHalfWidth;
        Check(landed && player.transform.position.x <= maximumPlayerX + 0.01f,
            "Landing beyond a side wall is clamped with the player's visual width");
        Check(!Player.HasActiveProjectiles,
            "The next action unlocks after the cardboard relocation finishes");
        player.transform.position = origin;

        beginVolley.Invoke(player, new object[] { 0 });
        Animal firstDestroyed = Spawn(queue, player, 0);
        Animal lastDestroyed = Spawn(queue, player, 0);
        firstDestroyed.GetComponent<Rigidbody2D>().simulated = false;
        lastDestroyed.GetComponent<Rigidbody2D>().simulated = false;
        firstDestroyed.transform.position = origin + new Vector3(-1f, 3f, 0f);
        lastDestroyed.transform.position = origin + new Vector3(1f, 3f, 0f);
        Object.Destroy(firstDestroyed.gameObject);
        yield return null;
        Check(player.transform.position == origin,
            "An early destroyed projectile cannot steal the landing from the rest of its volley");
        Object.Destroy(lastDestroyed.gameObject);
        yield return null;
        Check(player.transform.position == origin,
            "A landing waits until the volley has finished emitting");
        releaseVolley.Invoke(player, null);
        Check(Player.HasActiveProjectiles,
            "The final impact keeps input locked during the visible relocation frames");
        yield return Wait(profile.RelocationSteps * profile.RelocationFrameDuration + 0.1f);
        Check(Mathf.Abs(player.transform.position.x - (origin.x + 1f)) < 0.01f,
            "The last impact repositions the player when no projectile reaches the exit");
        player.transform.position = origin;

        Animal normal = Spawn(queue, player, 0);
        normal.GetComponent<Rigidbody2D>().simulated = false;
        yield return Wait(2.1f);
        Check(normal != null && !normal.IsReturning && Player.HasActiveProjectiles,
            "Ordinary flight has no automatic two-second expiry");
        normal.GetComponent<Rigidbody2D>().simulated = true;
        recall.onClick.Invoke();
        ProjectilePhysics2D physics = normal.GetComponent<ProjectilePhysics2D>();
        Check(normal.IsReturning && physics.TargetSpeed >= 40f && physics.TargetSpeed >= 36f,
            "Scene button recalls faster than ordinary flight");
        Check(normal.GetComponentsInChildren<Collider2D>(true).All(c => !c.enabled),
            "Returning projectile no longer hits enemies or walls");
        Check(normal.GetComponent<Rigidbody2D>().velocity.y < 0f, "Recall points below the field");
        yield return Wait(0.8f);
        Check(normal == null && !Player.HasActiveProjectiles, "Normal return completes before timeout");
        Check(queue.GetRemainingAmmo(0) == 3 && !manager.stepGame, "Idle button does not spend another volley");

        var stuck = Enumerable.Range(0, 3).Select(i => Spawn(queue, player, i)).ToArray();
        foreach (Animal animal in stuck) animal.GetComponent<Rigidbody2D>().simulated = false;
        recall.onClick.Invoke();
        yield return Wait(1.15f);
        Check(stuck.All(a => a != null) && !manager.CanSwitchRooms,
            "Stuck firefly, hedgehog and rabbit keep the shot locked before deadline");
        recall.onClick.Invoke();
        yield return Wait(1f);
        Check(stuck.All(a => a == null) && !Player.HasActiveProjectiles,
            "All three types are destroyed after two seconds; repeated click cannot extend timeout");
        Check(manager.CanSwitchRooms && !manager.stepGame && manager.TurnNumber == turn,
            "Cleanup unlocks rooms without giving enemies an early turn");
        Check(player.transform.position == origin, "Recall does not move the player's firing origin");

        Animal paused = Spawn(queue, player, 0);
        paused.GetComponent<Rigidbody2D>().simulated = false;
        recall.onClick.Invoke();
        Time.timeScale = 0f;
        double resumeAt = EditorApplication.timeSinceStartup + 2.1;
        while (EditorApplication.timeSinceStartup < resumeAt) yield return null;
        Check(paused != null && Player.HasActiveProjectiles, "Pause suspends the recall countdown");
        Time.timeScale = 1f;
        yield return Wait(2.15f);
        Check(paused == null && !Player.HasActiveProjectiles, "Countdown resumes after pause");

        GameObject bodyless = new GameObject("Recall without Rigidbody");
        bodyless.transform.position = origin + Vector3.up * 3;
        Animal bodylessAnimal = bodyless.AddComponent<Animal>();
        bodylessAnimal.SetReturnTarget(player.gameObject);
        recall.onClick.Invoke();
        yield return Wait(2.15f);
        Check(bodyless == null && !Player.HasActiveProjectiles, "Fallback also resolves missing physics bodies");

        queue.Unlock(1);
        typeof(AnimalProjectileQueue.AnimalProjectileEntry).GetField("ammoPerTurn", Private)
            .SetValue(queue.Animals[0], 40);
        queue.ReloadAll();
        queue.Select(0);
        Shoot(player);
        Check(Player.HasActiveProjectiles && queue.GetRemainingAmmo(0) == 39, "Long real volley begins");
        recall.onClick.Invoke();
        float recalledAt = Time.time;
        int maxLiving = 0;
        while (Time.time < recalledAt + 2.15f)
        {
            maxLiving = Mathf.Max(maxLiving, Player.animalsCollect.Count);
            foreach (Animal animal in Player.animalsCollect)
            {
                if (animal == null) continue;
                if (!animal.IsReturning) throw new Exception("Late shot missed the shared recall deadline");
                animal.GetComponent<Rigidbody2D>().simulated = false;
            }
            yield return null;
        }
        Check(maxLiving > 1, "Shots emitted after the button also join recall");
        Check(!Player.HasActiveProjectiles && Player.animalsCollect.Count == 0,
            "Two-second limit resolves both a long emitter and all its stuck bullets");
        Check(queue.GetRemainingAmmo(0) == 0 && queue.GetRemainingAmmo(1) == queue.Animals[1].AmmoPerTurn,
            "Only the recalled animal is spent; hedgehog ammunition is preserved");
        Check(!manager.stepGame && manager.TurnNumber == turn && manager.CanSwitchRooms,
            "Other animal can act before the enemy turn");
        yield return Wait(0.25f);
        Check(!Player.HasActiveProjectiles, "Cancelled long volley cannot emit orphan bullets");

        queue.Select(1);
        Shoot(player);
        Check(Player.HasActiveProjectiles && Player.animalsCollect.All(a => !a.IsReturning),
            "Next animal fires normally with no inherited recall flag");
        recall.onClick.Invoke();
        float lastRecall = Time.time;
        while (Time.time < lastRecall + 2.15f)
        {
            foreach (Animal animal in Player.animalsCollect)
                if (animal != null) animal.GetComponent<Rigidbody2D>().simulated = false;
            yield return null;
        }
        Check(!Player.HasActiveProjectiles && !queue.HasRemainingAmmo && manager.stepGame,
            "Last animal's return schedules the enemy turn");
        manager.enabled = true;
        yield return null;
        yield return null;
        while (manager.IsResolvingEnemyTurn) yield return null;
        Check(manager.TurnNumber == turn + 1, "Enemies take exactly one turn after all animals are spent");

        manager.enabled = false;
        manager.stepPlayer = false;
        queue.ClearAllAmmo();
        queue.Unlock(1);
        queue.Select(1);
        Shoot(player);
        manager.RegisterEnemyDefeated(manager.ExperienceForNextUpgrade);
        Check(manager.PendingProjectileUpgrades > 0 && !manager.IsChoosingUpgrade,
            "Earned upgrade waits while a recalled volley is still in flight");
        recall.onClick.Invoke();
        while (Player.HasActiveProjectiles)
        {
            foreach (Animal animal in Player.animalsCollect)
                if (animal != null) animal.GetComponent<Rigidbody2D>().simulated = false;
            yield return null;
        }
        Check(manager.IsChoosingUpgrade && !manager.stepGame && manager.TurnNumber == turn + 1,
            "Timeout opens the upgrade menu before enemies move");
        manager.SelectProjectileUpgrade(1);
        Check(!manager.IsChoosingUpgrade && manager.stepGame && !Player.HasActiveProjectiles,
            "Upgrade selection after recall cannot fire an extra projectile");
        manager.enabled = true;
        yield return null;
        yield return null;
        Check(manager.TurnNumber == turn + 2, "Enemy turn resumes once after the earned upgrade");
    }

    private static Animal Spawn(AnimalProjectileQueue queue, Player player, int index)
    {
        GameObject projectile = Object.Instantiate(queue.Animals[index].Prefab,
            player.transform.position + Vector3.up * 4, Quaternion.identity);
        Animal animal = projectile.GetComponent<Animal>();
        animal.SetReturnTarget(player.gameObject);
        ProjectilePhysics2D physics = projectile.GetComponent<ProjectilePhysics2D>();
        if (physics == null) physics = projectile.AddComponent<ProjectilePhysics2D>();
        physics.Launch(Vector2.up, 18f);
        return animal;
    }

    private static void Shoot(Player player)
    {
        typeof(Player).GetMethod("Shoot", Private).Invoke(player,
            new object[] { (Vector2)player.transform.position + Vector2.up * 5 });
    }

    private static IEnumerator Wait(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Recall validation timed out");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            while (stack.Count > 0)
            {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                if (stack.Peek().Current is IEnumerator nested) { stack.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report.Add("PASS " + message);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        Time.timeScale = 1f;
        string[] saved = SessionState.GetString(SavedPrefs, "").Split('|');
        for (int i = 0; i < Keys.Length && i < saved.Length; i++)
            if (saved[i] == "-") PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetInt(Keys[i], int.Parse(saved[i]));
        PlayerPrefs.Save();
        string result = error == null ? "PASS: " + report.Count + " projectile recall checks." : "FAIL: " + error;
        report.Add(result);
        File.WriteAllLines("Logs/ProjectileRecallValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
