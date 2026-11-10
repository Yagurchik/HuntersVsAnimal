using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FieryGehennaValidation
{
    private const string RuntimeKey = "ForestAnimalWar.FieryGehennaValidation.Running";
    private static IEnumerator checks;
    private static double deadline;

    static FieryGehennaValidation()
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
            Debug.Log("Fiery Gehenna authored-scene validation passed.");
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
        LevelProgression.PrepareLevel(26);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static void CheckAuthoredScene()
    {
        Require(LevelProgression.LocationCount == 6 && LevelProgression.MaxLevel == 30
            && LevelProgression.LocationNameForIndex(6) == "Геенна огненная"
            && LevelProgression.GlobalLevelFor(6, 1) == 26
            && LevelProgression.GlobalLevelFor(6, 5) == 30,
            "Fiery Gehenna needs campaign levels 26–30.");
        Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/LocationArt/FieryGehenna/GehennaBattlefield.png");
        Tile basaltTile = AssetDatabase.LoadAssetAtPath<Tile>(
            "Assets/Resources/Battlefield/FieryGehenna/BasaltFloorTile.asset");
        Require(artwork != null && basaltTile != null && basaltTile.sprite != null,
            "Fiery Gehenna artwork or basalt tile is missing.");

        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        Require(hub != null && hub.fieryGehennaIllustration != null
            && hub.fieryGehennaIllustration.GetComponent<RawImage>()?.texture == artwork.texture,
            "The editable Fiery Gehenna menu illustration is missing or unlinked.");

        EditorSceneManager.OpenScene("Assets/Asset.unity");
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        Require(progression != null, "Gameplay progression is missing.");
        SerializedObject data = new SerializedObject(progression);
        Require(data.FindProperty("levels").arraySize == 30,
            "All thirty campaign levels must be serialized in the gameplay scene.");
        int stages = 0;
        foreach (Stage stage in Object.FindObjectsOfType<Stage>(true))
        {
            Transform art = stage.transform.Find("Fiery Gehenna Battlefield Art");
            DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
            Require(art != null && visuals != null,
                "Stage " + stage.name + " lacks editable volcanic artwork.");
            SpriteRenderer renderer = art.GetComponent<SpriteRenderer>();
            Tilemap gehennaTiles = null;
            foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
                if (candidate.name == Stage.FieryGehennaTilemapName) gehennaTiles = candidate;
            SerializedObject visualData = new SerializedObject(visuals);
            Require(renderer != null && renderer.sprite == artwork
                && visualData.FindProperty("fieryGehennaArtwork").objectReferenceValue == renderer
                && gehennaTiles != null && stage.FieryGehennaFloorTile == basaltTile
                && visualData.FindProperty("fieryGehennaTiles").objectReferenceValue == gehennaTiles
                && gehennaTiles.GetUsedTilesCount() == 1,
                "Stage " + stage.name + " has incomplete Fiery Gehenna scene links.");
            Vector2 actual = Vector2.Scale(artwork.bounds.size, art.localScale);
            Require(Vector2.Distance(actual, stage.BattlefieldSize) < 0.01f,
                "Stage " + stage.name + " artwork does not fit its grid.");
            stages++;
        }
        Require(stages == 16, "Expected Fiery Gehenna art on all sixteen stages.");
    }

    private static IEnumerator CheckRuntime()
    {
        yield return null;
        yield return null;
        Require(LevelProgression.Instance != null
            && LevelProgression.Instance.CurrentLevel == 26
            && LevelProgression.Instance.CurrentLocationIndex == 6,
            "Level 26 did not enter Fiery Gehenna.");
        GameManager manager = GameManager.Instance;
        Require(manager != null && !manager.IsSurvivalMode && manager.rooms.Count > 0,
            "Fiery Gehenna must use the campaign battlefield.");
        Stage stage = manager.rooms[0].GetComponentInParent<Stage>();
        Require(stage != null, "No active battlefield stage.");
        DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
        Require(visuals != null, "Battlefield visuals are missing.");
        visuals.Refresh();
        Transform art = stage.transform.Find("Fiery Gehenna Battlefield Art");
        Transform snow = stage.transform.Find("Snowy Haze Battlefield Art");
        Require(art != null && art.gameObject.activeSelf
            && snow != null && !snow.gameObject.activeSelf,
            "Volcanic artwork did not replace the winter scene.");
        Tilemap gehennaTiles = null;
        Tilemap ordinaryTiles = null;
        foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
        {
            if (candidate.name == Stage.FieryGehennaTilemapName) gehennaTiles = candidate;
            else if (candidate.name != Stage.ForgottenBayTilemapName
                && candidate.name != Stage.SnowyHazeTilemapName) ordinaryTiles = candidate;
        }
        Require(gehennaTiles != null && gehennaTiles.gameObject.activeSelf
            && Mathf.Approximately(gehennaTiles.color.a, 1f)
            && ordinaryTiles != null && !ordinaryTiles.gameObject.activeSelf,
            "Opaque basalt cells must replace ordinary cells without scenery grain.");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Fiery Gehenna runtime validation timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(RuntimeKey, false);
        if (error == null) Debug.Log("Fiery Gehenna runtime validation passed.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
