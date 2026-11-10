using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ForgottenBayAuthoring
{
    private const string ArtworkPath =
        "Assets/LocationArt/ForgottenBay/ForgottenBayBattlefield.png";
    private const string TileArtworkPath =
        "Assets/LocationArt/ForgottenBay/ForgottenBaySeaFloorTile.png";
    private const string TileAssetPath =
        "Assets/Resources/Battlefield/ForgottenBay/SeaFloorTile.asset";
    private const string GameplayScenePath = "Assets/Asset.unity";
    private const string ArtworkObjectName = "Forgotten Bay Battlefield Art";

    [MenuItem("Tools/Forest Animal War/Locations/Bake Forgotten Bay")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before baking Forgotten Bay.");

        AssetDatabase.Refresh();
        Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
        Texture2D menuTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtworkPath);
        Sprite tileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TileArtworkPath);
        if (artwork == null || menuTexture == null || tileSprite == null)
            throw new InvalidOperationException("Forgotten Bay artwork did not import.");
        Tile seaFloorTile = BuildSeaFloorTile(tileSprite);
        BakeMenu(menuTexture);
        BakeGameplay(artwork, seaFloorTile);
        AssetDatabase.SaveAssets();
        Debug.Log("Forgotten Bay baked into editable menu and gameplay scenes: "
            + "menu illustration and sea-floor cells on all stages.");
    }

    private static Tile BuildSeaFloorTile(Sprite sprite)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(TileAssetPath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, TileAssetPath);
        }
        tile.sprite = sprite;
        tile.color = Color.white;
        tile.transform = Matrix4x4.identity;
        tile.gameObject = null;
        tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
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
        Transform bay = hero.Find("Forgotten Bay Illustration");
        if (bay == null)
        {
            GameObject image = new GameObject("Forgotten Bay Illustration",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            bay = image.transform;
            bay.SetParent(hero, false);
        }
        RectTransform rect = (RectTransform)bay;
        RectTransform source = (RectTransform)forest;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta;
        Transform bog = hero.Find("Drowned Bog Illustration");
        rect.SetSiblingIndex((bog != null ? bog : forest).GetSiblingIndex() + 1);
        RawImage raw = bay.GetComponent<RawImage>();
        raw.texture = illustration;
        raw.color = Color.white;
        // The source artwork is portrait; crop its pier-lined upper half to fit the wide card.
        raw.uvRect = new Rect(0f, 0.54f, 1f, 0.44f);
        raw.raycastTarget = false;
        hub.forgottenBayIllustration = bay.gameObject;
        bay.gameObject.SetActive(false);
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BakeGameplay(Sprite artwork, Tile seaFloorTile)
    {

        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
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

            Transform artworkTransform = stage.transform.Find(ArtworkObjectName);
            if (artworkTransform == null)
            {
                GameObject artworkObject = new GameObject(ArtworkObjectName);
                artworkTransform = artworkObject.transform;
                artworkTransform.SetParent(stage.transform, false);
            }

            SpriteRenderer renderer = artworkTransform.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = artworkTransform.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = artwork;
            renderer.sortingOrder = -21;
            renderer.color = Color.white;
            Vector2 size = stage.BattlefieldSize;
            artworkTransform.localPosition = Vector3.zero;
            artworkTransform.localRotation = Quaternion.identity;
            artworkTransform.localScale = new Vector3(
                size.x / artwork.bounds.size.x,
                size.y / artwork.bounds.size.y,
                1f);

            SerializedObject visualData = new SerializedObject(visuals);
            SerializedProperty bayArtwork = visualData.FindProperty("forgottenBayArtwork");
            if (bayArtwork == null)
                throw new InvalidOperationException("Forgotten Bay visual field is missing.");
            bayArtwork.objectReferenceValue = renderer;
            visualData.ApplyModifiedPropertiesWithoutUndo();
            artworkTransform.gameObject.SetActive(false);
            Tilemap ordinaryTiles = null;
            Tilemap bayTiles = null;
            foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
            {
                if (candidate.name == Stage.ForgottenBayTilemapName) bayTiles = candidate;
                else if (ordinaryTiles == null) ordinaryTiles = candidate;
            }
            if (ordinaryTiles == null)
                throw new InvalidOperationException("Stage " + stage.name + " has no existing tilemap.");
            if (bayTiles == null)
            {
                GameObject tileObject = new GameObject(Stage.ForgottenBayTilemapName,
                    typeof(Tilemap), typeof(TilemapRenderer));
                tileObject.layer = ordinaryTiles.gameObject.layer;
                tileObject.transform.SetParent(ordinaryTiles.transform.parent, false);
                bayTiles = tileObject.GetComponent<Tilemap>();
            }
            bayTiles.transform.localPosition = ordinaryTiles.transform.localPosition;
            bayTiles.transform.localRotation = ordinaryTiles.transform.localRotation;
            bayTiles.transform.localScale = ordinaryTiles.transform.localScale;
            bayTiles.tileAnchor = ordinaryTiles.tileAnchor;
            bayTiles.ClearAllTiles();
            foreach (Vector3Int cell in ordinaryTiles.cellBounds.allPositionsWithin)
                if (ordinaryTiles.HasTile(cell)) bayTiles.SetTile(cell, seaFloorTile);
            bayTiles.CompressBounds();
            TilemapRenderer tileRenderer = bayTiles.GetComponent<TilemapRenderer>();
            tileRenderer.sortingLayerID = ordinaryTiles.GetComponent<TilemapRenderer>().sortingLayerID;
            tileRenderer.sortingOrder = -20;
            stage.SetForgottenBayFloorTile(seaFloorTile);
            SerializedProperty bayTilesProperty = visualData.FindProperty("forgottenBayTiles");
            SerializedProperty bayTintProperty = visualData.FindProperty("bayTerrainTint");
            if (bayTilesProperty == null || bayTintProperty == null)
                throw new InvalidOperationException("Forgotten Bay tile visual fields are missing.");
            bayTilesProperty.objectReferenceValue = bayTiles;
            bayTintProperty.colorValue = new Color(1f, 1f, 1f, 0.58f);
            visualData.ApplyModifiedPropertiesWithoutUndo();
            bayTiles.gameObject.SetActive(false);
            EditorUtility.SetDirty(visuals);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(stage);
            EditorUtility.SetDirty(bayTiles);
            stageCount++;
        }

        if (stageCount == 0)
            throw new InvalidOperationException("No battlefield stages found.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Forgotten Bay gameplay artwork and sea-floor cells saved on "
            + stageCount + " stages. Enemy roster is unchanged.");
    }

    public static void BakeBatch()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
