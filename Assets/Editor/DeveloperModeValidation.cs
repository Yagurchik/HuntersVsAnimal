using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run in the existing isolated Unity project without -quit.
[InitializeOnLoad]
public static class DeveloperModeValidation
{
    private const string BatchKey = "ForestAnimalWar.DeveloperModeValidation.Running";
    private const string MenuScene = "Assets/Scenes/UIscene.unity";
    private const string GameScene = "Assets/Asset.unity";
    private static int readyFrame;
    private static double deadline;

    static DeveloperModeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(BatchKey, false)) return;
            readyFrame = Time.frameCount + 2;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += ValidateGameplayWhenReady;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated Unity project copy.");
        try
        {
            EditorSceneManager.OpenScene(MenuScene);
            MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
            Check(hub != null && hub.settingsModal != null,
                "Authored settings dialog is present");
            Transform toggle = hub.settingsModal.transform.Find("Panel/Toggle Developer Mode");
            Check(toggle != null && toggle.GetComponent<Button>() != null
                && hub.developerModeButtonText != null,
                "Developer toggle is authored and linked in UIscene");
            Check(toggle.GetComponent<Button>().onClick.GetPersistentEventCount() == 1
                && toggle.GetComponent<Button>().onClick.GetPersistentMethodName(0)
                    == nameof(MenuHubController.ToggleDeveloperMode),
                "Authored toggle invokes the menu controller");

            PlayerPrefs.SetInt("ForestAnimalWar.HighestUnlockedLevel", 1);
            PlayerPrefs.SetInt("ForestAnimalWar.CurrentLevel", 1);
            PlayerPrefs.SetInt("ForestAnimalWar.MenuSelectedLevel", 1);
            GameRunMode.SetDeveloperMode(false);
            Check(!LevelProgression.SelectMenuLevel(5), "Normal save locks level 5");
            GameRunMode.SetDeveloperMode(true);
            Check(LevelProgression.SelectMenuLevel(5)
                && LevelProgression.SelectedMenuLevel == 5,
                "Developer toggle unlocks level selection");
            GameRunMode.SetDeveloperMode(false);
            Check(LevelProgression.SelectedMenuLevel == 1,
                "Disabling developer mode restores the real unlock boundary");
            GameRunMode.SetDeveloperMode(true);
            GameRunMode.PrepareCampaign();
            LevelProgression.PrepareSelectedLevel();
            SessionState.SetBool(BatchKey, true);
            EditorSceneManager.OpenScene(GameScene);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static void ValidateGameplayWhenReady()
    {
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Finish(new InvalidOperationException("Gameplay did not become ready."));
            return;
        }
        GameManager manager = GameManager.Instance;
        if (Time.frameCount < readyFrame || manager == null || manager.Front == null
            || !manager.Front.IsReady || manager.Front.Model.OpenedLine != 4) return;
        EditorApplication.update -= ValidateGameplayWhenReady;
        try
        {
            Check(GameRunMode.IsDeveloperRun && !manager.IsSurvivalMode,
                "Run is developer campaign, not survival");
            Check(LevelProgression.Instance != null
                && LevelProgression.Instance.CurrentLevel == 5,
                "Locked fifth level launched");
            Check(manager.stage == 4 && manager.Front.Model.Rooms.Count == 16
                && manager.Front.Model.Rooms.All(room => room.CanSelect),
                "All sixteen campaign rooms are selectable immediately");
            Check(manager.Front.Model.Rooms.All(room =>
                manager.Front.GetGenerator(room.Id).IsInitialized),
                "All rooms have initialized battles");
            Check(manager.Front.FocusRoom(DefenseFront.IdFor(4, 4), true),
                "Player can enter the previously locked dragon room");

            LevelGenerator dragonRoom = manager.Front.GetGenerator(DefenseFront.IdFor(4, 4));
            BossEnemyTurnController boss = dragonRoom.EnemyCollection
                .Select(enemy => enemy != null ? enemy.GetComponent<BossEnemyTurnController>() : null)
                .FirstOrDefault(controller => controller != null);
            Check(boss != null, "Dragon boss is present in room 4-4");
            // Both dragon prefabs are authored to summon every third turn,
            // without a first-turn summon.
            boss.CompleteEnemyTurn(1);
            boss.CompleteEnemyTurn(2);
            boss.CompleteEnemyTurn(3);
            Brick[] summoned = dragonRoom.EnemyCollection
                .Select(enemy => enemy != null ? enemy.GetComponent<Brick>() : null)
                .Where(brick => brick != null && brick.IsSummonedVariant).ToArray();
            Check(summoned.Length > 0 && summoned.Length == boss.LastSummonCount,
                "Dragon summoned a full skeleton line");
            Check(summoned.All(brick => Mathf.Approximately(brick.hp, 1f)
                && Mathf.Approximately(brick.MaxHp, 1f)
                && brick.ExperienceReward == 0),
                "Every dragon skeleton has exactly 1/1 HP and no XP reward");

            int beforeGold = MetaProgression.Gold;
            int beforeAcorns = MetaProgression.Acorns;
            int beforeUnlocked = LevelProgression.HighestUnlockedLevel;
            manager.Front.PayTurnIncome();
            Check(MetaProgression.Gold == beforeGold,
                "Test run does not award room gold");
            manager.WinGame();
            Check(LevelProgression.HighestUnlockedLevel == beforeUnlocked
                && MetaProgression.Gold == beforeGold
                && MetaProgression.Acorns == beforeAcorns,
                "Test victory grants neither progress nor resources");
            Debug.Log("Developer mode validation: PASS");
            Finish(null);
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("Developer mode check: " + message);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= ValidateGameplayWhenReady;
        SessionState.SetBool(BatchKey, false);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
