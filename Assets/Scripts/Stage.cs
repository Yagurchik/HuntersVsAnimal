using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(RoomDarkness))]
public class Stage : MonoBehaviour
{
    public const string ForgottenBayTilemapName = "Forgotten Bay Tilemap";
    public const string SnowyHazeTilemapName = "Snowy Haze Tilemap";
    public const string FieryGehennaTilemapName = "Fiery Gehenna Tilemap";
    public const string GloomyHollowTilemapName = "Gloomy Hollow Tilemap";
    public const string DrownedBogTilemapName = "Drowned Bog Tilemap";
    public const int MaxBattlefieldHeight = 10;
    public const float ReturnZoneDepth = 1f;
    public const float WallExitExtension = 0.25f;
    public const float BreachBoundaryInset = 1f;
    [SerializeField] private GameObject player;
    [SerializeField, Range(1, 4)] private int defenseLine = 1;
    [SerializeField] private BattlefieldTilePalette battlefieldTilePalette;
    [SerializeField] private TileBase forgottenBayFloorTile;
    [SerializeField] private TileBase snowyHazeFloorTile;
    [SerializeField] private TileBase fieryGehennaFloorTile;
    [SerializeField] private TileBase gloomyHollowFloorTile;
    [SerializeField] private TileBase drownedBogFloorTile;
    [Header("Battlefield checkerboard (editable in the scene)")]
    [SerializeField] private Color lightCellTint = Color.white;
    [SerializeField] private Color darkCellTint = new Color(0.79f, 0.79f, 0.79f, 1f);
    [Header("Lower field (visual only)")]
    [SerializeField] private Color lowerFieldTint = new Color(0.56f, 0.56f, 0.56f, 1f);
    [HideInInspector] public Orientation orientation = Orientation.up;

    public GameObject PlayerObject => player;
    public int DefenseLine => defenseLine;
    public Vector2Int BattlefieldSize => SizeForLine(defenseLine);
    public float FieldBottomY => transform.position.y - BattlefieldSize.y * 0.5f;
    public float DefenseBoundaryY => FieldBottomY + BreachBoundaryInset;
    public Vector3 DefenseHealthAnchor
    {
        get
        {
            BattlefieldForestStyle style = GetComponent<BattlefieldForestStyle>();
            SpriteRenderer frame = style != null ? style.FrameRenderer : null;
            if (frame != null && frame.enabled && frame.sprite != null)
                return new Vector3(frame.bounds.center.x, frame.bounds.min.y,
                    frame.transform.position.z);
            LevelGenerator field = GetComponentInChildren<LevelGenerator>(true);
            return new Vector3(transform.position.x, FieldBottomY - 0.5f,
                field != null ? field.transform.position.z : transform.position.z);
        }
    }
    public BattlefieldTilePalette TilePalette => ResolveBattlefieldTilePalette();
    public TileBase ForgottenBayFloorTile => forgottenBayFloorTile;
    public TileBase SnowyHazeFloorTile => snowyHazeFloorTile;
    public TileBase FieryGehennaFloorTile => fieryGehennaFloorTile;
    public TileBase GloomyHollowFloorTile => gloomyHollowFloorTile;
    public TileBase DrownedBogFloorTile => drownedBogFloorTile;
    public Color LowerFieldTint => lowerFieldTint;

    public float ClampPlayerX(float worldX, float horizontalHalfSize)
    {
        float halfWidth = BattlefieldSize.x * 0.5f;
        float inset = Mathf.Clamp(horizontalHalfSize, 0f, Mathf.Max(0f, halfWidth - 0.05f));
        return Mathf.Clamp(worldX, transform.position.x - halfWidth + inset,
            transform.position.x + halfWidth - inset);
    }

    public static Vector2Int SizeForLine(int line)
    {
        line = Mathf.Clamp(line, 1, 4);
        return new Vector2Int(9 - line, 11 - line);
    }

    // Called before any waves spawn. Also used by the editor's room layout command.
    public void ConfigureBattlefield(int line)
    {
        defenseLine = Mathf.Clamp(line, 1, 4);
        orientation = Orientation.up;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        Vector2Int size = BattlefieldSize;
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        LevelGenerator generator = GetComponentInChildren<LevelGenerator>(true);
        if (generator != null)
        {
            generator.ConfigureBattlefieldSize(size);
            SetRoomPosition(generator.transform, -(size.x - 1) * 0.5f, (size.y - 1) * 0.5f);
            generator.transform.rotation = Quaternion.identity;
            generator.transform.localScale = Vector3.one;
        }
        if (player != null)
        {
            SetRoomPosition(player.transform, 0f, -halfHeight + 0.25f);
            player.transform.rotation = Quaternion.identity;
        }

        foreach (EdgeCollider2D wall in GetComponentsInChildren<EdgeCollider2D>(true))
        {
            if (!wall.name.StartsWith("Walls")) continue;
            SetRoomPosition(wall.transform, 0f, 0f);
            wall.transform.rotation = Quaternion.identity;
            wall.transform.localScale = Vector3.one;
            wall.offset = Vector2.zero;
            wall.points = new[] {
                new Vector2(-halfWidth, -halfHeight - ReturnZoneDepth - WallExitExtension),
                new Vector2(-halfWidth, halfHeight),
                new Vector2(halfWidth, halfHeight),
                new Vector2(halfWidth, -halfHeight - ReturnZoneDepth - WallExitExtension)
            };
        }
        foreach (BoxCollider2D box in GetComponentsInChildren<BoxCollider2D>(true))
        {
            bool touch = box.name.StartsWith("CollisionTouch");
            bool exit = box.name.StartsWith("DestroyBallCollider") || box.name.StartsWith("CollisionEnemyLose");
            if (!touch && !exit) continue;
            SetRoomPosition(box.transform, 0f, touch ? 0f : -halfHeight - 0.5f);
            box.transform.rotation = Quaternion.identity;
            // Retain the existing transparent sprite and collider setup at unit scale.
            box.transform.localScale = Vector3.one;
            box.offset = Vector2.zero;
            box.size = touch ? (Vector2)size : new Vector2(size.x + 2f, 1f);
        }
        DestroyBallCollider returnZone = EnsureReturnZone();
        BoxCollider2D returnCollider = returnZone.GetComponent<BoxCollider2D>();
        returnZone.gameObject.SetActive(true);
        returnZone.enabled = true;
        returnCollider.enabled = true;
        SetRoomPosition(returnZone.transform, 0f, -halfHeight - ReturnZoneDepth * 0.5f);
        returnZone.transform.rotation = Quaternion.identity;
        returnZone.transform.localScale = Vector3.one;
        returnCollider.offset = Vector2.zero;
        returnCollider.size = new Vector2(size.x + 2f, ReturnZoneDepth);
        returnCollider.isTrigger = true;
        RefreshBattlefieldTiles();

        RoomDarkness darkness = GetComponent<RoomDarkness>();
        if (darkness == null) darkness = gameObject.AddComponent<RoomDarkness>();
        darkness.Refresh();

        BattlefieldForestStyle forestStyle = GetComponent<BattlefieldForestStyle>();
        if (forestStyle != null) forestStyle.Refresh(this);
    }

    public void RefreshBattlefieldTiles()
    {
        Vector2Int size = BattlefieldSize;
        LevelGenerator generator = GetComponentInChildren<LevelGenerator>(true);
        BattlefieldTilePalette palette = ResolveBattlefieldTilePalette();
        foreach (Tilemap tiles in GetComponentsInChildren<Tilemap>(true))
        {
            if (tiles.name == ForgottenBayTilemapName && forgottenBayFloorTile != null)
                PaintTiles(tiles, size, null, generator, forgottenBayFloorTile);
            else if (tiles.name == SnowyHazeTilemapName && snowyHazeFloorTile != null)
                PaintTiles(tiles, size, null, generator, snowyHazeFloorTile);
            else if (tiles.name == FieryGehennaTilemapName && fieryGehennaFloorTile != null)
                PaintTiles(tiles, size, null, generator, fieryGehennaFloorTile);
            else if (tiles.name == GloomyHollowTilemapName && gloomyHollowFloorTile != null)
                PaintTiles(tiles, size, null, generator, gloomyHollowFloorTile);
            else if (tiles.name == DrownedBogTilemapName && drownedBogFloorTile != null)
                PaintTiles(tiles, size, null, generator, drownedBogFloorTile);
            else if (palette != null && palette.IsConfigured)
                PaintTiles(tiles, size, palette, generator);
            else ResizeTiles(tiles, size);
        }
    }

    private DestroyBallCollider EnsureReturnZone()
    {
        DestroyBallCollider returnZone = GetComponentInChildren<DestroyBallCollider>(true);
        if (returnZone != null) return returnZone;

        Transform zoneTransform = null;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("DestroyBallCollider"))
            {
                zoneTransform = child;
                break;
            }
        }

        if (zoneTransform == null)
        {
            GameObject zoneObject = new GameObject("DestroyBallCollider");
            zoneTransform = zoneObject.transform;
            zoneTransform.SetParent(transform, false);
            Vector3 localPosition = zoneTransform.localPosition;
            localPosition.z = player != null ? player.transform.localPosition.z : 0f;
            zoneTransform.localPosition = localPosition;
        }

        if (zoneTransform.GetComponent<BoxCollider2D>() == null)
            zoneTransform.gameObject.AddComponent<BoxCollider2D>();
        return zoneTransform.gameObject.AddComponent<DestroyBallCollider>();
    }

    public void SetBattlefieldTilePalette(BattlefieldTilePalette palette)
    {
        battlefieldTilePalette = palette;
    }

    public void SetForgottenBayFloorTile(TileBase tile)
    {
        forgottenBayFloorTile = tile;
    }

    public void SetSnowyHazeFloorTile(TileBase tile)
    {
        snowyHazeFloorTile = tile;
    }

    public void SetFieryGehennaFloorTile(TileBase tile)
    {
        fieryGehennaFloorTile = tile;
    }

    public void SetGloomyHollowFloorTile(TileBase tile)
    {
        gloomyHollowFloorTile = tile;
    }

    public void SetDrownedBogFloorTile(TileBase tile)
    {
        drownedBogFloorTile = tile;
    }

    private BattlefieldTilePalette ResolveBattlefieldTilePalette()
    {
        if (battlefieldTilePalette == null)
            battlefieldTilePalette = Resources.Load<BattlefieldTilePalette>(
                "Battlefield/TwilightThicket/TwilightThicketPalette");
        return battlefieldTilePalette;
    }

    private void SetRoomPosition(Transform target, float x, float y)
    {
        Vector3 position = target.position;
        position.x = transform.position.x + x;
        position.y = transform.position.y + y;
        target.position = position;
    }

    private void ResizeTiles(Tilemap tiles, Vector2Int size)
    {
        tiles.CompressBounds();
        BoundsInt source = tiles.cellBounds;
        if (source.size.x < 1 || source.size.y < 1) return;

        int width = size.x;
        int height = size.y;
        Vector3Int origin = new Vector3Int(-width / 2, -height / 2, source.zMin);
        bool sourceHasOuterBorder = source.size.x == width + 2 && source.size.y == height + 2;
        int sampleXMin = source.xMin + (sourceHasOuterBorder ? 1 : 0);
        int sampleYMin = source.yMin + (sourceHasOuterBorder ? 1 : 0);
        int sampleWidth = Mathf.Max(1, source.size.x - (sourceHasOuterBorder ? 2 : 0));
        int sampleHeight = Mathf.Max(1, source.size.y - (sourceHasOuterBorder ? 2 : 0));
        var positions = new Vector3Int[width * height];
        var assets = new TileBase[positions.Length];
        var matrices = new Matrix4x4[positions.Length];
        var colors = new Color[positions.Length];
        var flags = new TileFlags[positions.Length];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * width + x;
            // Strip the old one-cell outer border, then repeat only playable terrain.
            int sx = sampleXMin + x % sampleWidth;
            int sy = sampleYMin + y % sampleHeight;
            Vector3Int sample = new Vector3Int(sx, sy, source.zMin);
            positions[i] = origin + new Vector3Int(x, y, 0);
            assets[i] = tiles.GetTile(sample);
            matrices[i] = tiles.GetTransformMatrix(sample);
            colors[i] = tiles.GetColor(sample);
            flags[i] = tiles.GetTileFlags(sample);
        }
        tiles.ClearAllTiles();
        tiles.SetTiles(positions, assets);
        for (int i = 0; i < positions.Length; i++)
        {
            if (assets[i] == null) continue;
            tiles.SetTileFlags(positions[i], TileFlags.None);
            tiles.SetTransformMatrix(positions[i], matrices[i]);
            tiles.SetColor(positions[i], colors[i]);
            tiles.SetTileFlags(positions[i], flags[i]);
        }
        tiles.CompressBounds();
        tiles.transform.rotation = Quaternion.identity;
        // Use the unit-cell layout directly: inactive Tilemaps can return stale CellToWorld data.
        // Odd-sized boards need a half-cell offset before their first activation.
        SetRoomPosition(tiles.transform, -(origin.x + width * 0.5f), -(origin.y + height * 0.5f));
    }

    private void PaintTiles(Tilemap tiles, Vector2Int size, BattlefieldTilePalette palette,
        LevelGenerator generator, TileBase uniformTile = null)
    {
        int width = size.x;
        int height = size.y;
        Vector3Int origin = new Vector3Int(-width / 2, -height / 2, 0);
        var bounds = new BoundsInt(origin, new Vector3Int(width, height, 1));
        var assets = new TileBase[width * height];
        int roomIndex = generator != null ? generator.RoomIndex : 1;
        int seed = defenseLine * 97 + roomIndex * 31;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            assets[y * width + x] = uniformTile != null
                ? uniformTile : palette.GetFloorTile(x, y, seed);

        tiles.ClearAllTiles();
        tiles.SetTilesBlock(bounds, assets);
        // Enemy rows stay checkerboard. The pumpkin row shares one darker tone;
        // the visual-only continuation below the grid is a separate renderer.
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Vector3Int cell = origin + new Vector3Int(x, y, 0);
            tiles.SetTileFlags(cell, TileFlags.None);
            tiles.SetColor(cell, y == 0 ? lowerFieldTint
                : ((x + y) & 1) == 0 ? lightCellTint : darkCellTint);
            tiles.SetTransformMatrix(cell, Matrix4x4.identity);
        }
        tiles.CompressBounds();
        tiles.color = Color.white;
        tiles.transform.rotation = Quaternion.identity;
        TilemapRenderer renderer = tiles.GetComponent<TilemapRenderer>();
        if (renderer != null) renderer.sortingOrder = -20;
        SetRoomPosition(tiles.transform, -(origin.x + width * 0.5f), -(origin.y + height * 0.5f));
    }

    public Bounds GetViewBounds()
    {
        Vector2Int size = BattlefieldSize;
        return new Bounds(transform.position, new Vector3(size.x, size.y, 0f));
    }

    private void OnEnable()
    {
        SetPlayerActive(true);
    }
    private void OnDisable()
    {
        SetPlayerActive(false);
    }

    public void ConfigureOrientation(Orientation newOrientation)
    {
        orientation = newOrientation;
    }

    public void SetPlayerActive(bool active)
    {
        if (player != null)
        {
            player.SetActive(active);
        }
    }

}
public enum Orientation
{
    left,
    up,
    right
}
