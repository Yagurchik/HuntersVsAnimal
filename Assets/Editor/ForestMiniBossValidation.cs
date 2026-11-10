using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ForestMiniBossValidation
{
    private const string Running = "ForestMiniBossValidation.Running";
    private static IEnumerator checks;
    private static readonly List<string> report = new List<string>();
    private static double deadline;

    static ForestMiniBossValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            checks = CheckGameplay();
            deadline = EditorApplication.timeSinceStartup + 40d;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run only in an isolated validation project.");
        Directory.CreateDirectory("Logs");
        report.Clear();
        GameRunMode.SetDeveloperMode(false);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(3);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckGameplay()
    {
        for (int i = 0; i < 4; i++) yield return null;
        GameManager manager = GameManager.Instance;
        Check(manager != null && manager.Front != null, "Campaign scene opened");
        manager.enabled = false;
        foreach (Player player in UnityEngine.Object.FindObjectsOfType<Player>(true))
            player.enabled = false;
        foreach (FirstLevelTutorial tutorial in UnityEngine.Object.FindObjectsOfType<FirstLevelTutorial>(true))
            tutorial.enabled = false;
        Check(LevelProgression.Instance.CurrentLevel == 3, "Forest level 3 is active");
        for (int line = 2; line <= 4; line++) manager.Front.ActivateLine(line);
        for (int i = 0; i < 2; i++) yield return null;

        LevelGenerator room = manager.Front.GetGenerator(DefenseFront.IdFor(4, 4));
        Check(room != null && room.IsInitialized && room.IsCampaignBossRoom,
            "Outer central boss room is initialized");
        Brick hive = room.EnemyCollection.Where(value => value != null)
            .Select(value => value.GetComponent<Brick>())
            .Single(value => value != null && value.UsesBossHealth);
        ForestMiniBossAbility ability = hive.GetComponent<ForestMiniBossAbility>();
        Check(hive.name.StartsWith("HiveHollowMiniBoss") && ability != null,
            "Hive Hollow spawns in forest level 3 room 4-4");

        GameObject testProjectile = new GameObject("Forest mini-boss test projectile");
        Animal projectile = testProjectile.AddComponent<Animal>();
        projectile.enabled = false;
        for (int i = 0; i < 3; i++)
            ability.ModifyDamage(hive, projectile, hive.transform.position, 100f);
        for (int i = 0; i < 2; i++) yield return null;
        Check(Bees(room).Length == 2, "Third direct hit releases two editable bee enemies");
        Check(Bees(room).All(bee => bee.ExperienceReward == 0
            && bee.MaxHp < room.OrdinaryEnemyBaseHealth),
            "Bee summons are fragile and award no experience");

        for (int i = 0; i < 3; i++)
            ability.ModifyDamage(hive, projectile, hive.transform.position, 100f);
        for (int i = 0; i < 2; i++) yield return null;
        Check(Bees(room).Length == 2, "Hive cannot exceed its two-bee cap");

        GameObject judgeObject = UnityEngine.Object.Instantiate(
            Resources.Load<GameObject>("Enemies/GreenJudgeMiniBoss"));
        Brick judge = judgeObject.GetComponent<Brick>();
        ForestMiniBossAbility judgeAbility = judgeObject.GetComponent<ForestMiniBossAbility>();
        Check(judgeAbility != null && judgeAbility.LeftEyeOpen,
            "Green Judge begins with the left eye open");
        Vector2 center = judge.transform.position;
        float left = judgeAbility.ModifyDamage(judge, projectile, center + Vector2.left, 100f);
        float right = judgeAbility.ModifyDamage(judge, projectile, center + Vector2.right, 100f);
        Check(Mathf.Approximately(left, 150f) && Mathf.Approximately(right, 100f),
            "The lit eye side receives 50 percent extra direct damage");
        judgeAbility.CompleteEnemyTurn(1);
        left = judgeAbility.ModifyDamage(judge, projectile, center + Vector2.left, 100f);
        right = judgeAbility.ModifyDamage(judge, projectile, center + Vector2.right, 100f);
        Check(!judgeAbility.LeftEyeOpen && Mathf.Approximately(left, 100f)
            && Mathf.Approximately(right, 150f)
            && judgeObject.GetComponentInChildren<SpriteRenderer>().flipX,
            "Judge switches its vulnerable eye and sprite side after one turn");
        judgeAbility.CompleteEnemyTurn(1);
        Check(!judgeAbility.LeftEyeOpen, "Judge cannot switch twice in one turn");
    }

    private static Brick[] Bees(LevelGenerator room) => room.EnemyCollection
        .Where(value => value != null && value.name.StartsWith("HiveBee"))
        .Select(value => value.GetComponent<Brick>())
        .Where(value => value != null && !value.IsDefeated).ToArray();

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
                throw new TimeoutException("Forest mini-boss validation timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        report.Add(error == null ? "FOREST_MINI_BOSSES_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/ForestMiniBossValidation.txt", report);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
