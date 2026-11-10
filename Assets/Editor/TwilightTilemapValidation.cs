using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class TwilightTilemapValidation
{
    private static readonly List<string> Report = new List<string>();

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Exception failure = null;
        try
        {
            Report.Clear();
            EditorSceneManager.OpenScene("Assets/Asset.unity");
            BattlefieldTilePalette palette = TwilightThicketTilemapEditor.RebuildAssets();
            TwilightThicketTilemapEditor.ApplyToLoadedScene(palette, false);
            ValidateAtlas();
            ValidateRooms(palette);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        catch (Exception error) { failure = error; }

        Directory.CreateDirectory("Logs");
        string result = failure == null
            ? "PASS: " + Report.Count + " Twilight Thicket tilemap checks."
            : "FAIL: " + failure;
        Report.Add(result);
        File.WriteAllLines("Logs/TwilightTilemapValidation.txt", Report);
        if (failure == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }

    private static void ValidateAtlas()
    {
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(TwilightThicketTilemapEditor.AtlasPath);
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(TwilightThicketTilemapEditor.AtlasPath)
            .OfType<Sprite>().ToArray();
        Check(atlas != null && atlas.width == atlas.height,
            "Atlas is a square texture arranged in four rows and columns");
        Check(sprites.Length == 16, "Atlas imports as exactly sixteen editable sprites");
        float tileSize = atlas.width / 4f;
        Check(sprites.All(sprite => Mathf.Abs(sprite.rect.width - tileSize) <= 0.51f
            && Mathf.Abs(sprite.rect.height - tileSize) <= 0.51f
            && Mathf.Approximately(sprite.pixelsPerUnit, tileSize)),
            "Every sprite is one world-space cell with an exact square crop");
        Check(sprites.Select(sprite => sprite.name).Distinct().Count() == 16,
            "All atlas sprites have stable unique names");
    }

    private static void ValidateRooms(BattlefieldTilePalette palette)
    {
        Stage[] rooms = Object.FindObjectsOfType<Stage>(true);
        Check(palette != null && palette.IsConfigured, "Twilight Thicket palette contains corners, edges, and floor variants");
        Check(rooms.Length == 16, "All sixteen authored rooms receive the tilemap theme");
        for (int line = 1; line <= 4; line++)
            Check(rooms.Count(room => room.DefenseLine == line) == line * 2 - 1,
                "Defense line " + line + " keeps its authored room count");

        var floors = new HashSet<TileBase>(palette.FloorTiles);
        foreach (Stage room in rooms)
        {
            room.SetBattlefieldTilePalette(palette);
            room.ConfigureBattlefield(room.DefenseLine);
            Vector2Int gameplay = room.BattlefieldSize;
            Tilemap map = room.GetComponentInChildren<Tilemap>(true);
            Check(room.TilePalette == palette && map != null, room.name + ": palette and Tilemap are editable in the scene");
            BoundsInt bounds = map.cellBounds;
            Check(bounds.size.x == gameplay.x && bounds.size.y == gameplay.y,
                room.name + ": theme has no outer Tilemap layer beyond the playable cells");
            TileBase[] tiles = map.GetTilesBlock(bounds);
            Check(tiles.Length == gameplay.x * gameplay.y && tiles.All(tile => tile != null),
                room.name + ": every playable cell is painted");
            Check(tiles.All(tile => floors.Contains(tile)),
                room.name + ": outer forest tiles do not consume gameplay cells");
            Vector3Int lowerLeft = new Vector3Int(bounds.xMin, bounds.yMin, bounds.zMin);
            Vector3Int lowerNext = lowerLeft + Vector3Int.right;
            Vector3Int enemyLeft = lowerLeft + Vector3Int.up;
            Check(map.GetColor(lowerLeft) == map.GetColor(lowerNext)
                && map.GetColor(enemyLeft) != map.GetColor(enemyLeft + Vector3Int.right),
                room.name + ": dark pumpkin strip is uniform while enemy rows stay checkerboard");
            Check(map.GetTransformMatrix(lowerLeft) == Matrix4x4.identity,
                room.name + ": playable tile transforms stay inside their cells");
            Check(map.GetComponent<TilemapRenderer>() != null
                && map.GetComponent<TilemapRenderer>().sortingOrder == -20,
                room.name + ": terrain renders behind enemies, projectiles, and fog");
            BattlefieldForestStyle style = room.GetComponent<BattlefieldForestStyle>();
            Check(style != null && style.FrameRenderer != null && style.VineRenderer != null,
                room.name + ": editable forest frame and breach vine are present");
            Check(style.BoardEdgeRenderers != null && style.BoardEdgeRenderers.Count == 3
                && style.BoardEdgeRenderers.All(edge => edge != null && edge.sprite == style.VineRenderer.sprite),
                room.name + ": top and side orange edges remain editable, without a lower orange branch");
            Check(style.LowerFieldRenderer != null && style.LowerFieldRenderer.enabled
                && style.LowerFieldRenderer.drawMode == SpriteDrawMode.Tiled
                && style.LowerFieldRenderer.bounds.max.y >= room.GetViewBounds().min.y - 0.05f
                && style.LowerFieldRenderer.bounds.min.y <= room.GetViewBounds().min.y
                    - style.FrameProfile.LowerFieldExtension + 0.05f,
                room.name + ": separate lower field reaches the animal branch without gameplay cells");
            Check(AssetDatabase.GetAssetPath(style.FrameRenderer.sprite)
                    == TwilightThicketTilemapEditor.FramePathForLine(room.DefenseLine)
                && AssetDatabase.GetAssetPath(style.VineRenderer.sprite)
                    == TwilightThicketTilemapEditor.VinePath,
                room.name + ": decoration uses the restrained authored artwork");
            SpriteRenderer frame = style.FrameRenderer;
            Check(frame.sortingOrder > RoomDarkness.WorldOverlayOrder
                && Mathf.Approximately(frame.color.a, 1f)
                && AssetDatabase.GetAssetPath(frame.sharedMaterial)
                    == TwilightThicketTilemapEditor.OpaqueFrameMaterialPath
                && style.BoardEdgeRenderers.All(edge => edge.sortingOrder > RoomDarkness.WorldOverlayOrder
                    && Mathf.Approximately(edge.color.a, 1f)),
                room.name + ": opaque forest frame and edges render above the darkness");
            Check(style.FrameProfile != null && style.FrameProfile.DefenseLine == room.DefenseLine
                && style.FrameProfile.FieldSize == gameplay,
                room.name + ": uses its own defense-line artwork and editable profile");
            Rect opening = style.FrameProfile.OpeningNormalized;
            Bounds spriteBounds = frame.sprite.bounds;
            Vector3 openingMin = frame.transform.TransformPoint(spriteBounds.min + new Vector3(
                opening.xMin * spriteBounds.size.x, opening.yMin * spriteBounds.size.y, 0f));
            Vector3 openingMax = frame.transform.TransformPoint(spriteBounds.min + new Vector3(
                opening.xMax * spriteBounds.size.x, opening.yMax * spriteBounds.size.y, 0f));
            Bounds board = room.GetViewBounds();
            Check(Mathf.Abs(style.BoardEdgeRenderers[0].transform.position.y - board.max.y) < 0.001f
                && Mathf.Abs(style.BoardEdgeRenderers[1].transform.position.x - board.max.x) < 0.001f
                && Mathf.Abs(style.BoardEdgeRenderers[2].transform.position.x - board.min.x) < 0.001f
                && room.transform.Find("Orange Forest Edge Bottom") == null,
                room.name + ": orange edges border only the top and sides");
            const float layoutTolerance = 0.001f;
            Check(frame.drawMode == SpriteDrawMode.Simple
                && openingMin.x <= board.min.x + layoutTolerance
                && openingMin.y <= board.min.y + layoutTolerance
                && openingMax.x >= board.max.x - layoutTolerance
                && openingMax.y >= board.max.y - layoutTolerance,
                room.name + ": frame's clear opening contains every playable cell"
                + " (opening " + openingMin + ".." + openingMax
                + ", board " + board.min + ".." + board.max + ")");
            Bounds presentation = style.GetFramingBounds(board);
            Check(presentation.min.x < board.min.x && presentation.min.y < board.min.y
                && presentation.max.x > board.max.x && presentation.max.y > board.max.y,
                room.name + ": camera reserves exterior space for roots and spectators");
            Check(Mathf.Abs(style.VineRenderer.transform.position.y - room.DefenseBoundaryY) < 0.001f,
                room.name + ": bright vine marks the exact defeat boundary");
            Check(room.PlayerObject != null && Mathf.Abs(style.VineRenderer.transform.position.z
                    - room.PlayerObject.transform.position.z) < 0.001f,
                room.name + ": decoration shares the existing battlefield render plane");
            Check(style.BlinkingEyes.Count == 3
                && style.BlinkingEyes.All(eyes => eyes != null
                    && eyes.LeftEye != null && eyes.RightEye != null
                    && eyes.LeftEye.sortingOrder > frame.sortingOrder
                    && eyes.LeftEye.sortingOrder < 100
                    && eyes.RightEye.sortingOrder == eyes.LeftEye.sortingOrder),
                room.name + ": three independent eye pairs remain visible and can blink in darkness");
            Check(style.BlinkingEyes.All(eyes => eyes.transform.position.y > board.max.y),
                room.name + ": spectator eyes stay outside the battlefield");
            Check(style.FrameRenderer.GetComponentsInChildren<Collider2D>(true).Length == 0
                && style.BoardEdgeRenderers.All(edge => edge.GetComponentsInChildren<Collider2D>(true).Length == 0)
                && style.VineRenderer.GetComponentsInChildren<Collider2D>(true).Length == 0
                && style.BlinkingEyes.All(eyes =>
                    eyes.GetComponentsInChildren<Collider2D>(true).Length == 0),
                room.name + ": decoration adds no gameplay collision");

            LevelGenerator generator = room.GetComponentInChildren<LevelGenerator>(true);
            Vector3Int upperLeftCell = new Vector3Int(bounds.xMin, bounds.yMax - 1, bounds.zMin);
            // Inactive authored rooms can return stale GridLayout conversions in edit mode.
            Vector3 cellCenter = map.transform.TransformPoint(new Vector3(
                upperLeftCell.x + map.tileAnchor.x, upperLeftCell.y + map.tileAnchor.y, 0f));
            Check(generator != null && Vector2.Distance(cellCenter, generator.transform.position) < 0.01f,
                room.name + ": painted cells align exactly with enemy grid positions (tile=" + cellCenter
                + ", enemy=" + (generator != null ? generator.transform.position.ToString() : "null")
                + ", map=" + map.transform.position + ", bounds=" + bounds + ")");
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Report.Add("PASS " + description);
    }
}
