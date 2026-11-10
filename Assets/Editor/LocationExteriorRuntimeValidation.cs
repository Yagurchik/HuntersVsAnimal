using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class LocationExteriorRuntimeValidation
{
    private const string RuntimeKey = "ForestAnimalWar.ExteriorValidation.Level";
    private static IEnumerator checks;
    private static double deadline;
    private static readonly string[] ArtworkPaths = {
        "Assets/LocationArt/TwilightThicket/TwilightExterior.png",
        "Assets/LocationArt/GloomyHollow/GloomyHollowBattlefield.png",
        "Assets/LocationArt/DrownedBog/DrownedBogBattlefield.png",
        "Assets/LocationArt/ForgottenBay/ForgottenBayBattlefield.png",
        "Assets/LocationArt/SnowyHaze/SnowyHazeBattlefield.png",
        "Assets/LocationArt/FieryGehenna/GehennaBattlefield.png"
    };
    private static readonly string[] ActiveTilemapNames = {
        "Tilemap", Stage.GloomyHollowTilemapName, Stage.DrownedBogTilemapName,
        Stage.ForgottenBayTilemapName, Stage.SnowyHazeTilemapName,
        Stage.FieryGehennaTilemapName
    };

    static LocationExteriorRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            int level = SessionState.GetInt(RuntimeKey, 0);
            if (level == 0) return;
            checks = CheckRuntime(level);
            deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update += Tick;
        };
    }

    public static void RunLevel1Batch() => Start(1);
    public static void RunLevel6Batch() => Start(6);
    public static void RunLevel11Batch() => Start(11);
    public static void RunLevel16Batch() => Start(16);
    public static void RunLevel21Batch() => Start(21);
    public static void RunLevel26Batch() => Start(26);

    private static void Start(int level)
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
        SessionState.SetInt(RuntimeKey, level);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(level);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckRuntime(int level)
    {
        yield return null;
        yield return null;
        int location = LevelProgression.LocationIndexForLevel(level);
        Require(LevelProgression.Instance != null
            && LevelProgression.Instance.CurrentLevel == level,
            "Wrong campaign level loaded.");
        GameplayBackdropUI backdrop = UnityEngine.Object.FindObjectOfType<GameplayBackdropUI>(true);
        Require(backdrop != null, "Authored backdrop is missing.");
        LocationBackdropGraphic exterior = backdrop.GetComponentInChildren<LocationBackdropGraphic>(true);
        PumpkinBackdropGraphic ornament = backdrop.GetComponentInChildren<PumpkinBackdropGraphic>(true);
        Texture2D expected = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtworkPaths[location - 1]);
        Require(exterior != null && exterior.isActiveAndEnabled
            && exterior.mainTexture == expected,
            "Wrong outer illustration at level " + level + ".");
        Require(ornament != null && ornament.isActiveAndEnabled,
            "Location-coloured ornaments are missing.");
        GameManager manager = GameManager.Instance;
        Require(manager != null && manager.rooms.Count > 0, "No active campaign room.");
        Stage stage = manager.rooms[0].GetComponentInParent<Stage>();
        Require(stage != null, "No active stage.");
        Camera camera = Camera.main;
        Require(camera != null, "No gameplay camera.");
        Bounds board = stage.GetViewBounds();
        Vector3 boardMin = camera.WorldToViewportPoint(board.min);
        Vector3 boardMax = camera.WorldToViewportPoint(board.max);
        Rect window = exterior.BoardWindow;
        Require(Mathf.Abs(window.xMin - boardMin.x) < 0.025f
            && Mathf.Abs(window.yMin - boardMin.y) < 0.025f
            && Mathf.Abs(window.xMax - boardMax.x) < 0.025f
            && Mathf.Abs(window.yMax - boardMax.y) < 0.025f,
            "Exterior has a gap before the battlefield at level " + level + ".");
        DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
        Require(visuals != null, "No visual switcher on stage.");
        visuals.Refresh();
        int visibleTiles = 0;
        foreach (Tilemap map in stage.GetComponentsInChildren<Tilemap>(true))
        {
            if (!map.gameObject.activeSelf) continue;
            Require(map.name == ActiveTilemapNames[location - 1]
                && Mathf.Approximately(map.color.a, 1f),
                "Wrong or translucent tilemap at level " + level + ".");
            visibleTiles++;
        }
        Require(visibleTiles == 1, "Exactly one fully opaque tilemap should be visible.");
        BattlefieldForestStyle frame = stage.GetComponent<BattlefieldForestStyle>();
        Require(frame != null && frame.FrameRenderer != null
            && frame.FrameRenderer.color.a > 0.99f,
            "Exterior frame is missing or translucent.");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Location exterior runtime check timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        int level = SessionState.GetInt(RuntimeKey, 0);
        SessionState.SetInt(RuntimeKey, 0);
        if (error == null) Debug.Log("Location exterior runtime validation passed at level "
            + level + ".");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
