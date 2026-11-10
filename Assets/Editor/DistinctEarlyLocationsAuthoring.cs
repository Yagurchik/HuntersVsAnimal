using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Bakes authored scenery for locations two and three. Location one keeps its
// existing editable forest frame and varied tile palette.
public static class DistinctEarlyLocationsAuthoring
{
    private const string GloomyArtworkPath =
        "Assets/LocationArt/GloomyHollow/GloomyHollowBattlefield.png";
    private const string GloomyTilePath =
        "Assets/Resources/Battlefield/GloomyHollow/EarthFloorTile.asset";
    private const string BogArtworkPath =
        "Assets/LocationArt/DrownedBog/DrownedBogBattlefield.png";
    private const string BogTilePath =
        "Assets/Resources/Battlefield/DrownedBog/MudFloorTile.asset";

    [MenuItem("Tools/Forest Animal War/Locations/Bake distinct early locations")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before baking locations.");
        AssetDatabase.Refresh();
        Sprite gloomyArtwork = LoadArtwork(GloomyArtworkPath);
        Sprite bogArtwork = LoadArtwork(BogArtworkPath);
        Tile gloomyFloor = LoadFloor(GloomyTilePath);
        Tile bogFloor = LoadFloor(BogTilePath);

        BakeMenu(gloomyArtwork.texture);
        BakeGameplay(gloomyArtwork, gloomyFloor, bogArtwork, bogFloor);
        AssetDatabase.SaveAssets();
        Debug.Log("Gloomy Hollow and Drowned Bog saved as distinct editable scenery "
            + "across all campaign rooms. The swamp fog remains unchanged.");
    }

    private static Sprite LoadArtwork(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Artwork did not import: " + path);
        return sprite;
    }

    private static Tile LoadFloor(string path)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null || tile.sprite == null)
            throw new InvalidOperationException("Floor tile did not import: " + path);
        return tile;
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
        Transform gloomy = hero.Find("Gloomy Hollow Illustration");
        if (gloomy == null)
        {
            GameObject image = new GameObject("Gloomy Hollow Illustration",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            gloomy = image.transform;
            gloomy.SetParent(hero, false);
        }
        RectTransform rect = (RectTransform)gloomy;
        RectTransform source = (RectTransform)forest;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta;
        rect.SetSiblingIndex(forest.GetSiblingIndex() + 1);
        RawImage raw = gloomy.GetComponent<RawImage>();
        raw.texture = illustration;
        raw.color = Color.white;
        raw.uvRect = new Rect(0f, 0.52f, 1f, 0.42f);
        raw.raycastTarget = false;
        hub.gloomyHollowIllustration = gloomy.gameObject;
        gloomy.gameObject.SetActive(false);
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BakeGameplay(Sprite gloomyArtwork, Tile gloomyFloor,
        Sprite bogArtwork, Tile bogFloor)
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Asset.unity", OpenSceneMode.Single);
        int stageCount = 0;
        foreach (Stage stage in Object.FindObjectsOfType<Stage>(true))
        {
            DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
            if (visuals == null)
                throw new InvalidOperationException("Stage " + stage.name + " has no visual switcher.");
            Tilemap ordinaryTiles = stage.transform.Find("Tilemap")?.GetComponent<Tilemap>();
            if (ordinaryTiles == null)
            {
                foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
                    if (candidate.name == "Tilemap") { ordinaryTiles = candidate; break; }
            }
            if (ordinaryTiles == null)
                throw new InvalidOperationException("Stage " + stage.name + " has no forest tilemap.");

            SpriteRenderer gloomyRenderer = BakeArtwork(stage,
                "Gloomy Hollow Battlefield Art", gloomyArtwork);
            SpriteRenderer bogRenderer = BakeArtwork(stage,
                "Drowned Bog Battlefield Art", bogArtwork);
            Tilemap gloomyTiles = BakeTiles(ordinaryTiles, Stage.GloomyHollowTilemapName,
                gloomyFloor);
            Tilemap bogTiles = BakeTiles(ordinaryTiles, Stage.DrownedBogTilemapName,
                bogFloor);
            stage.SetGloomyHollowFloorTile(gloomyFloor);
            stage.SetDrownedBogFloorTile(bogFloor);

            SerializedObject data = new SerializedObject(visuals);
            data.FindProperty("gloomyHollowArtwork").objectReferenceValue = gloomyRenderer;
            data.FindProperty("gloomyHollowTiles").objectReferenceValue = gloomyTiles;
            data.FindProperty("drownedBogArtwork").objectReferenceValue = bogRenderer;
            data.FindProperty("drownedBogTiles").objectReferenceValue = bogTiles;
            data.FindProperty("gloomyTerrainTint").colorValue =
                new Color(1f, 1f, 1f, 0.56f);
            data.FindProperty("bogTerrainTint").colorValue =
                new Color(1f, 1f, 1f, 0.56f);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visuals);
            EditorUtility.SetDirty(stage);
            stageCount++;
        }
        if (stageCount != 16)
            throw new InvalidOperationException("Expected sixteen stages, found " + stageCount + ".");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static SpriteRenderer BakeArtwork(Stage stage, string name, Sprite sprite)
    {
        Transform art = stage.transform.Find(name);
        if (art == null)
        {
            art = new GameObject(name).transform;
            art.SetParent(stage.transform, false);
        }
        SpriteRenderer renderer = art.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = art.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -21;
        renderer.color = Color.white;
        Vector2 size = stage.BattlefieldSize;
        art.localPosition = Vector3.zero;
        art.localRotation = Quaternion.identity;
        art.localScale = new Vector3(size.x / sprite.bounds.size.x,
            size.y / sprite.bounds.size.y, 1f);
        art.gameObject.SetActive(false);
        EditorUtility.SetDirty(renderer);
        return renderer;
    }

    private static Tilemap BakeTiles(Tilemap ordinaryTiles, string name, Tile tile)
    {
        Transform parent = ordinaryTiles.transform.parent;
        Transform existing = parent.Find(name);
        if (existing == null)
        {
            GameObject tileObject = new GameObject(name,
                typeof(Tilemap), typeof(TilemapRenderer));
            tileObject.layer = ordinaryTiles.gameObject.layer;
            existing = tileObject.transform;
            existing.SetParent(parent, false);
        }
        Tilemap tiles = existing.GetComponent<Tilemap>();
        tiles.transform.localPosition = ordinaryTiles.transform.localPosition;
        tiles.transform.localRotation = ordinaryTiles.transform.localRotation;
        tiles.transform.localScale = ordinaryTiles.transform.localScale;
        tiles.tileAnchor = ordinaryTiles.tileAnchor;
        tiles.ClearAllTiles();
        foreach (Vector3Int cell in ordinaryTiles.cellBounds.allPositionsWithin)
            if (ordinaryTiles.HasTile(cell)) tiles.SetTile(cell, tile);
        tiles.CompressBounds();
        TilemapRenderer renderer = tiles.GetComponent<TilemapRenderer>();
        renderer.sortingLayerID = ordinaryTiles.GetComponent<TilemapRenderer>().sortingLayerID;
        renderer.sortingOrder = -20;
        tiles.gameObject.SetActive(false);
        EditorUtility.SetDirty(tiles);
        return tiles;
    }

    public static void BakeBatch()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
