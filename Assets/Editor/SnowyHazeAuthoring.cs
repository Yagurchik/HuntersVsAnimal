using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class SnowyHazeAuthoring
{
    private const string ArtworkPath =
        "Assets/LocationArt/SnowyHaze/SnowyHazeBattlefield.png";
    private const string TilePath =
        "Assets/Resources/Battlefield/SnowyHaze/IceFloorTile.asset";
    private const string ArtworkObjectName = "Snowy Haze Battlefield Art";

    [MenuItem("Tools/Forest Animal War/Locations/Bake Snowy Haze")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before baking Snowy Haze.");

        AssetDatabase.Refresh();
        Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
        Tile iceFloor = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        if (artwork == null || iceFloor == null || iceFloor.sprite == null)
            throw new InvalidOperationException("Snowy Haze artwork or ice floor tile did not import.");

        BakeMenu(artwork.texture);
        BakeGameplay(artwork, iceFloor);
        AssetDatabase.SaveAssets();
        Debug.Log("Snowy Haze baked into editable menu and gameplay scenes: "
            + "five campaign levels, winter illustration and ice cells on all stages.");
    }

    private static void BakeMenu(Texture2D illustration)
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity", OpenSceneMode.Single);
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null || hub.tabs == null || hub.tabs.Length == 0)
            throw new InvalidOperationException("Campaign menu controller is missing.");
        Transform hero = hub.tabs[0].page.transform.Find("Viewport/Content/Forest Hero");
        Transform forest = hero != null ? hero.Find("Forest Illustration") : null;
        if (forest == null)
            throw new InvalidOperationException("Forest illustration is missing.");

        Transform snow = hero.Find("Snowy Haze Illustration");
        if (snow == null)
        {
            GameObject image = new GameObject("Snowy Haze Illustration",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            snow = image.transform;
            snow.SetParent(hero, false);
        }
        RectTransform rect = (RectTransform)snow;
        RectTransform source = (RectTransform)forest;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta;
        Transform bay = hero.Find("Forgotten Bay Illustration");
        rect.SetSiblingIndex((bay != null ? bay : forest).GetSiblingIndex() + 1);
        RawImage raw = snow.GetComponent<RawImage>();
        raw.texture = illustration;
        raw.color = Color.white;
        // Show the wind-swept clearing and trees, not the full portrait artwork.
        raw.uvRect = new Rect(0f, 0.52f, 1f, 0.42f);
        raw.raycastTarget = false;
        hub.snowyHazeIllustration = snow.gameObject;
        snow.gameObject.SetActive(false);
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BakeGameplay(Sprite artwork, Tile iceFloor)
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Asset.unity", OpenSceneMode.Single);
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        if (progression == null)
            throw new InvalidOperationException("Gameplay progression is missing.");
        progression.SendMessage("NormalizeLevelData", SendMessageOptions.RequireReceiver);
        EditorUtility.SetDirty(progression);

        int stageCount = 0;
        foreach (Stage stage in Object.FindObjectsOfType<Stage>(true))
        {
            DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
            if (visuals == null) visuals = stage.gameObject.AddComponent<DrownedBogVisuals>();

            Transform art = stage.transform.Find(ArtworkObjectName);
            if (art == null)
            {
                GameObject artObject = new GameObject(ArtworkObjectName);
                art = artObject.transform;
                art.SetParent(stage.transform, false);
            }
            SpriteRenderer renderer = art.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = art.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = artwork;
            renderer.sortingOrder = -21;
            renderer.color = new Color(0.76f, 0.83f, 0.91f, 1f);
            Vector2 size = stage.BattlefieldSize;
            art.localPosition = Vector3.zero;
            art.localRotation = Quaternion.identity;
            art.localScale = new Vector3(
                size.x / artwork.bounds.size.x,
                size.y / artwork.bounds.size.y, 1f);
            art.gameObject.SetActive(false);

            Tilemap ordinaryTiles = null;
            Tilemap snowTiles = null;
            foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
            {
                if (candidate.name == Stage.SnowyHazeTilemapName) snowTiles = candidate;
                else if (candidate.name != Stage.ForgottenBayTilemapName && ordinaryTiles == null)
                    ordinaryTiles = candidate;
            }
            if (ordinaryTiles == null)
                throw new InvalidOperationException("Stage " + stage.name + " has no ordinary tilemap.");
            if (snowTiles == null)
            {
                GameObject tileObject = new GameObject(Stage.SnowyHazeTilemapName,
                    typeof(Tilemap), typeof(TilemapRenderer));
                tileObject.layer = ordinaryTiles.gameObject.layer;
                tileObject.transform.SetParent(ordinaryTiles.transform.parent, false);
                snowTiles = tileObject.GetComponent<Tilemap>();
            }
            snowTiles.transform.localPosition = ordinaryTiles.transform.localPosition;
            snowTiles.transform.localRotation = ordinaryTiles.transform.localRotation;
            snowTiles.transform.localScale = ordinaryTiles.transform.localScale;
            snowTiles.tileAnchor = ordinaryTiles.tileAnchor;
            snowTiles.ClearAllTiles();
            foreach (Vector3Int cell in ordinaryTiles.cellBounds.allPositionsWithin)
                if (ordinaryTiles.HasTile(cell)) snowTiles.SetTile(cell, iceFloor);
            snowTiles.CompressBounds();
            TilemapRenderer tileRenderer = snowTiles.GetComponent<TilemapRenderer>();
            tileRenderer.sortingLayerID = ordinaryTiles.GetComponent<TilemapRenderer>().sortingLayerID;
            tileRenderer.sortingOrder = -20;
            snowTiles.gameObject.SetActive(false);

            stage.SetSnowyHazeFloorTile(iceFloor);
            SerializedObject data = new SerializedObject(visuals);
            SerializedProperty snowArtwork = data.FindProperty("snowyHazeArtwork");
            SerializedProperty snowTilemap = data.FindProperty("snowyHazeTiles");
            SerializedProperty snowTint = data.FindProperty("snowTerrainTint");
            if (snowArtwork == null || snowTilemap == null || snowTint == null)
                throw new InvalidOperationException("Snowy Haze visual fields are missing.");
            snowArtwork.objectReferenceValue = renderer;
            snowTilemap.objectReferenceValue = snowTiles;
            snowTint.colorValue = new Color(1f, 1f, 1f, 0.56f);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visuals);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(stage);
            EditorUtility.SetDirty(snowTiles);
            stageCount++;
        }

        if (stageCount == 0)
            throw new InvalidOperationException("No battlefield stages found.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Snowy Haze artwork and ice cells saved on " + stageCount
            + " stages. Enemy roster remains unchanged.");
    }

    public static void BakeBatch()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
