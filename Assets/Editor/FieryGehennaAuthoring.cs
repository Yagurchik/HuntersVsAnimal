using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class FieryGehennaAuthoring
{
    private const string ArtworkPath = "Assets/LocationArt/FieryGehenna/GehennaBattlefield.png";
    private const string TilePath = "Assets/Resources/Battlefield/FieryGehenna/BasaltFloorTile.asset";
    private const string ArtworkObjectName = "Fiery Gehenna Battlefield Art";

    [MenuItem("Tools/Forest Animal War/Locations/Bake Fiery Gehenna")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before baking Fiery Gehenna.");

        AssetDatabase.Refresh();
        Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
        Tile basaltFloor = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        if (artwork == null || basaltFloor == null || basaltFloor.sprite == null)
            throw new InvalidOperationException("Fiery Gehenna artwork or basalt floor tile did not import.");

        BakeMenu(artwork.texture);
        BakeGameplay(artwork, basaltFloor);
        AssetDatabase.SaveAssets();
        Debug.Log("Fiery Gehenna baked into editable menu and gameplay scenes: levels 26–30, "
            + "volcanic illustration and basalt cells on all stages.");
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

        Transform gehenna = hero.Find("Fiery Gehenna Illustration");
        if (gehenna == null)
        {
            GameObject image = new GameObject("Fiery Gehenna Illustration",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            gehenna = image.transform;
            gehenna.SetParent(hero, false);
        }
        RectTransform rect = (RectTransform)gehenna;
        RectTransform source = (RectTransform)forest;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta;
        Transform snow = hero.Find("Snowy Haze Illustration");
        rect.SetSiblingIndex((snow != null ? snow : forest).GetSiblingIndex() + 1);
        RawImage raw = gehenna.GetComponent<RawImage>();
        raw.texture = illustration;
        raw.color = Color.white;
        raw.uvRect = new Rect(0f, 0.52f, 1f, 0.42f);
        raw.raycastTarget = false;
        hub.fieryGehennaIllustration = gehenna.gameObject;
        gehenna.gameObject.SetActive(false);
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BakeGameplay(Sprite artwork, Tile basaltFloor)
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
            renderer.color = Color.white;
            Vector2 size = stage.BattlefieldSize;
            art.localPosition = Vector3.zero;
            art.localRotation = Quaternion.identity;
            art.localScale = new Vector3(
                size.x / artwork.bounds.size.x,
                size.y / artwork.bounds.size.y, 1f);
            art.gameObject.SetActive(false);

            Tilemap ordinaryTiles = null;
            Tilemap gehennaTiles = null;
            foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
            {
                if (candidate.name == Stage.FieryGehennaTilemapName) gehennaTiles = candidate;
                else if (candidate.name != Stage.ForgottenBayTilemapName
                    && candidate.name != Stage.SnowyHazeTilemapName && ordinaryTiles == null)
                    ordinaryTiles = candidate;
            }
            if (ordinaryTiles == null)
                throw new InvalidOperationException("Stage " + stage.name + " has no ordinary tilemap.");
            if (gehennaTiles == null)
            {
                GameObject tileObject = new GameObject(Stage.FieryGehennaTilemapName,
                    typeof(Tilemap), typeof(TilemapRenderer));
                tileObject.layer = ordinaryTiles.gameObject.layer;
                tileObject.transform.SetParent(ordinaryTiles.transform.parent, false);
                gehennaTiles = tileObject.GetComponent<Tilemap>();
            }
            gehennaTiles.transform.localPosition = ordinaryTiles.transform.localPosition;
            gehennaTiles.transform.localRotation = ordinaryTiles.transform.localRotation;
            gehennaTiles.transform.localScale = ordinaryTiles.transform.localScale;
            gehennaTiles.tileAnchor = ordinaryTiles.tileAnchor;
            gehennaTiles.ClearAllTiles();
            foreach (Vector3Int cell in ordinaryTiles.cellBounds.allPositionsWithin)
                if (ordinaryTiles.HasTile(cell)) gehennaTiles.SetTile(cell, basaltFloor);
            gehennaTiles.CompressBounds();
            TilemapRenderer tileRenderer = gehennaTiles.GetComponent<TilemapRenderer>();
            tileRenderer.sortingLayerID = ordinaryTiles.GetComponent<TilemapRenderer>().sortingLayerID;
            tileRenderer.sortingOrder = -20;
            gehennaTiles.gameObject.SetActive(false);

            stage.SetFieryGehennaFloorTile(basaltFloor);
            SerializedObject data = new SerializedObject(visuals);
            SerializedProperty gehennaArtwork = data.FindProperty("fieryGehennaArtwork");
            SerializedProperty gehennaTilemap = data.FindProperty("fieryGehennaTiles");
            SerializedProperty gehennaTint = data.FindProperty("gehennaTerrainTint");
            if (gehennaArtwork == null || gehennaTilemap == null || gehennaTint == null)
                throw new InvalidOperationException("Fiery Gehenna visual fields are missing.");
            gehennaArtwork.objectReferenceValue = renderer;
            gehennaTilemap.objectReferenceValue = gehennaTiles;
            gehennaTint.colorValue = new Color(1f, 1f, 1f, 0.56f);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visuals);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(stage);
            EditorUtility.SetDirty(gehennaTiles);
            stageCount++;
        }

        if (stageCount == 0)
            throw new InvalidOperationException("No battlefield stages found.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Fiery Gehenna artwork and basalt cells saved on " + stageCount
            + " stages. Enemy roster remains unchanged.");
    }

    public static void BakeBatch()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
