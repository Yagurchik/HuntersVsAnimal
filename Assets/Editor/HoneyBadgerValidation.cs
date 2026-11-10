using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HoneyBadgerValidation
{
    [MenuItem("Tools/Forest Animal War/Validate Honey Badger And Boar")]
    public static void RunFromMenu()
    {
        Validate(false);
    }

    public static void RunBatch()
    {
        Validate(true);
    }

    private static void Validate(bool exitEditor)
    {
        try
        {
            GameObject badger = AssetDatabase.LoadAssetAtPath<GameObject>(HoneyBadgerAuthoring.PrefabPath);
            GameObject boar = AssetDatabase.LoadAssetAtPath<GameObject>(BoarAndBadgerAuthoring.BoarPrefabPath);
            HoneyBadgerProjectile badgerChain = badger != null
                ? badger.GetComponent<HoneyBadgerProjectile>()
                : null;
            Require(badgerChain != null,
                "Honey badger prefab owns its chain-attack component.");
            Require(Mathf.Approximately(badgerChain.ChainJumpRadius, 1.2f),
                "Honey badger starts with a 1.2-cell chain radius.");
            Require(Mathf.Approximately(badgerChain.DamageIncreasePerKill, 0.25f),
                "Every chain kill adds twenty-five percent of base damage.");
            Require(badgerChain.MaximumAttacks == 5
                    && badgerChain.AttacksAddedPerKill == 3
                    && badgerChain.MaximumConsecutiveAttacksPerTarget == 1,
                "Honey badger starts with five attacks and every kill extends the chain by three.");
            Require(Mathf.Approximately(badger.transform.localScale.x, 0.6f),
                "Honey badger combat model is twice its original size.");
            Require(badgerChain.PoisonCloudDamageMultiplier > 0f
                    && badgerChain.PoisonCloudRadius > 0f
                    && badgerChain.PoisonCloudDurationTurns > 0
                    && Mathf.Approximately(badgerChain.AttackPoisonCloudChance, 0.05f),
                "Enemy retaliation leaves poison on lethal damage and every chain attack has a five-percent cloud chance.");
            Require(badgerChain.DustBrawlSprite != null,
                "Honey badger chain attacks use the dust-brawl sprite.");
            SerializedObject badgerData = new SerializedObject(badgerChain);
            Require(badgerData.FindProperty("poisonPuffSprite").objectReferenceValue
                == AssetDatabase.LoadAssetAtPath<Sprite>(BoarAndBadgerAuthoring.BoarDustArtPath),
                "Honey badger poison uses the authored dust-puff cloud sprite.");
            GameObject ghostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Enemies/Ghost.prefab");
            Require(ghostPrefab != null && ghostPrefab.GetComponent<Brick>() != null,
                "Ghost prefab is available for the badger fall regression.");
            GameObject fallingBadger = UnityEngine.Object.Instantiate(badger);
            try
            {
                HoneyBadgerProjectile fall = fallingBadger.GetComponent<HoneyBadgerProjectile>();
                ValidateBossJumpRange(fall, ghostPrefab);
                Rigidbody2D fallBody = fallingBadger.GetComponent<Rigidbody2D>();
                ProjectilePhysics2D fallFlight = fallingBadger.GetComponent<ProjectilePhysics2D>();
                typeof(HoneyBadgerProjectile).GetField("body",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(fall, fallBody);
                typeof(HoneyBadgerProjectile).GetField("flight",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(fall, fallFlight);
                MethodInfo resume = typeof(HoneyBadgerProjectile).GetMethod("ResumeOrdinaryFall",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(resume != null, "Badger has a final descent after its chain.");
                resume.Invoke(fall, new object[] { null });
                Require(fall.IsFinishingChain && !fall.IsChainActive
                    && fallBody.simulated && fallFlight.IsFlying && fallBody.velocity.y < 0f,
                    "An exhausted badger enters a downward final descent.");
                Collider2D[] hitboxes = fallingBadger.GetComponentsInChildren<Collider2D>(true);
                Require(hitboxes.Length > 0 && Array.TrueForAll(hitboxes, collider => !collider.enabled),
                    "Final descent cannot collide with the ghost and ricochet upward.");
                fall.ModifyDamage(ghostPrefab.GetComponent<Brick>(), 10f);
                Require(!fall.IsChainActive,
                    "A ghost cannot restart the exhausted badger chain.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fallingBadger);
            }
            BoarProjectile boarCharge = boar != null ? boar.GetComponent<BoarProjectile>() : null;
            Require(boarCharge != null,
                "Boar prefab owns its charge component.");
            Require(boar.GetComponent<BoarChargeTrail>() != null,
                "Boar prefab owns its fading yellow trail.");
            Require(boar.GetComponent<CircleCollider2D>() == null,
                "Boar uses a point ray instead of a circular physics collider.");
            Require(boarCharge.PassesThroughEnemies
                    && boarCharge.MaximumWallBounces == 7
                    && boarCharge.WallBouncePauseDuration > 0f
                    && boarCharge.StunChance > 0f
                    && Mathf.Approximately(boarCharge.BossStunChance, 0.05f)
                    && Mathf.Approximately(boarCharge.VisualScaleMultiplier, 1f),
                "Boar runs visibly, damages each enemy once per segment, pauses at walls, and uses its compact projectile model.");
            Require(boar.GetComponent<ProjectileFlightVisual>() != null
                    && Mathf.Approximately(boar.GetComponent<ProjectileFlightVisual>().SpinDegreesPerSecond, 0f),
                "Boar artwork faces its movement direction instead of spinning.");

            EditorSceneManager.OpenScene(HoneyBadgerAuthoring.GameplayScene);
            AnimalProjectileQueue queue = UnityEngine.Object.FindObjectOfType<AnimalProjectileQueue>(true);
            Require(queue != null && queue.Animals.Count >= 5,
                "Gameplay queue contains the honey badger and boar.");
            Require(queue.Animals[3].Prefab == badger && queue.Animals[4].Prefab == boar,
                "Gameplay queue references the correct projectile prefabs.");
            Require(Mathf.Approximately(queue.GetHoneyBadgerAttackRadius(3), 1.2f)
                    && queue.GetHoneyBadgerMaximumAttacks(3) == 5
                    && queue.GetHoneyBadgerConsecutiveAttacks(3) == 1,
                "Honey badger starts with the intended unupgraded chain values.");
            Require(queue.GetBoarMaximumBounces(AnimalProjectileQueue.BoarIndex) == 7
                    && Mathf.Approximately(queue.GetBoarFirstHitDamageMultiplier(
                        AnimalProjectileQueue.BoarIndex), 1f),
                "Boar starts with seven paths and normal first-hit damage.");

            const string report = "Honey badger and boar validation passed.";
            Debug.Log(report);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/HoneyBadgerValidation.txt", report);
            if (exitEditor) EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (exitEditor) EditorApplication.Exit(1);
            else throw;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void ValidateBossJumpRange(HoneyBadgerProjectile badger, GameObject ghostPrefab)
    {
        GameObject largeBossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/PoisonousGloomBoss.prefab");
        GameObject smallBossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/DrownedMiniBoss.prefab");
        Require(largeBossPrefab != null && smallBossPrefab != null,
            "Boss prefabs are available for the badger jump regression.");
        GameObject roomObject = new GameObject("Honey Badger Jump Test Room");
        GameObject largeBoss = null;
        GameObject smallBoss = null;
        GameObject ordinaryEnemy = null;
        try
        {
            LevelGenerator room = roomObject.AddComponent<LevelGenerator>();
            MethodInfo findNext = typeof(HoneyBadgerProjectile).GetMethod("FindNextTarget",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(findNext != null, "Badger can search for its next chain target.");
            largeBoss = UnityEngine.Object.Instantiate(largeBossPrefab);
            smallBoss = UnityEngine.Object.Instantiate(smallBossPrefab);
            ordinaryEnemy = UnityEngine.Object.Instantiate(ghostPrefab);

            largeBoss.transform.position = new Vector3(2.1f, 0f, 0f);
            room.EnemyCollection.Add(largeBoss);
            Require((Brick)findNext.Invoke(badger, new object[] { room, Vector2.zero, null })
                == largeBoss.GetComponent<Brick>(),
                "A 1.2-cell radius reaches the near occupied cell of a 3x3 boss.");
            largeBoss.transform.position = new Vector3(3f, 0f, 0f);
            Require(findNext.Invoke(badger, new object[] { room, Vector2.zero, null }) == null,
                "A 3x3 boss outside the radius remains unreachable.");

            room.EnemyCollection.Clear();
            room.EnemyCollection.Add(smallBoss);
            smallBoss.transform.position = new Vector3(1.6f, 0f, 0f);
            Require((Brick)findNext.Invoke(badger, new object[] { room, Vector2.zero, null })
                == smallBoss.GetComponent<Brick>(),
                "A 1.2-cell radius reaches the near occupied cell of a 2x2 boss.");
            smallBoss.transform.position = new Vector3(2.5f, 0f, 0f);
            Require(findNext.Invoke(badger, new object[] { room, Vector2.zero, null }) == null,
                "A 2x2 boss outside the radius remains unreachable.");

            room.EnemyCollection.Clear();
            room.EnemyCollection.Add(ordinaryEnemy);
            ordinaryEnemy.transform.position = new Vector3(2f, 0f, 0f);
            Require(findNext.Invoke(badger, new object[] { room, Vector2.zero, null }) == null,
                "An ordinary enemy two cells away receives no boss allowance.");
            ordinaryEnemy.transform.position = new Vector3(1f, 0f, 0f);
            Require((Brick)findNext.Invoke(badger, new object[] { room, Vector2.zero, null })
                == ordinaryEnemy.GetComponent<Brick>(),
                "An adjacent ordinary enemy remains reachable.");
        }
        finally
        {
            if (ordinaryEnemy != null) UnityEngine.Object.DestroyImmediate(ordinaryEnemy);
            if (smallBoss != null) UnityEngine.Object.DestroyImmediate(smallBoss);
            if (largeBoss != null) UnityEngine.Object.DestroyImmediate(largeBoss);
            UnityEngine.Object.DestroyImmediate(roomObject);
        }
    }
}
