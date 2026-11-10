using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ZombieEnemyValidation
{
    private const string Running = "ForestAnimalWar.ZombieValidation.Running";
    private static readonly List<string> Report = new List<string>();
    private static readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;

    static ZombieEnemyValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            Stack.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        Directory.CreateDirectory("Logs");
        SessionState.SetBool(Running, true);
        LevelProgression.PrepareLevel(2);
        EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        manager.enabled = false;
        manager.stepGame = manager.stepPlayer = false;
        LevelProgression progression = LevelProgression.Instance;
        LevelGenerator room = manager.Front.GetGenerator(0);
        GameObject prefab = Resources.Load<GameObject>("Enemies/Zombie");
        ZombieResurrection prefabZombie = prefab != null ? prefab.GetComponent<ZombieResurrection>() : null;
        Brick prefabBrick = prefab != null ? prefab.GetComponent<Brick>() : null;
        Check(prefabZombie != null && prefabBrick != null,
            "Zombie is an editable Resources enemy prefab");
        Check(prefabZombie.LivingSprite != null && prefabZombie.GraveSprite != null
            && prefabBrick.Footprint == Vector2Int.one,
            "Zombie prefab contains both supplied sprites and occupies one cell");
        Check(progression.GetZombieCountPerWave(1, 1, 1) == 0,
            "Twilight Thicket level one never requests zombies");
        Check(progression.GetZombieCountPerWave(2, 1, 1) == 1
            && progression.GetZombieCountPerWave(5, 4, 7) == 1,
            "The default editable rule replaces one enemy per wave with a zombie from level two onward");

        var placement = new EnemyRoomGrid(room, room.Rows);
        List<Vector2Int> freeAnchors = placement.FreeAnchors(Vector2Int.one);
        Check(freeAnchors.Count > 0, "Validation room has a free cell for the zombie");
        GameObject zombieObject = Object.Instantiate(prefab,
            placement.Center(freeAnchors[0], Vector2Int.one), Quaternion.identity, room.transform);
        Brick zombie = zombieObject.GetComponent<Brick>();
        zombie.levelGenerator = room;
        room.EnemyCollection.Add(zombieObject);
        ZombieResurrection resurrection = zombie.GetComponent<ZombieResurrection>();
        Vector3 gravePosition = zombie.transform.position;
        float maximumHealth = zombie.MaxHp;
        int experienceBefore = manager.CurrentExperience;
        int upgradeLevelBefore = manager.PlayerUpgradeLevel;
        int roomCountBefore = room.EnemyCollection.Count;
        // A status kill turns this zombie into a grave and clears its effects
        // inside the damage callback. The other status must not tick afterward.
        EnemyStatusEffects statuses = zombie.GetComponent<EnemyStatusEffects>();
        zombie.hp = 5f;
        statuses.ApplyPeriodicDamage("grave_extra", 2f, 1);
        statuses.ApplyPeriodicDamage("grave_lethal", 10f, 1);
        int graveTicks = EnemyStatusEffects.ProcessAllAfterEnemyTurn(manager.TurnNumber + 100);
        Check(graveTicks == 1 && statuses.ActivePeriodicDamageCount == 0,
            "A lethal periodic tick creates the grave without processing cleared effects");
        yield return null;
        Check(zombie != null && !zombie.IsDefeated && resurrection.IsAwaitingResurrection
            && resurrection.BlocksCell && zombie.transform.position == gravePosition
            && Mathf.Approximately(resurrection.GraveHealth, maximumHealth),
            "First defeat leaves a shootable full-health grave on the exact death cell");
        Check(room.EnemyCollection.Contains(zombie.gameObject)
            && room.EnemyCollection.Count == roomCountBefore
            && manager.CurrentExperience == experienceBefore,
            "The grave remains part of the battle and does not grant defeat experience early");
        Check(resurrection.SpriteRenderer.sprite == resurrection.GraveSprite
            && zombie.GetComponentsInChildren<Collider2D>(true).Any(collider => collider.enabled),
            "The supplied grave art replaces the zombie and keeps the authored hitbox");
        float damagePoseEnd = Time.realtimeSinceStartup + 0.25f;
        while (Time.realtimeSinceStartup < damagePoseEnd) yield return null;
        Check(resurrection.SpriteRenderer.sprite == resurrection.GraveSprite,
            "The zombie damage pose cannot restore the living sprite over the grave");
        zombie.TakeProjectileDamage(1f, 0, CombatDamageSource.Rabbit);
        yield return null;
        Check(resurrection.SpriteRenderer.sprite == resurrection.GraveSprite,
            "Shooting the grave keeps its grave artwork visible");
        Check(EnemyCombatTextUI.Instance.GetHealthText(zombie) != null,
            "The grave displays its own full health");

        EnemyRoomGrid grid = new EnemyRoomGrid(room, room.Rows);
        Vector2Int graveCell = grid.Anchor(gravePosition, Vector2Int.one);
        Check(!grid.FreeAnchors(Vector2Int.one).Contains(graveCell),
            "Spawn and pressure placement reserve the grave cell");

        GameObject followerObject = Object.Instantiate(room.brickPrefab,
            gravePosition + Vector3.up, Quaternion.identity, room.transform);
        Brick follower = followerObject.GetComponent<Brick>();
        follower.levelGenerator = room;
        Vector3 followerBefore = follower.transform.position;
        follower.Step();
        yield return null;
        Check(!follower.IsStepping && follower.transform.position == followerBefore,
            "An enemy directly behind the grave loses its movement for that turn");

        Check(ZombieResurrection.ProcessAllAfterEnemyTurn(manager.TurnNumber) == 0
            && resurrection.IsAwaitingResurrection,
            "Zombie cannot rise on the death turn");
        Check(ZombieResurrection.ProcessAllAfterEnemyTurn(manager.TurnNumber + 1) == 0
            && resurrection.IsAwaitingResurrection,
            "Zombie waits while the player receives one full attack turn against the grave");
        Check(ZombieResurrection.ProcessAllAfterEnemyTurn(manager.TurnNumber + 2) == 1,
            "Zombie rises after that player attack turn if the grave survives");
        Check(!zombie.IsDefeated && !resurrection.IsAwaitingResurrection
            && Mathf.Approximately(zombie.hp, maximumHealth * 0.5f)
            && zombie.transform.position == gravePosition,
            "Resurrected zombie returns on the same cell with exactly half maximum health");
        Check(resurrection.SpriteRenderer.sprite == resurrection.LivingSprite
            && zombie.GetComponentsInChildren<Collider2D>(true).Any(collider => collider.enabled),
            "Living art, collisions and health UI return after resurrection");

        zombie.TakeDamage(maximumHealth * 2f);
        yield return null;
        Check(zombie == null && room.EnemyCollection.Count == roomCountBefore - 1
            && manager.CurrentExperience == experienceBefore + 20
            && manager.PlayerUpgradeLevel == upgradeLevelBefore,
            "Second defeat is permanent and grants the zombie's configured experience once");
        Object.Destroy(followerObject);
        yield return null;

        // A second zombie verifies damage and permanent destruction of a grave.
        GameObject breakableObject = Object.Instantiate(prefab,
            room.transform.position + new Vector3(0.5f, -0.5f), Quaternion.identity, room.transform);
        Brick breakable = breakableObject.GetComponent<Brick>();
        breakable.levelGenerator = room;
        room.EnemyCollection.Add(breakableObject);
        float graveMaximum = breakable.MaxHp;
        int experienceBeforeGrave = manager.CurrentExperience;
        int upgradeLevelBeforeGrave = manager.PlayerUpgradeLevel;
        int nextUpgradeCost = manager.ExperienceForNextUpgrade;
        int expectedExperienceAfterGrave = experienceBeforeGrave + breakable.ExperienceReward;
        int expectedUpgradeAfterGrave = upgradeLevelBeforeGrave;
        if (expectedExperienceAfterGrave >= nextUpgradeCost)
        {
            expectedExperienceAfterGrave -= nextUpgradeCost;
            expectedUpgradeAfterGrave++;
        }
        breakable.TakeDamage(graveMaximum + 1f);
        ZombieResurrection breakableGrave = breakable.GetComponent<ZombieResurrection>();
        Check(breakableGrave.IsAwaitingResurrection && room.HasLivingEnemies,
            "A grave is a shootable room target after the zombie dies");
        breakable.TakeProjectileDamage(graveMaximum * 0.25f, 0, CombatDamageSource.Rabbit);
        Check(breakableGrave.IsAwaitingResurrection
            && Mathf.Abs(breakableGrave.GraveHealth - graveMaximum * 0.75f) < 0.01f,
            "Projectile damage reduces the grave's independent full-health pool");
        breakable.TakeProjectileDamage(graveMaximum, 0, CombatDamageSource.Rabbit);
        yield return null;
        Check(breakable == null && manager.CurrentExperience == expectedExperienceAfterGrave
            && manager.PlayerUpgradeLevel == expectedUpgradeAfterGrave,
            "Breaking the grave prevents resurrection and grants experience once");
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Report.Add("PASS " + description);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Zombie validation timed out");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            while (Stack.Count > 0)
            {
                IEnumerator current = Stack.Peek();
                if (!current.MoveNext()) { Stack.Pop(); continue; }
                if (current.Current is IEnumerator nested) { Stack.Push(nested); continue; }
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
        string result = error == null ? "PASS: " + Report.Count + " zombie integration checks." : "FAIL: " + error;
        Report.Add(result);
        File.WriteAllLines("Logs/ZombieEnemyValidation.txt", Report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
