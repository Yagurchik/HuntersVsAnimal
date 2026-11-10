using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyFirstEncounterValidation
{
    private const string GameplayScene = "Assets/Asset.unity";
    private const string PlayValidationRunning = "ForestAnimalWar.EnemyFirstEncounterValidation.Running";
    private static readonly List<string> failures = new List<string>();
    private static double playDeadline;
    private static double playStartedAt;

    static EnemyFirstEncounterValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static void RunBatch()
    {
        try
        {
            failures.Clear();
            EditorSceneManager.OpenScene(GameplayScene);
            ValidateSceneUI();
            ValidateProfile("Assets/Enemy/Brick.prefab", "forest_skeleton");
            ValidateProfile("Assets/Resources/Enemies/Zombie.prefab", "zombie");
            ValidateProfile("Assets/Resources/Enemies/ArmoredSkeleton.prefab", "armored_skeleton");
            ValidateProfile("Assets/Resources/Enemies/SwampDemon.prefab", "swamp_demon");
            ValidateProfile("Assets/Resources/Enemies/BogPustulant.prefab", "bog_pustulant");
            ValidateProfile("Assets/Resources/Enemies/LichRegular.prefab", "lich");
            ValidateProfile("Assets/Resources/Enemies/LichBoss.prefab", "lich_boss");
            ValidateProfile("Assets/Resources/Enemies/HiveHollowMiniBoss.prefab", "hive_hollow_mini_boss");
            ValidateProfile("Assets/Resources/Enemies/GreenJudgeMiniBoss.prefab", "green_judge_mini_boss");
            ValidateProfile("Assets/Resources/Enemies/BellRingerBoss.prefab", "bell_ringer_boss");
            ValidateProfile("Assets/Resources/Enemies/KnotBinderBoss.prefab", "knot_binder_boss");
            ValidateProfile("Assets/Resources/Enemies/VolleyEaterBoss.prefab", "volley_eater_boss");
            ValidateProfile("Assets/Resources/Enemies/ThreeSealIdolBoss.prefab", "three_seal_idol_boss");
            ValidateProfile("Assets/Resources/Enemies/ImpostorBoss.prefab", "impostor_boss");
            if (failures.Count > 0)
                throw new InvalidOperationException("Enemy first encounter validation failed:\n- "
                    + string.Join("\n- ", failures));
            Debug.Log("EnemyFirstEncounterValidation passed: scene UI and enemy profiles.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    public static void RunPlayBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Directory.CreateDirectory("Logs");
        failures.Clear();
        SessionState.SetBool(PlayValidationRunning, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(2);
        EditorSceneManager.OpenScene(GameplayScene);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode
            || !SessionState.GetBool(PlayValidationRunning, false)) return;
        playStartedAt = EditorApplication.timeSinceStartup;
        playDeadline = playStartedAt + 40d;
        EditorApplication.update += TickPlayValidation;
    }

    private static void TickPlayValidation()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > playDeadline)
                throw new TimeoutException("Enemy first encounter play validation timed out.");
            // Let initial enemies report themselves and the slide-in finish.
            if (EditorApplication.timeSinceStartup - playStartedAt < 1.1d) return;

            EnemyFirstEncounterUI ui = EnemyFirstEncounterUI.Instance;
            GameManager manager = GameManager.Instance;
            Brick enemy = UnityEngine.Object.FindObjectOfType<Brick>();
            Check(ui != null && manager != null && enemy != null,
                "Runtime systems and an initial enemy are active");
            Check(!manager.IsTutorialFlowActive,
                "Level two encounter cards are not held by the first-level tutorial");
            Check(ui.SeenTypeCount >= 2,
                "The ordinary skeleton and zombie are independently recognized");
            Check(ui.CardGroup != null && ui.CardGroup.alpha > 0f
                && !string.IsNullOrWhiteSpace(ui.TitleText.text),
                "The first queued summary visibly slides in during play");
            int countBeforeDuplicate = ui.SeenTypeCount;
            EnemyFirstEncounterUI.ReportSpawned(enemy);
            Check(ui.SeenTypeCount == countBeforeDuplicate,
                "Repeated enemies do not create another summary in the same level");
            FinishPlayValidation(null);
        }
        catch (Exception error)
        {
            FinishPlayValidation(error);
        }
    }

    private static void FinishPlayValidation(Exception error)
    {
        EditorApplication.update -= TickPlayValidation;
        SessionState.SetBool(PlayValidationRunning, false);
        string result = error == null
            ? "PASS: runtime first-encounter queue, display and deduplication checks."
            : "FAIL: " + error;
        File.WriteAllText("Logs/EnemyFirstEncounterPlayValidation.txt", result);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void ValidateSceneUI()
    {
        EnemyFirstEncounterUI ui = UnityEngine.Object.FindObjectOfType<EnemyFirstEncounterUI>(true);
        Check(ui != null, "UI exists in gameplay scene");
        if (ui == null) return;
        Check(Mathf.Approximately(ui.DisplayDuration, 5f), "Card duration is five seconds");
        Check(ui.EncounterCanvas != null && ui.EncounterCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            && ui.EncounterCanvas.sortingOrder == 260, "Card uses the non-blocking HUD layer");
        Check(ui.CardGroup != null && !ui.CardGroup.blocksRaycasts && !ui.CardGroup.interactable,
            "Card never blocks player input");
        Check(ui.Card != null && ui.TitleText != null, "Editable card references are serialized");
        Check(ui.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length >= 4,
            "Card contains readable summary fields");
        Check(ui.GetComponent<UnityEngine.UI.CanvasScaler>() != null,
            "Card scales for portrait Android screens");
    }

    private static void ValidateProfile(string path, string expectedId)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        EnemyFirstEncounterProfile profile = prefab != null
            ? prefab.GetComponent<EnemyFirstEncounterProfile>() : null;
        Check(profile != null && profile.EnemyTypeId == expectedId,
            expectedId + " has a first encounter profile");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) failures.Add(message);
    }
}
