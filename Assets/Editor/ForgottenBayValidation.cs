using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ForgottenBayValidation
{
    private const string RuntimeKey = "ForestAnimalWar.ForgottenBayValidation.Running";
    private static IEnumerator checks;
    private static double deadline;

    static ForgottenBayValidation()
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
            Debug.Log("Forgotten Bay authored-scene validation passed.");
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
        LevelProgression.PrepareLevel(16);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static void CheckAuthoredScene()
    {
        Require(LevelProgression.LocationCount == 6 && LevelProgression.MaxLevel == 30
            && LevelProgression.LocationNameForIndex(4) == "Забытый залив",
            "Forgotten Bay is not the fourth five-level location.");
        Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/LocationArt/ForgottenBay/ForgottenBayBattlefield.png");
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(
            "Assets/Resources/Battlefield/ForgottenBay/SeaFloorTile.asset");
        Require(artwork != null && tile != null && tile.sprite != null,
            "Forgotten Bay artwork or sea-floor tile is missing.");
        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        Require(hub != null && hub.forgottenBayIllustration != null
            && hub.forgottenBayIllustration.GetComponent<RawImage>()?.texture == artwork.texture,
            "The editable menu illustration is missing or unlinked.");
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        Require(progression != null, "Gameplay progression is missing.");
        SerializedObject serializedProgression = new SerializedObject(progression);
        Require(serializedProgression.FindProperty("levels").arraySize == 30,
            "All thirty levels must be serialized into the gameplay scene.");
        int count = 0;
        foreach (Stage stage in Object.FindObjectsOfType<Stage>(true))
        {
            Transform art = stage.transform.Find("Forgotten Bay Battlefield Art");
            DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
            Require(art != null && visuals != null,
                "Stage " + stage.name + " needs editable Forgotten Bay artwork.");
            SpriteRenderer renderer = art.GetComponent<SpriteRenderer>();
            SerializedObject data = new SerializedObject(visuals);
            Require(renderer != null && renderer.sprite == artwork
                && renderer.sortingOrder == -21
                && data.FindProperty("forgottenBayArtwork").objectReferenceValue == renderer,
                "Stage " + stage.name + " has an incomplete artwork link.");
            Tilemap bayTiles = null;
            foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
                if (candidate.name == Stage.ForgottenBayTilemapName) bayTiles = candidate;
            Require(bayTiles != null && stage.ForgottenBayFloorTile == tile
                && data.FindProperty("forgottenBayTiles").objectReferenceValue == bayTiles
                && bayTiles.GetUsedTilesCount() == 1,
                "Stage " + stage.name + " has incomplete editable sea-floor cells.");
            Vector2 expected = stage.BattlefieldSize;
            Vector2 actual = Vector2.Scale(artwork.bounds.size, art.localScale);
            Require(Vector2.Distance(expected, actual) < 0.01f,
                "Stage " + stage.name + " artwork does not fit its grid.");
            count++;
        }
        Require(count == 16, "Expected artwork on all sixteen stages.");
    }

    private static IEnumerator CheckRuntime()
    {
        yield return null;
        yield return null;
        Require(LevelProgression.Instance != null
            && LevelProgression.Instance.CurrentLevel == 16
            && LevelProgression.Instance.CurrentLocationIndex == 4,
            "Level 16 did not enter Forgotten Bay.");
        Require(GameManager.Instance != null && !GameManager.Instance.IsSurvivalMode,
            "Forgotten Bay must use campaign visuals.");
        Stage activeStage = GameManager.Instance.rooms.Count > 0
            ? GameManager.Instance.rooms[0].GetComponentInParent<Stage>() : null;
        Require(activeStage != null, "No active battlefield stage.");
        DrownedBogVisuals visuals = activeStage.GetComponent<DrownedBogVisuals>();
        Require(visuals != null, "Battlefield visuals are missing.");
        visuals.Refresh();
        Transform bay = activeStage.transform.Find("Forgotten Bay Battlefield Art");
        Transform bog = activeStage.transform.Find("Drowned Bog Visual Fog");
        Require(bay != null && bay.gameObject.activeSelf
            && bog != null && !bog.gameObject.activeSelf,
            "Forgotten Bay artwork did not replace the bog fog.");
        Tilemap bayTiles = null;
        Tilemap ordinaryTiles = null;
        foreach (Tilemap candidate in activeStage.GetComponentsInChildren<Tilemap>(true))
        {
            if (candidate.name == Stage.ForgottenBayTilemapName) bayTiles = candidate;
            else ordinaryTiles = candidate;
        }
        Require(bayTiles != null && bayTiles.gameObject.activeSelf
            && Mathf.Approximately(bayTiles.color.a, 1f)
            && ordinaryTiles != null && !ordinaryTiles.gameObject.activeSelf,
            "The bay sea-floor cells must replace the forest cells without hiding the image.");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Forgotten Bay runtime validation timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(RuntimeKey, false);
        if (error == null) Debug.Log("Forgotten Bay runtime validation passed.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
