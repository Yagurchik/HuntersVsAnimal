using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ExperienceEconomyValidation
{
    private const string GameplayScenePath = "Assets/Asset.unity";

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");

        Directory.CreateDirectory("Logs");
        var report = new List<string>();
        GameObject testObject = null;

        try
        {
            EditorSceneManager.OpenScene(GameplayScenePath);
            ExperienceEconomy authoredEconomy = Object.FindObjectOfType<ExperienceEconomy>(true);
            Check(authoredEconomy != null && authoredEconomy.ExperienceForNextUpgrade == 30,
                "Gameplay scene starts the first upgrade at 30 XP", report);
            GameManager authoredManager = Object.FindObjectOfType<GameManager>(true);
            authoredManager.RegisterEnemyDefeated(35);
            authoredManager.RegisterEnemyDefeated(10);
            authoredManager.RegisterLineCleared(1);
            Check(authoredManager.PlayerUpgradeLevel == 3
                    && authoredManager.PendingProjectileUpgrades == 2
                    && authoredManager.CurrentExperience == 15
                    && authoredManager.ExperienceForNextUpgrade == 34,
                "A threshold-crossing final kill and its line clear grant two queued levels while preserving excess XP",
                report);
            authoredManager.RegisterLineCleared(1);
            Check(authoredManager.PlayerUpgradeLevel == 3 && authoredManager.PendingProjectileUpgrades == 2,
                "A duplicate line reward does not grant another level", report);
            Check(GameManager.ShouldFinishCampaignWithoutUpgrade(false, 4, 4, true, true),
                "A quiet final campaign line finishes without an upgrade choice", report);
            Check(!GameManager.ShouldFinishCampaignWithoutUpgrade(false, 3, 4, true, true)
                    && !GameManager.ShouldFinishCampaignWithoutUpgrade(false, 4, 4, true, false)
                    && !GameManager.ShouldFinishCampaignWithoutUpgrade(false, 4, 4, false, true)
                    && !GameManager.ShouldFinishCampaignWithoutUpgrade(true, 4, 4, true, true),
                "Earlier lines, active threats and survival retain normal upgrade flow", report);

            testObject = new GameObject("Experience Economy Validation");
            ExperienceEconomy economy = testObject.AddComponent<ExperienceEconomy>();
            Check(economy.ExperienceForNextUpgrade == 30,
                "New experience economies use the 30 XP default", report);
            Check(economy.AddExperience(29) == 0 && economy.CurrentExperience == 29,
                "29 XP does not unlock the first upgrade", report);
            Check(economy.AddExperience(1) == 1 && economy.CurrentExperience == 0
                && economy.ExperienceForNextUpgrade == 34,
                "The first upgrade consumes 30 XP and raises the next price to 34", report);
            Check(economy.AddExperience(33) == 0 && economy.CurrentExperience == 33,
                "33 XP does not unlock the second upgrade", report);
            Check(economy.AddExperience(1) == 1 && economy.CurrentExperience == 0
                && economy.ExperienceForNextUpgrade == 38,
                "The second upgrade consumes 34 XP and raises the next price to 38", report);
            Check(economy.AddExperience(80) == 2 && economy.CurrentExperience == 0
                && economy.ExperienceForNextUpgrade == 46,
                "Large rewards correctly cross the 38 and 42 XP thresholds", report);
            int thresholdBeforeLineClear = economy.ExperienceForNextUpgrade;
            economy.AddExperience(thresholdBeforeLineClear - 1);
            Check(economy.CurrentExperience == thresholdBeforeLineClear - 1
                    && economy.ExperienceForNextUpgrade == thresholdBeforeLineClear,
                "Experience can remain just below the next threshold", report);
            economy.GrantUpgradeLevels(1);
            Check(economy.CurrentExperience == thresholdBeforeLineClear - 1
                    && economy.ExperienceForNextUpgrade == thresholdBeforeLineClear,
                "A guaranteed line-clear level preserves XP and its threshold",
                report);

            CheckReward("Assets/Enemy/Brick.prefab", 10, "basic enemy", report);
            CheckReward("Assets/Resources/Enemies/Zombie.prefab", 20, "zombie", report);
            CheckReward("Assets/Resources/Enemies/ArmoredSkeleton.prefab", 20,
                "armored skeleton", report);
            CheckReward("Assets/Resources/Enemies/LichRegular.prefab", 30,
                "ordinary Lich", report);
            CheckReward("Assets/Resources/Enemies/LichBoss.prefab", 80, "Lich boss", report);
            CheckReward("Assets/Resources/Enemies/BoneDragonMiniBoss.prefab", 100,
                "mini-boss", report);
            CheckReward("Assets/Resources/Enemies/BoneDragonBoss.prefab", 200,
                "final boss", report);
            CheckReward("Assets/Resources/Enemies/GhostlySorrowBoss.prefab", 200,
                "Ghostly Sorrow boss", report);
            CheckReward("Assets/Resources/Enemies/PoisonousGloomBoss.prefab", 200,
                "Poisonous Gloom boss", report);
            CheckReward("Assets/Resources/Enemies/DrownedMiniBoss.prefab", 35,
                "Drowned mini-boss", report);

            report.Add("PASS: " + report.Count + " experience economy checks.");
            File.WriteAllLines("Logs/ExperienceEconomyValidation.txt", report);
            Debug.Log(report[report.Count - 1]);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            report.Add("FAIL: " + error);
            File.WriteAllLines("Logs/ExperienceEconomyValidation.txt", report);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
        finally
        {
            if (testObject != null) Object.DestroyImmediate(testObject);
        }
    }

    private static void CheckReward(string path, int expected, string enemyName, List<string> report)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Brick enemy = prefab != null ? prefab.GetComponent<Brick>() : null;
        Check(enemy != null && enemy.ExperienceReward == expected,
            enemyName + " awards " + expected + " XP", report);
    }

    private static void Check(bool condition, string description, List<string> report)
    {
        if (!condition) throw new Exception(description);
        report.Add("PASS " + description);
    }
}
