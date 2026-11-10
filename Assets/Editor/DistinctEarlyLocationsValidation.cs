using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DistinctEarlyLocationsValidation
{
    private const string RuntimeKey = "ForestAnimalWar.DistinctEarlyLocations.Level";
    private static IEnumerator checks;
    private static double deadline;

    static DistinctEarlyLocationsValidation()
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

    public static void RunBatch()
    {
        try
        {
            CheckAuthoredScenes();
            Debug.Log("Distinct early locations authored-scene validation passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void RunGloomyPlayBatch() => StartPlayCheck(6);
    public static void RunBogPlayBatch() => StartPlayCheck(11);

    private static void StartPlayCheck(int level)
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
        CheckAuthoredScenes();
        SessionState.SetInt(RuntimeKey, level);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(level);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static void CheckAuthoredScenes()
    {
        Sprite gloomyArtwork = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/LocationArt/GloomyHollow/GloomyHollowBattlefield.png");
        Sprite bogArtwork = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/LocationArt/DrownedBog/DrownedBogBattlefield.png");
        Tile gloomyFloor = AssetDatabase.LoadAssetAtPath<Tile>(
            "Assets/Resources/Battlefield/GloomyHollow/EarthFloorTile.asset");
        Tile bogFloor = AssetDatabase.LoadAssetAtPath<Tile>(
            "Assets/Resources/Battlefield/DrownedBog/MudFloorTile.asset");
        Require(gloomyArtwork != null && bogArtwork != null
            && gloomyFloor != null && gloomyFloor.sprite != null
            && bogFloor != null && bogFloor.sprite != null,
            "Location two or three artwork is missing.");

        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        Require(hub != null && hub.gloomyHollowIllustration != null
            && hub.gloomyHollowIllustration.GetComponent<RawImage>()?.texture
                == gloomyArtwork.texture
            && hub.drownedBogIllustration != null,
            "Distinct menu artwork is missing.");

        EditorSceneManager.OpenScene("Assets/Asset.unity");
        int stages = 0;
        foreach (Stage stage in Object.FindObjectsOfType<Stage>(true))
        {
            CheckStage(stage, Stage.GloomyHollowTilemapName,
                "Gloomy Hollow Battlefield Art", gloomyArtwork, gloomyFloor);
            CheckStage(stage, Stage.DrownedBogTilemapName,
                "Drowned Bog Battlefield Art", bogArtwork, bogFloor);
            Require(stage.transform.Find("Drowned Bog Visual Fog") != null,
                "Existing swamp fog was removed from " + stage.name + ".");
            stages++;
        }
        Require(stages == 16, "Expected sixteen authored stages.");
    }

    private static void CheckStage(Stage stage, string tileName, string artName,
        Sprite artwork, Tile tile)
    {
        Transform art = stage.transform.Find(artName);
        Require(art != null && art.GetComponent<SpriteRenderer>()?.sprite == artwork,
            stage.name + " lacks " + artName + ".");
        Tilemap tilemap = null;
        foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
            if (candidate.name == tileName) tilemap = candidate;
        Require(tilemap != null && tilemap.GetUsedTilesCount() == 1
            && (tileName == Stage.GloomyHollowTilemapName
                ? stage.GloomyHollowFloorTile : stage.DrownedBogFloorTile) == tile,
            stage.name + " lacks " + tileName + ".");
        Vector2 actual = Vector2.Scale(artwork.bounds.size, art.localScale);
        Require(Vector2.Distance(actual, stage.BattlefieldSize) < 0.01f,
            artName + " does not fit " + stage.name + ".");
    }

    private static IEnumerator CheckRuntime(int level)
    {
        yield return null;
        yield return null;
        int location = level == 6 ? 2 : 3;
        Require(LevelProgression.Instance != null
            && LevelProgression.Instance.CurrentLevel == level
            && LevelProgression.Instance.CurrentLocationIndex == location,
            "Wrong campaign location at level " + level + ".");
        GameManager manager = GameManager.Instance;
        Require(manager != null && manager.rooms.Count > 0, "No active room.");
        Stage stage = manager.rooms[0].GetComponentInParent<Stage>();
        DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
        visuals.Refresh();
        string artName = location == 2 ? "Gloomy Hollow Battlefield Art"
            : "Drowned Bog Battlefield Art";
        string tileName = location == 2 ? Stage.GloomyHollowTilemapName
            : Stage.DrownedBogTilemapName;
        Require(stage.transform.Find(artName).gameObject.activeSelf,
            "Location-specific battlefield art is inactive.");
        int activeMaps = 0;
        foreach (Tilemap tilemap in stage.GetComponentsInChildren<Tilemap>(true))
        {
            if (!tilemap.gameObject.activeSelf) continue;
            Require(tilemap.name == tileName, "Wrong location tilemap is active.");
            activeMaps++;
        }
        Require(activeMaps == 1, "Exactly one location tilemap should be active.");
        if (location == 3)
            Require(stage.transform.Find("Drowned Bog Visual Fog").gameObject.activeSelf,
                "Visual swamp fog should remain active.");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Early-location runtime validation timed out.");
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
        if (error == null) Debug.Log("Distinct early location runtime validation passed at level "
            + level + ".");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
