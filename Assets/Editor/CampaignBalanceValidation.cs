using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run only in the isolated validation project: the play checks use empty saves.
[InitializeOnLoad]
public static class CampaignBalanceValidation
{
    private const string Running = "ForestAnimalWar.CampaignBalanceValidation.Running";
    private const string ReportPath = "Logs/CampaignBalanceValidation.txt";
    private const string RemovedExperienceLevelKey = "ForestAnimalWar.PermanentExperience.Level";
    private static readonly List<string> Report = new List<string>();
    private static double deadline;
    private static double readyAfter;

    static CampaignBalanceValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Report.Clear();
            Report.AddRange(File.ReadAllLines(ReportPath));
            deadline = EditorApplication.timeSinceStartup + 90;
            readyAfter = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation"
            || Application.productName != "CampaignBalanceValidation")
            throw new InvalidOperationException("Use the isolated CampaignBalanceValidation project.");
        try
        {
            Directory.CreateDirectory("Logs");
            Report.Clear();
            EditorSceneManager.OpenScene("Assets/Asset.unity");
            LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
            Require(progression != null, "Authored gameplay scene has progression settings");
            ValidateCurves(progression);
            ValidateEconomy();
            ValidateBosses(progression);
            File.WriteAllLines(ReportPath, Report);

            // These PlayerPrefs belong exclusively to this temporary test product.
            PlayerPrefs.DeleteAll();
            // Old purchases must remain inert even when a player's save still
            // contains a completed experience tree.
            PlayerPrefs.SetInt(RemovedExperienceLevelKey, 100);
            PlayerPrefs.SetString("ForestAnimalWar.PermanentExperience.Progress", "1000000000");
            GameRunMode.SetDeveloperMode(false);
            GameRunMode.PrepareCampaign();
            LevelProgression.PrepareLevel(LevelProgression.MaxLevel);
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(error); }
    }

    public static void RunHealthTableBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new InvalidOperationException("Use the isolated validation project.");
        try
        {
            Report.Clear();
            const string menu = "Assets/Scenes/UIscene.unity";
            byte[] before = File.ReadAllBytes(menu);
            EditorSceneManager.OpenScene(menu);
            MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
            Require(HeroEquipmentAuthoring.HasCurrentLayout(hub), "Saved menu keeps six authored equipment slots");
            Require(VillageEntityWindowAuthoring.HasCurrentLayout(hub), "Saved menu keeps the complete village layout");
            var bossHealth = new Dictionary<string, float>();
            foreach (string path in Directory.GetFiles("Assets/Resources/Enemies", "*.prefab"))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Brick enemy = prefab.GetComponent<Brick>();
                if (enemy != null && enemy.UsesBossHealth)
                    bossHealth[prefab.GetComponent<EnemyFirstEncounterProfile>().DisplayName] = enemy.hp;
            }
            int count = 0;
            foreach (BestiaryEntryView entry in Object.FindObjectsOfType<BestiaryEntryView>(true))
                if (entry.Category == BestiaryEntryView.Section.Enemies && bossHealth.TryGetValue(entry.Title, out float hp))
                {
                    Require(entry.Body.Contains("2-я линия: ×20\n") && entry.Body.Contains("3-я линия: ×30\n")
                        && entry.Body.Contains("4-я линия: ×40"), "Authored boss health rule: " + entry.Title);
                    count++;
                }
            Require(count >= 16, "All current boss health rules are represented in the saved menu");
            Require(before.SequenceEqual(File.ReadAllBytes(menu)), "Validation leaves the authored menu file unchanged");
            Directory.CreateDirectory("Logs");
            Report.Add("RESULT: PASS (" + Report.Count + " checks)");
            File.WriteAllLines("Logs/EnemyHealthTableValidation.txt", Report);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/EnemyHealthTableValidation.txt", error.ToString());
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    private static void ValidateCurves(LevelProgression progression)
    {
        Require(Mathf.Approximately(progression.GetEnemyHealthMultiplier(1), 1f),
            "First-level health retains its authored base value");
        for (int level = 1; level <= LevelProgression.MaxLevel; level++)
        {
            float health = progression.GetEnemyHealthMultiplier(level);
            Require(Mathf.Approximately(health, (float)Math.Pow(1d + (level - 1) / 8d, 1.9d)),
                "Level " + level + " matches the approved diminishing HP table");
            for (int wave = 1; wave <= LevelProgression.CampaignWavesPerRoom; wave++)
            {
                float[] originalWaves = { 1f, 1.25f, 1.5f, 1.75f, 2f };
                Require(Mathf.Approximately(LevelProgression.GetCampaignWaveHealthMultiplier(wave), originalWaves[wave - 1]),
                    "Level " + level + " wave " + wave + " uses its approved five-wave HP multiplier");
            }
        }
        Require(progression.GetEnemyHealthMultiplier(19) * 100f < 1000f
            && progression.GetEnemyHealthMultiplier(20) * 100f > 1000f,
            "Base first-wave HP crosses 1000 only at level twenty");
        Require(Mathf.Approximately(progression.GetEnemyHealthMultiplier(30), (float)Math.Pow(1d + 29d / 8d, 1.9d)),
            "Final-level base HP is 1835.32");
        Require(Mathf.Approximately(LevelProgression.GetCampaignWaveHealthMultiplier(5), 2f)
            && Mathf.Approximately(LevelProgression.GetCampaignWaveHealthMultiplier(6), 2f)
            && Mathf.Approximately(LevelProgression.GetCampaignWaveHealthMultiplier(-1), 1f),
            "Five-wave HP bounds and invalid wave handling are correct");
    }

    private static void ValidateEconomy()
    {
        GameObject root = new GameObject("Balance XP validation");
        try
        {
            ExperienceEconomy economy = root.AddComponent<ExperienceEconomy>();
            Require(economy.ExperienceForNextUpgrade == 30, "First upgrade costs three ordinary kills");
            economy.AddExperience(20);
            economy.GrantUpgradeLevels(3);
            Require(economy.CurrentExperience == 20 && economy.ExperienceForNextUpgrade == 30
                && economy.EarnedUpgrades == 3,
                "Free line rewards retain XP and do not raise the kill-XP price");
            Require(economy.AddExperience(10) == 1 && economy.CurrentExperience == 0
                && economy.ExperienceForNextUpgrade == 34,
                "Paid XP upgrades add four to the next price");
            int earned = economy.AddExperience(10000);
            Require(earned > 100 && economy.ExperienceForNextUpgrade == 90,
                "Long runs keep earning upgrades at a maximum price of ninety XP");
        }
        finally { Object.DestroyImmediate(root); }

        ExperienceEconomy authored = Object.FindObjectOfType<ExperienceEconomy>(true);
        Require(authored != null && authored.ExperienceForNextUpgrade == 30,
            "Gameplay scene uses the new XP prices");
    }

    private static void ValidateBosses(LevelProgression progression)
    {
        var originalHealth = new Dictionary<string, float> {
            { "BellRingerBoss", 2600f }, { "KnotBinderBoss", 2600f },
            { "VolleyEaterBoss", 2600f }, { "ThreeSealIdolBoss", 2600f }, { "ImpostorBoss", 2600f },
            { "GreenJudgeMiniBoss", 2800f }, { "HiveHollowMiniBoss", 3000f }, { "LichBoss", 4440f },
            { "BoneDragonMiniBoss", 5550f }, { "MirageBoss", 5550f }, { "DrownedKing", 5550f },
            { "BoneDragonBoss", 11100f }, { "GhostlySorrowBoss", 11100f }, { "PoisonousGloomBoss", 11100f },
            { "DrownedMiniBoss", 1387.5f }, { "DrownedHeir", 1387.5f }
        };
        int bossCount = 0;
        foreach (string path in Directory.GetFiles("Assets/Resources/Enemies", "*.prefab"))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Brick boss = prefab != null ? prefab.GetComponent<Brick>() : null;
            if (boss == null || !boss.UsesBossHealth) continue;
            Require(originalHealth.TryGetValue(prefab.name, out float expected)
                && Mathf.Approximately(boss.hp, expected), prefab.name + " restores its exact pre-balance HP");
            bossCount++;
        }
        Require(bossCount == originalHealth.Count, "All sixteen original boss HP values are covered");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Play validation timed out");
            if (EditorApplication.timeSinceStartup < readyAfter) return;
            GameManager manager = GameManager.Instance;
            AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
            LevelProgression progression = LevelProgression.Instance;
            if (manager == null || queue == null || progression == null) return;
            LevelGenerator room = manager.rooms.FirstOrDefault(value => value != null && value.IsInitialized);
            if (room == null) return;
            EditorApplication.update -= Tick;
            ValidateRuntime(queue, progression, room);
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void ValidateRuntime(AnimalProjectileQueue queue, LevelProgression progression, LevelGenerator room)
    {
        Require(progression.CurrentLevel == 30 && PlayerPrefs.GetInt(RemovedExperienceLevelKey) == 100
            && Mathf.Approximately(MetaProgression.PumpkinProjectileDamageMultiplier, 1f),
            "Final campaign level starts with obsolete XP purchases present and no equipped bonuses");
        DefenseLineRuntime front = GameManager.Instance.Front;
        float authoredBarricadeHealth = new SerializedObject(front).FindProperty("startingBarricadeHealth").floatValue;
        Require(Mathf.Approximately(front.BarricadeMaxHealth, authoredBarricadeHealth),
            "A completed obsolete experience tree grants no barricade HP");
        Stage stage = room.GetComponentInParent<Stage>();
        float damLimit = (float)typeof(BeaverDam).GetMethod("ResolveHealthLimit", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { stage });
        Require(Mathf.Approximately(damLimit, authoredBarricadeHealth * 0.25f),
            "A completed obsolete experience tree grants no extra beaver dam capacity");
        Player launcher = stage.PlayerObject.GetComponent<Player>();
        GameObject shotPrefab = queue.Animals[0].Prefab;
        typeof(Player).GetMethod("LaunchProjectileSingle", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(launcher, new object[] { Vector2.up, shotPrefab, 0, 0.5f });
        Animal shot = Player.animalsCollect.Last(value => value != null);
        Require(Mathf.Approximately(shot.dmg, shotPrefab.GetComponent<Animal>().dmg * 0.5f),
            "Real projectile launch keeps volley scaling and ignores obsolete experience damage bonuses");
        Object.Destroy(shot.gameObject);
        Require(queue.SelectedUpgradeCount == 0,
            "Authored starter ammo does not count as selected upgrades");
        Require(!queue.GetAvailableUpgradeOptions().Any(option => option.Kind == AnimalProjectileQueue.UpgradeKind.GlobalDamage),
            "Generic damage is absent from available upgrades");
        FieldInfo loadoutField = typeof(AnimalProjectileQueue).GetField("selectedForRun", BindingFlags.Instance | BindingFlags.NonPublic);
        object loadout = loadoutField.GetValue(queue);
        loadoutField.SetValue(queue, new bool[0]);
        try { Require(!queue.IsAnimalAllowedForRun(0), "Transient deserialization of an empty loadout cannot index outside its array"); }
        finally { loadoutField.SetValue(queue, loadout); }
        Require(!queue.Upgrade(-1, AnimalProjectileQueue.UpgradeKind.GlobalDamage),
            "Obsolete generic damage requests are rejected");
        Require(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.Burn),
            "A specialized upgrade is still accepted");
        int selected = queue.SelectedUpgradeCount;
        Require(!queue.Upgrade(-1, AnimalProjectileQueue.UpgradeKind.Burn)
            && queue.SelectedUpgradeCount == selected, "Rejected upgrades do not grant free damage");

        ExperienceAndUpgradeUI ui = Object.FindObjectOfType<ExperienceAndUpgradeUI>(true);
        MethodInfo offers = typeof(ExperienceAndUpgradeUI).GetMethod("SelectOffers", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < 50; i++)
        {
            var selectedOffers = (List<AnimalProjectileQueue.UpgradeOption>)offers.Invoke(ui,
                new object[] { queue.GetAvailableUpgradeOptions(), true, queue });
            Require(!selectedOffers.Any(option => option.Kind == AnimalProjectileQueue.UpgradeKind.GlobalDamage),
                "Random offer " + i + " contains no removed generic damage choice");
        }
        queue.ForceNextUpgradeOption(0, AnimalProjectileQueue.UpgradeKind.Burn);
        Require(queue.GetAvailableUpgradeOptions().Count == 1, "Tutorial forced choices remain exclusive");
        queue.ClearForcedUpgradeOption();

        GameObject targetObject = Object.Instantiate(room.brickPrefab, new Vector3(100f, 100f), Quaternion.identity, room.transform);
        Brick target = targetObject.GetComponent<Brick>();
        target.levelGenerator = room;
        Require(Mathf.Approximately(target.MaxHp, room.OrdinaryEnemyBaseHealth * (float)Math.Pow(1d + 29d / 8d, 1.9d)),
            "Actual final-level spawn applies the approved diminishing HP curve");
        float contactDamage = target.ProjectileContactDamage;
        target.ApplyCampaignWaveHealth(LevelProgression.GetCampaignWaveHealthMultiplier(4));
        Require(Mathf.Approximately(target.MaxHp, room.OrdinaryEnemyBaseHealth * (float)Math.Pow(1d + 29d / 8d, 1.9d) * 1.75f)
            && Mathf.Approximately(target.ProjectileContactDamage, contactDamage),
            "Wave HP scaling does not increase enemy contact damage");
        target.ConfigureSummonedVariant(0, Color.white);
        // Neither the campaign level nor the number of run upgrades scales damage.
        const float multiplier = 1f;
        foreach (CombatDamageSource source in new[] { CombatDamageSource.Firefly, CombatDamageSource.Hedgehog,
            CombatDamageSource.Rabbit, CombatDamageSource.HoneyBadger, CombatDamageSource.Boar,
            CombatDamageSource.Beaver, CombatDamageSource.Frog, CombatDamageSource.Squirrel,
            CombatDamageSource.AcornExplosion })
        {
            target.SetSummonedHealth(10000f);
            target.TakeProjectileDamage(100f, 0, source);
            Require(Mathf.Abs(10000f - target.hp - 100f * multiplier) < 0.02f,
                source + " receives no campaign or run damage growth");
        }
        foreach (CombatDamageSource source in new[] { CombatDamageSource.Fire, CombatDamageSource.Poison })
        {
            target.SetSummonedHealth(10000f);
            target.TakeAfterTurnDamage(25f, source, 0);
            Require(Mathf.Abs(10000f - target.hp - 25f * multiplier) < 0.02f,
                source + " periodic damage keeps pace with direct damage");
        }
        target.SetSummonedHealth(10000f);
        FireflyImpactEffect burn = Object.Instantiate(queue.Animals[0].Prefab).GetComponent<FireflyImpactEffect>();
        burn.ConfigureBurnDamage(25f);
        burn.ApplyTo(target);
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(10000);
        Require(Mathf.Abs(10000f - target.hp - 25f * multiplier) < 0.02f,
            "An actual firefly status tick applies the same progression once");
        Object.Destroy(burn.gameObject);
        target.SetSummonedHealth(10000f);
        target.TakeFrogDamage(15f, 0, true);
        Require(Mathf.Abs(10000f - target.hp - 15f * multiplier) < 0.02f,
            "A seated frog bite applies progression at resolution");
        target.SetSummonedHealth(10000f);
        target.TakeAfterTurnDamage(25f, CombatDamageSource.Poison, 0, true);
        Require(Mathf.Approximately(10000f - target.hp, 25f),
            "Precomputed artifact poison does not apply progression twice");
        target.SetSummonedHealth(10000f);
        FieldInfo secondary = typeof(ArtifactCombatEffects).GetField("secondaryDepth", BindingFlags.Static | BindingFlags.NonPublic);
        secondary.SetValue(null, 1);
        try { target.TakeProjectileDamage(25f, 0, CombatDamageSource.Hedgehog); }
        finally { secondary.SetValue(null, 0); }
        Require(Mathf.Approximately(10000f - target.hp, 25f),
            "Artifact echo and splash do not multiply their inherited damage again");
        Object.Destroy(targetObject);

        queue.ClearForcedUpgradeOption();
        int exhaustedChoices = 0;
        while (queue.GetAvailableUpgradeOptions().Count > 0 && exhaustedChoices++ < 512)
        {
            AnimalProjectileQueue.UpgradeOption option = queue.GetAvailableUpgradeOptions()[0];
            Require(option.Kind != AnimalProjectileQueue.UpgradeKind.GlobalDamage
                && queue.Upgrade(option.AnimalIndex, option.Kind), "Finite branch choice " + exhaustedChoices);
        }
        Require(queue.GetAvailableUpgradeOptions().Count == 0 && exhaustedChoices < 512,
            "All unique branches exhaust without a generic damage fallback");
        Require(!queue.Upgrade(-1, AnimalProjectileQueue.UpgradeKind.GlobalDamage),
            "Removed generic damage is rejected after all branches are complete");
        GameManager exhaustedManager = GameManager.Instance;
        var privateFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(GameManager).GetField("pendingProjectileUpgrades", privateFlags).SetValue(exhaustedManager, 3);
        typeof(GameManager).GetField("enemyTurnPendingAfterUpgrade", privateFlags).SetValue(exhaustedManager, true);
        typeof(GameManager).GetProperty("IsChoosingUpgrade").SetValue(exhaustedManager, true);
        exhaustedManager.isPaused = false;
        exhaustedManager.stepGame = false;
        Time.timeScale = 0f;
        typeof(GameManager).GetMethod("CompleteProjectileUpgradeSelection", privateFlags).Invoke(exhaustedManager, null);
        Require(!exhaustedManager.IsChoosingUpgrade && Time.timeScale > 0f && exhaustedManager.stepGame
            && (int)typeof(GameManager).GetField("pendingProjectileUpgrades", privateFlags).GetValue(exhaustedManager) == 0,
            "Exhaustion with multiple pending levels closes the modal and resumes the enemy turn");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Report.Add("PASS: " + message);
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Report.Add(error == null ? "RESULT: PASS (" + Report.Count + " checks)" : "RESULT: FAIL " + error);
        File.WriteAllLines(ReportPath, Report);
        if (error != null) Debug.LogException(error);
        else Debug.Log(Report[Report.Count - 1]);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
