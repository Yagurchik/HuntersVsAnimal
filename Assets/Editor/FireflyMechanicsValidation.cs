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

// Batch-only integration validation. Run from the isolated project copy.
[InitializeOnLoad]
public static class FireflyMechanicsValidation
{
    private const string Running = "ForestAnimalWar.FireflyValidation.Running";
    private const string SavedPrefs = Running + ".Prefs";
    private static readonly string[] Keys = {
        "ForestAnimalWar.Meta.Gold", "ForestAnimalWar.Meta.Acorns", "ForestAnimalWar.Meta.Victories",
        "ForestAnimalWar.CurrentLevel", "ForestAnimalWar.HighestUnlockedLevel",
        "ForestAnimalWar.MenuSelectedLevel", "ForestAnimalWar.RequestedLevel"
    };
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> report = new List<string>();
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static int checks;
    private static int lastFrame = -1;
    private static double deadline;

    static FireflyMechanicsValidation()
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
        SessionState.SetString(SavedPrefs, string.Join("|", Keys.Select(key =>
            PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key).ToString() : "-")));
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
        manager.enabled = false;
        manager.isPaused = false;
        manager.stepGame = false;
        manager.stepPlayer = false;
        Player player = Object.FindObjectsOfType<Player>().First(component => component.isActiveAndEnabled);
        player.enabled = false;
        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        Stage room = manager.Front.GetGenerator(0).GetComponentInParent<Stage>();
        LevelGenerator generator = manager.Front.GetGenerator(0);
        RoomDarkness darkness = room.GetComponent<RoomDarkness>();
        var settings = new SerializedObject(darkness);
        settings.FindProperty("useLineLighting").boolValue = false;
        settings.FindProperty("litFraction").floatValue = 0f;
        settings.FindProperty("environmentOpacity").floatValue = 0.55f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        darkness.Refresh();

        Check(queue.Animals[0].Prefab.GetComponent<FireflyImpactEffect>() != null,
            "Firefly prefab owns the light-and-burn impact effect");
        Check(queue.Animals.Skip(1).All(entry => entry.Prefab.GetComponent<FireflyImpactEffect>() == null),
            "Non-firefly projectiles do not inherit firefly effects");
        List<AnimalProjectileQueue.UpgradeOption> initialOptions = queue.GetAvailableUpgradeOptions();
        Check(queue.Animals[AnimalProjectileQueue.RabbitIndex].Prefab != null
            && AssetDatabase.GetAssetPath(
                queue.Animals[AnimalProjectileQueue.RabbitIndex].Prefab)
                == "Assets/AnimalsBullet/Rabbit.prefab",
            "Gameplay roster references the current Rabbit prefab root");
        Check(queue.Animals[0].AmmoPerTurn == 5 && queue.GetAmmoUpgradeLevel(0) == 1
            && initialOptions.Count(option => option.AnimalIndex == 0) == 2
            && !initialOptions.Any(option => option.AnimalIndex == 0 && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo),
            "Firefly has five shots and only manual-volley and burn branches");
        Check(initialOptions.Count(option => option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo)
            == queue.Animals.Select((entry, index) => new { entry, index })
                .Count(item => queue.IsAnimalAllowedForRun(item.index) && item.entry.IsConfigured && !item.entry.IsUnlocked),
            "Locked projectile types are offered through their own ammunition branch");
        Check(queue.Animals[AnimalProjectileQueue.RabbitIndex].BaseAmmoPerTurn == 2
                && initialOptions.Any(option => option.AnimalIndex == AnimalProjectileQueue.RabbitIndex
                    && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo),
            "Rabbit unlock starts with two projectiles");
        Brick prefab = generator.brickPrefab.GetComponent<Brick>();
        Check(prefab.GetComponent<EnemyIllumination>() != null
            && prefab.GetComponent<EnemyStatusEffects>() != null,
            "Enemy prefab exposes reusable illumination and status components");
        Check(prefab.ImpactStyle == Brick.ImpactEffectStyle.WhiteDust
            && prefab.ImpactEffectPrefab == null,
            "Current enemy configures its own built-in white dust impact effect");

        Brick enemy = generator.EnemyCollection[0].GetComponent<Brick>();
        foreach (GameObject other in generator.EnemyCollection)
            if (other != enemy.gameObject)
                foreach (Collider2D collider in other.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        enemy.transform.position = new Vector3(room.transform.position.x,
            room.transform.position.y + 2.25f, enemy.transform.position.z);
        enemy.hp = 1000f;
        yield return null;
        EnemyDarknessVisibility visibility = enemy.GetComponent<EnemyDarknessVisibility>();
        Check(!darkness.IsVisible(enemy.transform.position) && visibility.IsHidden,
            "Unlit enemy starts fully concealed in darkness");

        float hpBeforeHiddenHit = enemy.hp;
        int hiddenDamageNumbersBefore = EnemyCombatTextUI.Instance.ActiveDamageNumberCount;
        int hiddenImpactEffectsBefore = EnemyImpactEffectPool.ActiveEffectCount;
        enemy.PlayProjectileImpact(enemy.transform.position + Vector3.down * 0.5f, Vector2.down);
        enemy.TakeDamage(1f);
        yield return null;
        Check(enemy.hp < hpBeforeHiddenHit && visibility.IsHidden
            && !darkness.IsVisible(enemy.transform.position),
            "Projectile damage does not reveal an enemy concealed by darkness");
        Check(EnemyCombatTextUI.Instance.ActiveDamageNumberCount > hiddenDamageNumbersBefore,
            "Damage numbers remain visible when their enemy is concealed by darkness");
        Check(EnemyImpactEffectPool.ActiveEffectCount > hiddenImpactEffectsBefore
            && EnemyImpactEffectPool.LastPlayed != null
            && EnemyImpactEffectPool.LastPlayed.SortingOrder > RoomDarkness.WorldOverlayOrder,
            "White impact dust is spawned outside the enemy and rendered above darkness");

        float hpBefore = enemy.hp;
        int damageNumbersBefore = EnemyCombatTextUI.Instance.ActiveDamageNumberCount;
        GameObject firefly = Object.Instantiate(queue.Animals[0].Prefab,
            enemy.transform.position + Vector3.down * 1.25f, Quaternion.identity);
        firefly.GetComponent<Animal>().SetReturnTarget(player.gameObject);
        ProjectilePhysics2D flight = firefly.GetComponent<ProjectilePhysics2D>();
        if (flight == null) flight = firefly.AddComponent<ProjectilePhysics2D>();
        flight.Launch(Vector2.up, 18f);
        float hitDeadline = Time.time + 1f;
        while (!enemy.GetComponent<EnemyIllumination>().IsIlluminated && Time.time < hitDeadline) yield return null;
        yield return null;

        FireflyImpactEffect impact = firefly.GetComponent<FireflyImpactEffect>();
        EnemyIllumination illumination = enemy.GetComponent<EnemyIllumination>();
        EnemyStatusEffects statuses = enemy.GetComponent<EnemyStatusEffects>();
        float directDamage = firefly.GetComponent<Animal>().dmg;
        float expectedBurn = impact.BurnDamage;
        for (int tier = 0; tier <= AnimalProjectileQueue.MaximumBranchLevel; tier++)
        {
            impact.ConfigureBurnDamage(expectedBurn, false, tier);
            Check(Mathf.Approximately(impact.BonfireDamage, 10f * (tier + 1)),
                "Bonfire tier " + tier + " deals fixed damage independent of burn stacks");
        }
        impact.ConfigureBurnDamage(expectedBurn);
        Check(illumination.IsIlluminated && Mathf.Approximately(illumination.LightRadius, 1f),
            "A real physics collision illuminates the enemy with a one-cell radius");
        Check(Mathf.Approximately(enemy.hp, hpBefore - directDamage),
            "Collision still deals normal firefly impact damage");
        Check(statuses.HasPeriodicDamage(FireflyImpactEffect.BurnEffectId)
            && statuses.GetRemainingTurns(FireflyImpactEffect.BurnEffectId) == 1
            && Mathf.Approximately(statuses.GetPeriodicDamage(FireflyImpactEffect.BurnEffectId), expectedBurn),
            "Collision schedules one 25-damage burn tick");
        Check(EnemyCombatTextUI.Instance.ActiveDamageNumberCount > damageNumbersBefore,
            "Illumination happens before direct damage, so the impact number is visible");
        Check(!visibility.IsHidden && enemy.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.forceRenderingOff),
            "Illuminated enemy and its authored visuals become visible immediately");
        Check(darkness.IsVisible(enemy.transform.position)
            && darkness.IsVisible(enemy.transform.position + Vector3.right * 0.99f)
            && !darkness.IsVisible(enemy.transform.position + Vector3.right * 1.05f),
            "Information visibility ends at exactly one cell from the enemy");
        Check(darkness.ActiveLightCount == 1 && darkness.Overlay.LightCount == 1,
            "Room darkness receives one local light without spawning another overlay");
        Vector2 localCenter = darkness.Overlay.rectTransform.InverseTransformPoint(enemy.transform.position);
        Check(darkness.Overlay.GetAlphaAt(localCenter) < 0.001f
            && darkness.Overlay.GetAlphaAt(localCenter + Vector2.right * 0.99f) < 0.001f
            && darkness.Overlay.GetAlphaAt(localCenter + Vector2.right * 1.25f) > 0.1f,
            "Local light cuts a clear one-cell hole with a soft outer edge in the fog mesh");
        Vector2 aimed = darkness.ClipToVisibleArea(player.transform.position, enemy.transform.position);
        Check((aimed - (Vector2)enemy.transform.position).sqrMagnitude < 0.001f,
            "Aim can reach an enemy revealed by a firefly");

        impact.ApplyTo(enemy);
        Check(statuses.ActivePeriodicDamageCount == 1
            && statuses.GetStackCount(FireflyImpactEffect.BurnEffectId) == 2
            && Mathf.Approximately(statuses.GetPeriodicDamage(FireflyImpactEffect.BurnEffectId), expectedBurn * 2f),
            "Every firefly hit adds one burn stack and increases its pending damage");
        Check(illumination.IsIlluminated && Mathf.Approximately(illumination.LightRadius, 2f)
            && !visibility.IsHidden && darkness.IsVisible(enemy.transform.position),
            "A repeated firefly hit keeps the enemy visible and expands its light radius per stack");
        impact.ApplyTo(enemy);
        impact.ApplyTo(enemy);
        Check(statuses.GetStackCount(FireflyImpactEffect.BurnEffectId) == FireflyImpactEffect.MaximumBurnStacks
            && Mathf.Approximately(statuses.GetPeriodicDamage(FireflyImpactEffect.BurnEffectId), expectedBurn * 3f),
            "Firefly burn stops at three stacks even after additional impacts");
        Check(Mathf.Approximately(illumination.LightRadius, 3f),
            "Capped burn stacks also cap the enemy light radius at three cells");
        Object.Destroy(firefly);
        yield return null;
        Check(Mathf.Approximately(enemy.hp, hpBefore - directDamage),
            "Burn does not deal damage during the player's projectile phase");

        Vector3 lightBeforeMove = enemy.transform.position;
        int turnBefore = manager.TurnNumber;
        typeof(GameManager).GetMethod("ResolveEnemyTurn", Private).Invoke(manager, null);
        Check(statuses.LastProcessedTurn == turnBefore + 1
            && Mathf.Approximately(enemy.hp, hpBefore - directDamage - expectedBurn * 3f)
            && (enemy.transform.position - lightBeforeMove).sqrMagnitude < 0.001f,
            "Stacked burn ticks before the enemy starts moving");
        yield return WaitForEnemyTurn(manager);
        Check(manager.TurnNumber == turnBefore + 1 && manager.stepPlayer,
            "The real enemy phase completes and hands control back to the player");
        Check(enemy.transform.position.y < lightBeforeMove.y
            && Mathf.Approximately(enemy.hp, hpBefore - directDamage - expectedBurn * 3f),
            "Stacked burn damage is not applied a second time after enemy movement");
        Check(statuses.LastProcessedTurn == manager.TurnNumber
            && statuses.GetStackCount(FireflyImpactEffect.BurnEffectId) == 2
            && Mathf.Approximately(statuses.GetPeriodicDamage(FireflyImpactEffect.BurnEffectId), expectedBurn * 2f),
            "The first enemy turn removes only one of three burn stacks");
        Check(illumination.IsIlluminated && Mathf.Approximately(illumination.LightRadius, 3f)
            && darkness.IsVisible(enemy.transform.position),
            "Expanded enemy light persists while burn stacks decay and follows movement");
        float hpAfterBurn = enemy.hp;
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(manager.TurnNumber);
        Check(Mathf.Approximately(enemy.hp, hpAfterBurn), "The same turn cannot deal burn damage twice");
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(manager.TurnNumber + 1);
        Check(Mathf.Approximately(enemy.hp, hpAfterBurn - expectedBurn * 2f)
            && statuses.GetStackCount(FireflyImpactEffect.BurnEffectId) == 1,
            "Two remaining stacks deal damage and lose one stack on the next turn");
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(manager.TurnNumber + 2);
        Check(Mathf.Approximately(enemy.hp, hpAfterBurn - expectedBurn * 3f)
            && !statuses.HasPeriodicDamage(FireflyImpactEffect.BurnEffectId),
            "The last burn stack deals damage before expiring on the third turn");

        Brick bonfireSource = generator.EnemyCollection
            .First(gameObject => gameObject != enemy.gameObject).GetComponent<Brick>();
        bonfireSource.transform.position = enemy.transform.position + Vector3.right * 0.75f;
        bonfireSource.hp = 5f;
        EnemyStatusEffects bonfireSourceStatuses = bonfireSource.GetComponent<EnemyStatusEffects>();
        bonfireSourceStatuses.ApplyPeriodicDamage(FireflyImpactEffect.BurnEffectId, 6f, 1,
            EnemyStatusEffects.StackingMode.AddDamage);
        bonfireSource.RecordFireflyBonfireDamage(20f);
        bonfireSource.GetComponent<EnemyIllumination>().Illuminate(1f);
        int bonfiresBefore = FireflyBonfire.ActiveCount;
        int burnDeathTurn = manager.TurnNumber + 3;
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(burnDeathTurn);
        yield return null;
        FireflyBonfire bonfire = FireflyBonfire.LastSpawned;
        Check(bonfireSource == null && bonfire != null
            && FireflyBonfire.ActiveCount == bonfiresBefore + 1,
            "An enemy killed by its final firefly burn tick leaves a bonfire");
        Check(bonfire.Room == generator && Mathf.Approximately(bonfire.DamagePerTurn, 20f)
            && Mathf.Approximately(bonfire.LightRadius, 1f)
            && bonfire.RemainingTurns == 3,
            "Bonfire inherits burn damage and light radius and starts with three turns");
        ParticleSystemRenderer flameRenderer = bonfire.GetComponentInChildren<ParticleSystemRenderer>();
        Check(flameRenderer != null && flameRenderer.sortingOrder > RoomDarkness.WorldOverlayOrder
            && darkness.IsVisible(bonfire.transform.position),
            "Bonfire flames and their local light remain visible in room darkness");
        float hpBeforeBonfireTick = enemy.hp;
        int bonfireHits = FireflyBonfire.ProcessAllAfterEnemyTurn(burnDeathTurn + 1);
        yield return null;
        Check(bonfireHits >= 1 && Mathf.Approximately(enemy.hp, hpBeforeBonfireTick - 20f)
            && bonfire.RemainingTurns == 2,
            "Bonfire damages nearby enemies once after an enemy turn");
        Object.Destroy(bonfire.gameObject);
        yield return null;
        Check(FireflyBonfire.ActiveCount == bonfiresBefore,
            "Destroyed bonfires unregister their light and turn processing");
        hpAfterBurn = enemy.hp;

        statuses.ApplyPeriodicDamage("future_periodic_damage", 3f, 2,
            EnemyStatusEffects.StackingMode.Refresh);
        statuses.ApplyPeriodicDamage("future_periodic_damage", 2f, 1,
            EnemyStatusEffects.StackingMode.Refresh);
        Check(statuses.ActivePeriodicDamageCount == 1
            && statuses.GetRemainingTurns("future_periodic_damage") == 2
            && Mathf.Approximately(statuses.GetPeriodicDamage("future_periodic_damage"), 3f),
            "Reusable status API refreshes duration without weakening an existing effect");
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(manager.TurnNumber + 4);
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(manager.TurnNumber + 5);
        Check(Mathf.Approximately(enemy.hp, hpAfterBurn - 6f)
            && !statuses.HasPeriodicDamage("future_periodic_damage"),
            "Reusable periodic status deals one tick per distinct enemy turn");

        Brick doomed = generator.EnemyCollection.First(gameObject => gameObject != enemy.gameObject).GetComponent<Brick>();
        foreach (Collider2D collider in doomed.GetComponentsInChildren<Collider2D>()) collider.enabled = true;
        int roomCountBefore = generator.EnemyCollection.Count;
        int xpBefore = manager.CurrentExperience;
        int thresholdBeforeKill = manager.ExperienceForNextUpgrade;
        int killReward = doomed.ExperienceReward;
        int experienceToThreshold = Mathf.Max(0, thresholdBeforeKill - xpBefore - killReward);
        if (experienceToThreshold > 0) manager.RegisterEnemyDefeated(experienceToThreshold);
        int experienceBeforeKill = manager.CurrentExperience;
        doomed.hp = 5f;
        doomed.GetComponent<EnemyStatusEffects>().ApplyPeriodicDamage("test_lethal_burn", 10f, 1);
        manager.stepPlayer = false;
        typeof(GameManager).GetMethod("ResolveEnemyTurn", Private).Invoke(manager, null);
        Check(doomed != null && doomed.IsDefeated && !doomed.IsStepping,
            "Lethal periodic damage defeats an enemy before it can step");
        yield return WaitForEnemyTurn(manager);
        Check(doomed == null && generator.EnemyCollection.Count == roomCountBefore - 1,
            "Lethal periodic damage removes an enemy through the normal room pipeline");
        int expectedExperience = experienceBeforeKill + killReward - thresholdBeforeKill;
        Check(manager.CurrentExperience == expectedExperience && expectedExperience >= 0,
            "Enemy defeated by a status effect grants its normal experience reward");
        Check(manager.IsChoosingUpgrade && manager.stepPlayer && !Player.HasActiveProjectiles,
            "Upgrade earned by pre-movement burn opens before the next player action");
        manager.SelectProjectileUpgrade(0);
        Check(!manager.IsChoosingUpgrade && manager.stepPlayer,
            "Selecting the burn-earned upgrade resumes the pending player turn");

        Brick traveller = generator.EnemyCollection.First(gameObject => gameObject != enemy.gameObject).GetComponent<Brick>();
        LevelGenerator outerSource = manager.Front.GetGenerator(1);
        generator.EnemyCollection.Remove(traveller.gameObject);
        traveller.levelGenerator = outerSource;
        traveller.hp = 5f;
        traveller.GetComponent<EnemyStatusEffects>().ApplyPeriodicDamage("travelling_burn", 10f, 1);
        int pendingTransfersBefore = manager.Front.PendingTransferEnemies;
        int damageLabelsBefore = Object.FindObjectsOfType<EnemyCombatTextUI>(true)
            .Sum(ui => ui.ActiveDamageNumberCount);
        manager.Front.SendToInnerRoom(outerSource, traveller);
        manager.Front.DeliverPressure();
        Check(manager.Front.PendingTransferEnemies == pendingTransfersBefore
            && traveller.gameObject.activeSelf && traveller.levelGenerator == generator
            && generator.EnemyCollection.Contains(traveller.gameObject),
            "A burning enemy transfers to the inner room immediately without losing its status");
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(manager.TurnNumber + 10);
        yield return null;
        Check(traveller == null && manager.Front.PendingTransferEnemies == pendingTransfersBefore,
            "Lethal periodic damage removes an immediately transferred enemy normally");
        Check(Object.FindObjectsOfType<EnemyCombatTextUI>(true).Sum(ui => ui.ActiveDamageNumberCount) > damageLabelsBefore,
            "Status damage remains visible after an immediate room transfer");

        Check(!queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.Ammo)
            && queue.Animals[0].AmmoPerTurn == 5 && queue.GetAmmoUpgradeLevel(0) == 1,
            "Firefly keeps five projectiles without an ammunition branch");
        Check(queue.GetManualVolleyCount(0) == 1
            && Mathf.Approximately(queue.GetManualVolleyDamageMultiplier(0), 1f),
            "Firefly starts with one manually aimed full-damage volley");
        Check(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire)
            && queue.GetManualVolleyCount(0) == 2
            && Mathf.Approximately(queue.GetManualVolleyDamageMultiplier(0), 0.5f),
            "First repeat upgrade grants two manually aimed volleys at half damage");
        Check(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire)
            && queue.GetManualVolleyCount(0) == 3
            && Mathf.Approximately(queue.GetManualVolleyDamageMultiplier(0), 0.33f),
            "Second repeat upgrade grants three manually aimed volleys at thirty-three percent damage");
        Check(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire)
            && queue.GetManualVolleyCount(0) == 3
            && Mathf.Approximately(queue.GetManualVolleyDamageMultiplier(0), 0.5f)
            && !queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire),
            "Fourth firefly state caps at three manually aimed volleys with half damage");
        for (int i = 0; i < 3; i++)
            Check(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.Burn),
                "Firefly burn branch accepts level " + (i + 1));
        Check(!queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.Burn)
            && Mathf.Approximately(queue.GetBurnDamage(0), 100f),
            "Burn branch caps at three levels and raises 25-damage burn to 100 damage");
        Check(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.FireflyUltimate)
                && queue.HasFireflyUltimate(0),
            "Firefly eternal-fire ultimate unlocks after ammo, repeat and burn branches are complete");

        for (int animalIndex = 1; animalIndex < queue.Animals.Count; animalIndex++)
        {
            if (animalIndex == AnimalProjectileQueue.RabbitIndex)
            {
                Check(queue.Upgrade(animalIndex, AnimalProjectileQueue.UpgradeKind.Ammo)
                        && queue.Animals[animalIndex].AmmoPerTurn == 2,
                    "Rabbit unlock grants two projectiles");
                Check(!queue.Upgrade(animalIndex, AnimalProjectileQueue.UpgradeKind.Ammo)
                        && queue.Animals[animalIndex].AmmoPerTurn == 2
                        && !queue.GetAvailableUpgradeOptions().Any(option =>
                            option.AnimalIndex == animalIndex
                            && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo),
                    "Rabbit quantity stays at its initial pair with no quantity upgrade offers");
                continue;
            }

            for (int i = 0; i < 3; i++)
                Check(queue.Upgrade(animalIndex, AnimalProjectileQueue.UpgradeKind.Ammo),
                    "Projectile " + animalIndex + " ammunition branch accepts level " + (i + 1));
            Check(!queue.Upgrade(animalIndex, AnimalProjectileQueue.UpgradeKind.Ammo)
                && queue.Animals[animalIndex].AmmoPerTurn == 3,
                "Non-firefly projectile ammunition ends at three");
        }

        for (int i = 0; i < 3; i++)
        {
            Check(queue.Upgrade(AnimalProjectileQueue.RabbitIndex,
                    AnimalProjectileQueue.UpgradeKind.RabbitPackStrength),
                "Rabbit pack-strength branch accepts level " + (i + 1));
            Check(queue.Upgrade(AnimalProjectileQueue.RabbitIndex,
                    AnimalProjectileQueue.UpgradeKind.RabbitBreeding),
                "Rabbit breeding branch accepts level " + (i + 1));
        }
        Check(Mathf.Approximately(queue.GetRabbitPackDamagePerRabbit(
                    AnimalProjectileQueue.RabbitIndex), 3f)
                && queue.GetRabbitInitialReplicationHits(
                    AnimalProjectileQueue.RabbitIndex) == 5
                && queue.GetRabbitMaximumReplicationHits(
                    AnimalProjectileQueue.RabbitIndex) == 7,
            "Rabbit branches reach plus-three damage per rabbit and reduce the maximum reproduction threshold to seven hits");
        Check(!queue.Upgrade(AnimalProjectileQueue.RabbitIndex, AnimalProjectileQueue.UpgradeKind.RabbitUltimate),
            "Rabbit ultimate also requires the first-reproduction talent");
        for (int rank = 1; rank <= 3; rank++)
            Check(queue.Upgrade(AnimalProjectileQueue.RabbitIndex, AnimalProjectileQueue.UpgradeKind.RabbitFirstBreeding)
                && queue.GetRabbitInitialReplicationHits(AnimalProjectileQueue.RabbitIndex) == 5 - rank
                && queue.GetRabbitMaximumReplicationHits(AnimalProjectileQueue.RabbitIndex) == 7,
                "Rabbit first-reproduction rank " + rank + " lowers only its initial threshold");
        Check(queue.Upgrade(AnimalProjectileQueue.RabbitIndex,
                AnimalProjectileQueue.UpgradeKind.RabbitUltimate)
                && queue.HasRabbitUltimate(AnimalProjectileQueue.RabbitIndex),
            "Rabbit ultimate becomes selectable after all three talent branches are complete");

        for (int i = 0; i < 3; i++)
        {
            Check(queue.Upgrade(AnimalProjectileQueue.HedgehogIndex,
                    AnimalProjectileQueue.UpgradeKind.HedgehogRadius),
                "Hedgehog radius branch accepts level " + (i + 1));
            Check(queue.Upgrade(AnimalProjectileQueue.HedgehogIndex,
                    AnimalProjectileQueue.UpgradeKind.HedgehogQuillDamage),
                "Hedgehog quill-damage branch accepts level " + (i + 1));
        }
        Check(Mathf.Approximately(queue.GetExplosionRadius(AnimalProjectileQueue.HedgehogIndex), 4f)
                && Mathf.Approximately(queue.GetHedgehogQuillDamageGrowthPerHit(
                    AnimalProjectileQueue.HedgehogIndex), 3f),
            "Hedgehog branches reach radius four and plus-three quill damage");
        Check(queue.Upgrade(AnimalProjectileQueue.HedgehogIndex,
                AnimalProjectileQueue.UpgradeKind.HedgehogUltimate)
                && queue.HasHedgehogUltimate(AnimalProjectileQueue.HedgehogIndex),
            "Hedgehog ultimate becomes selectable after ammo, radius and damage branches are complete");

        for (int i = 0; i < 3; i++)
        {
            Check(queue.Upgrade(AnimalProjectileQueue.HoneyBadgerIndex,
                    AnimalProjectileQueue.UpgradeKind.HoneyBadgerRadius),
                "Honey badger radius branch accepts level " + (i + 1));
            Check(queue.Upgrade(AnimalProjectileQueue.HoneyBadgerIndex,
                    AnimalProjectileQueue.UpgradeKind.HoneyBadgerChain),
                "Honey badger chain branch accepts level " + (i + 1));
        }
        Check(Mathf.Approximately(queue.GetHoneyBadgerAttackRadius(3), 4f)
                && queue.GetHoneyBadgerMaximumAttacks(3) == 11
                && queue.GetHoneyBadgerConsecutiveAttacks(3) == 4,
            "Honey badger branches reach radius four and the eleven/four attack profile");
        Check(queue.Upgrade(AnimalProjectileQueue.HoneyBadgerIndex,
                AnimalProjectileQueue.UpgradeKind.HoneyBadgerUltimate)
                && queue.HasHoneyBadgerUltimate(AnimalProjectileQueue.HoneyBadgerIndex),
            "Honey badger ultimate becomes selectable after ammo, radius and chain branches are complete");

        for (int i = 0; i < 3; i++)
        {
            Check(queue.Upgrade(AnimalProjectileQueue.BoarIndex,
                    AnimalProjectileQueue.UpgradeKind.BoarBounces),
                "Boar bounce branch accepts level " + (i + 1));
            Check(queue.Upgrade(AnimalProjectileQueue.BoarIndex,
                    AnimalProjectileQueue.UpgradeKind.BoarFirstHit),
                "Boar first-hit branch accepts level " + (i + 1));
        }
        Check(queue.GetBoarMaximumBounces(AnimalProjectileQueue.BoarIndex) == 16
                && Mathf.Approximately(queue.GetBoarFirstHitDamageMultiplier(
                    AnimalProjectileQueue.BoarIndex), 4f),
            "Boar branches reach sixteen paths and four-hundred-percent first hits");
        Check(queue.Upgrade(AnimalProjectileQueue.BoarIndex,
                AnimalProjectileQueue.UpgradeKind.BoarUltimate)
                && queue.HasBoarUltimate(AnimalProjectileQueue.BoarIndex),
            "Boar ultimate becomes selectable after ammo, bounce and first-hit branches are complete");

        List<AnimalProjectileQueue.UpgradeOption> fallback = queue.GetAvailableUpgradeOptions();
        Check(fallback.Count == 0,
            "Exhausting unique branches leaves no generic damage fallback");
        Check(!queue.Upgrade(-1, AnimalProjectileQueue.UpgradeKind.GlobalDamage),
            "Removed generic damage cannot be selected after exhausting branches");

    }

    private static IEnumerator WaitForEnemyTurn(GameManager manager)
    {
        float timeout = Time.realtimeSinceStartup + 3f;
        while (manager != null && manager.IsResolvingEnemyTurn
            && Time.realtimeSinceStartup < timeout)
            yield return null;
        if (manager != null && manager.IsResolvingEnemyTurn)
            throw new Exception("Enemy movement animation did not finish before the turn timeout");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Firefly validation timed out");
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

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
        report.Add("PASS " + description);
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
        string result = error == null ? "PASS: " + checks + " firefly integration checks." : "FAIL: " + error;
        report.Add(result);
        File.WriteAllLines("Logs/FireflyMechanicsValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
