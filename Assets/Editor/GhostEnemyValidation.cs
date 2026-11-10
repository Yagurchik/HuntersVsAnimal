using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class GhostEnemyValidation
{
    private const string Running = "ForestAnimalWar.GhostValidation.Running";
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static double deadline;
    private static int lastFrame = -1;

    static GhostEnemyValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 60d;
            Steps.Push(CheckGhost());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
        Directory.CreateDirectory("Logs");
        Report.Clear();
        SessionState.SetBool(Running, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(1);
        EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckGhost()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        Check(manager != null && manager.Front != null, "Campaign scene is ready");
        manager.enabled = false;
        Time.timeScale = 1f;
        LevelGenerator room = manager.Front.GetGenerator(0);
        GameObject prefab = Resources.Load<GameObject>("Enemies/Ghost");
        GhostPhasing templatePhase = prefab != null ? prefab.GetComponent<GhostPhasing>() : null;
        GhostVisualAnimator templateVisual = prefab != null
            ? prefab.GetComponentInChildren<GhostVisualAnimator>() : null;
        Brick templateBrick = prefab != null ? prefab.GetComponent<Brick>() : null;
        Check(templatePhase != null && templateVisual != null && templateBrick != null
            && templateVisual.GetComponent<SpriteRenderer>().sprite != null,
            "Editable ghost prefab contains art, animation and phasing");
        SpriteRenderer ghostRenderer = templateVisual.GetComponent<SpriteRenderer>();
        BoxCollider2D ghostCollider = prefab.GetComponent<BoxCollider2D>();
        Check(ghostRenderer.sprite.name == "GhostReferenceCutout"
            && Mathf.Abs(ghostRenderer.transform.localScale.x * 1045f / 1254f
                / ghostCollider.size.x - 1.05f) < 0.002f
            && Mathf.Abs(ghostRenderer.transform.localScale.y * 1075f / 1254f
                / ghostCollider.size.y - 1.05f) < 0.002f
            && Mathf.Approximately(ghostCollider.edgeRadius, 0.12f)
            && prefab.GetComponent<EnemyFirstEncounterProfile>().Portrait
                == ghostRenderer.sprite,
            "Reference art is 5% wider and taller than the collider and is used on the encounter card");
        Check(Mathf.Approximately(templatePhase.PassThroughChance, 0.2f)
            && Mathf.Approximately(templateBrick.HealthCoefficient, 0.8f),
            "Prefab starts with 20% phasing and 80% ordinary health");

        GameObject enemyObject = Object.Instantiate(prefab, new Vector3(100f, 100f),
            Quaternion.identity, room.transform);
        Brick enemy = enemyObject.GetComponent<Brick>();
        GhostPhasing phase = enemyObject.GetComponent<GhostPhasing>();
        Rigidbody2D enemyBody = enemyObject.GetComponent<Rigidbody2D>();
        enemyBody.bodyType = RigidbodyType2D.Kinematic;
        enemyBody.gravityScale = 0f;
        enemy.levelGenerator = room;
        Check(Mathf.Approximately(enemy.MaxHp, room.OrdinaryEnemyBaseHealth * 0.8f),
            "Live ghost has exactly 80% of ordinary enemy HP on level 1");

        GameObject projectileObject = new GameObject("Ghost validation firefly");
        projectileObject.tag = "Animal";
        projectileObject.transform.position = new Vector3(100f, 98f);
        Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        CircleCollider2D projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
        projectileCollider.radius = 0.1f;
        Animal projectile = projectileObject.AddComponent<Animal>();
        projectile.SetRuntimeDamage(10f);
        projectile.SetRuntimeHealth(100f);
        projectileObject.AddComponent<FireflyImpactEffect>();
        ProjectilePhysics2D flight = projectileObject.AddComponent<ProjectilePhysics2D>();
        flight.Launch(Vector2.up, 5f);
        Collider2D enemyCollider = enemyObject.GetComponent<Collider2D>();
        float fullHealth = enemy.hp;

        SerializedObject phaseData = new SerializedObject(phase);
        phaseData.FindProperty("passThroughChance").floatValue = 1f;
        phaseData.ApplyModifiedPropertiesWithoutUndo();
        Check(phase.TryPassThrough(projectile)
            && Physics2D.GetIgnoreCollision(enemyCollider, projectileCollider),
            "Successful phase disables collision with this projectile");
        Check(!enemy.ApplyProjectileHit(projectile, enemy.transform.position, Vector2.down)
            && Mathf.Approximately(enemy.hp, fullHealth)
            && !enemy.GetComponent<EnemyStatusEffects>()
                .HasPeriodicDamage(FireflyImpactEffect.BurnEffectId),
            "Passed-through firefly deals no damage and applies no burn");
        Check(body.velocity.y > 0f, "Projectile keeps its incoming direction");

        body.position = new Vector2(100f, 100f);
        Physics2D.SyncTransforms();
        yield return null;
        Check(Physics2D.GetIgnoreCollision(enemyCollider, projectileCollider)
            && phase.IsPassingThrough(projectile),
            "Collision stays disabled while the projectile is inside the ghost collider");

        body.position = new Vector2(100f, 103f);
        float restoreDeadline = Time.time + 0.5f;
        while (Physics2D.GetIgnoreCollision(enemyCollider, projectileCollider)
            && Time.time < restoreDeadline) yield return null;
        Check(!Physics2D.GetIgnoreCollision(enemyCollider, projectileCollider),
            "Enemy collision returns after projectile leaves ghost bounds");

        phaseData.Update();
        phaseData.FindProperty("passThroughChance").floatValue = 0f;
        phaseData.ApplyModifiedPropertiesWithoutUndo();
        Check(!phase.TryPassThrough(projectile), "Zero chance cannot phase");
        Check(enemy.ApplyProjectileHit(projectile, enemy.transform.position, Vector2.down)
            && Mathf.Approximately(enemy.hp, fullHealth - 10f)
            && enemy.GetComponent<EnemyStatusEffects>()
                .HasPeriodicDamage(FireflyImpactEffect.BurnEffectId),
            "Normal firefly hit deals direct damage and applies burn");
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(manager.TurnNumber + 1);
        Check(Mathf.Approximately(enemy.hp, fullHealth - 35f),
            "Existing fire damage cannot be phased through");

        GameObject contactGhostObject = Object.Instantiate(prefab, new Vector3(105f, 100f),
            Quaternion.identity, room.transform);
        Brick contactGhost = contactGhostObject.GetComponent<Brick>();
        Rigidbody2D contactGhostBody = contactGhostObject.GetComponent<Rigidbody2D>();
        contactGhostBody.bodyType = RigidbodyType2D.Kinematic;
        contactGhostBody.gravityScale = 0f;
        SerializedObject contactPhaseData = new SerializedObject(
            contactGhostObject.GetComponent<GhostPhasing>());
        BoxCollider2D contactGhostCollider = contactGhostObject.GetComponent<BoxCollider2D>();
        contactPhaseData.FindProperty("passThroughChance").floatValue = 1f;
        contactPhaseData.ApplyModifiedPropertiesWithoutUndo();
        GameObject flyingObject = new GameObject("Ghost physical contact projectile");
        flyingObject.tag = "Animal";
        flyingObject.transform.position = new Vector3(105f, 98f);
        Rigidbody2D flyingBody = flyingObject.AddComponent<Rigidbody2D>();
        flyingBody.gravityScale = 0f;
        flyingBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        CircleCollider2D flyingCollider = flyingObject.AddComponent<CircleCollider2D>();
        flyingCollider.radius = 0.1f;
        Animal flyingAnimal = flyingObject.AddComponent<Animal>();
        flyingAnimal.SetRuntimeDamage(10f);
        flyingAnimal.SetRuntimeHealth(100f);
        flyingObject.AddComponent<ProjectilePhysics2D>().Launch(Vector2.up, 8f);
        float contactHealth = contactGhost.hp;
        float contactDeadline = Time.time + 1.5f;
        while (flyingObject != null && flyingObject.transform.position.y < 102f
            && Time.time < contactDeadline) yield return null;
        float collisionRestoreDeadline = Time.time + 0.5f;
        while (Physics2D.GetIgnoreCollision(contactGhostCollider, flyingCollider)
            && Time.time < collisionRestoreDeadline) yield return null;
        Check(!Physics2D.GetIgnoreCollision(contactGhostCollider, flyingCollider),
            "Real missed shot restores collision after clearing the far side");
        Check(flyingObject != null && flyingObject.transform.position.y >= 102f
            && flyingBody.velocity.y > 0f
            && Mathf.Approximately(contactGhost.hp, contactHealth)
            && Mathf.Approximately(flyingAnimal.Health, 100f),
            "Real physics contact lets the projectile cross the ghost without bouncing or damage");

        Object.Destroy(projectileObject);
        Object.Destroy(enemyObject);
        Object.Destroy(flyingObject);
        Object.Destroy(contactGhostObject);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Report.Add("PASS " + description);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Ghost validation timed out");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            while (Steps.Count > 0)
            {
                IEnumerator current = Steps.Peek();
                if (!current.MoveNext()) { Steps.Pop(); continue; }
                if (current.Current is IEnumerator nested) { Steps.Push(nested); continue; }
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
        Report.Add(error == null ? "PASS: ghost integration checks" : "FAIL: " + error);
        File.WriteAllLines("Logs/GhostEnemyValidation.txt", Report);
        if (error == null) Debug.Log(Report[Report.Count - 1]);
        else Debug.LogError(Report[Report.Count - 1]);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
