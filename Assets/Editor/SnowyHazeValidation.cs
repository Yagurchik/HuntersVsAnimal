using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SnowyHazeValidation
{
    private const string RuntimeKey = "ForestAnimalWar.SnowyHazeValidation.Running";
    private static IEnumerator checks;
    private static double deadline;

    static SnowyHazeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(RuntimeKey, false)) return;
            checks = CheckRuntime();
            deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        try
        {
            CheckAuthoredScene();
            Debug.Log("Snowy Haze authored-scene validation passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void RunPlayBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
        CheckAuthoredScene();
        SessionState.SetBool(RuntimeKey, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(21);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static void CheckAuthoredScene()
    {
        Require(LevelProgression.LocationCount == 6 && LevelProgression.MaxLevel == 30
            && LevelProgression.LocationNameForIndex(5) == "Снежная мгла"
            && LevelProgression.GlobalLevelFor(5, 1) == 21
            && LevelProgression.GlobalLevelFor(5, 5) == 25,
            "Snowy Haze needs five campaign levels, 21–25.");
        Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/LocationArt/SnowyHaze/SnowyHazeBattlefield.png");
        Tile iceTile = AssetDatabase.LoadAssetAtPath<Tile>(
            "Assets/Resources/Battlefield/SnowyHaze/IceFloorTile.asset");
        Require(artwork != null && iceTile != null && iceTile.sprite != null,
            "Snowy Haze artwork or ice floor tile is missing.");

        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        Require(hub != null && hub.snowyHazeIllustration != null
            && hub.snowyHazeIllustration.GetComponent<RawImage>()?.texture == artwork.texture,
            "The editable Snowy Haze menu illustration is missing or unlinked.");

        EditorSceneManager.OpenScene("Assets/Asset.unity");
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        Require(progression != null, "Gameplay progression is missing.");
        SerializedObject data = new SerializedObject(progression);
        Require(data.FindProperty("levels").arraySize == 30,
            "All thirty levels must be serialized in the gameplay scene.");
        int stages = 0;
        foreach (Stage stage in Object.FindObjectsOfType<Stage>(true))
        {
            Transform art = stage.transform.Find("Snowy Haze Battlefield Art");
            DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
            Require(art != null && visuals != null,
                "Stage " + stage.name + " lacks editable winter artwork.");
            SpriteRenderer renderer = art.GetComponent<SpriteRenderer>();
            Tilemap snowTiles = null;
            foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
                if (candidate.name == Stage.SnowyHazeTilemapName) snowTiles = candidate;
            SerializedObject visualData = new SerializedObject(visuals);
            Require(renderer != null && renderer.sprite == artwork
                && visualData.FindProperty("snowyHazeArtwork").objectReferenceValue == renderer
                && snowTiles != null && stage.SnowyHazeFloorTile == iceTile
                && visualData.FindProperty("snowyHazeTiles").objectReferenceValue == snowTiles
                && snowTiles.GetUsedTilesCount() == 1,
                "Stage " + stage.name + " has incomplete snowy scene links.");
            Vector2 actual = Vector2.Scale(artwork.bounds.size, art.localScale);
            Require(Vector2.Distance(actual, stage.BattlefieldSize) < 0.01f,
                "Stage " + stage.name + " artwork does not fit its grid.");
            stages++;
        }
        Require(stages == 16, "Expected Snowy Haze art on all sixteen stages.");
    }

    private static IEnumerator CheckRuntime()
    {
        yield return null;
        yield return null;
        Require(LevelProgression.Instance != null
            && LevelProgression.Instance.CurrentLevel == 21
            && LevelProgression.Instance.CurrentLocationIndex == 5,
            "Level 21 did not enter Snowy Haze.");
        GameManager manager = GameManager.Instance;
        Require(manager != null && !manager.IsSurvivalMode && manager.rooms.Count > 0,
            "Snowy Haze must use the campaign battlefield.");
        Stage stage = manager.rooms[0].GetComponentInParent<Stage>();
        Require(stage != null, "No active battlefield stage.");
        DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
        Require(visuals != null, "Battlefield visuals are missing.");
        visuals.Refresh();
        Transform art = stage.transform.Find("Snowy Haze Battlefield Art");
        Transform bay = stage.transform.Find("Forgotten Bay Battlefield Art");
        Require(art != null && art.gameObject.activeSelf
            && bay != null && !bay.gameObject.activeSelf,
            "Winter artwork did not replace the bay scene.");
        Tilemap snowTiles = null;
        Tilemap ordinaryTiles = null;
        foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
        {
            if (candidate.name == Stage.SnowyHazeTilemapName) snowTiles = candidate;
            else if (candidate.name != Stage.ForgottenBayTilemapName) ordinaryTiles = candidate;
        }
        Require(snowTiles != null && snowTiles.gameObject.activeSelf
            && Mathf.Approximately(snowTiles.color.a, 1f)
            && ordinaryTiles != null && !ordinaryTiles.gameObject.activeSelf,
            "Opaque ice cells must replace ordinary cells without scenery grain.");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Snowy Haze runtime validation timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(RuntimeKey, false);
        if (error == null) Debug.Log("Snowy Haze runtime validation passed.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
