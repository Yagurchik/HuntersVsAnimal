using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "BattlefieldTilePalette", menuName = "Forest Animal War/Battlefield Tile Palette")]
public sealed class BattlefieldTilePalette : ScriptableObject
{
    [Header("Corners")]
    [SerializeField] private TileBase topLeft;
    [SerializeField] private TileBase topRight;
    [SerializeField] private TileBase bottomLeft;
    [SerializeField] private TileBase bottomRight;

    [Header("Edges")]
    [SerializeField] private TileBase[] topEdges = new TileBase[2];
    [SerializeField] private TileBase[] rightEdges = new TileBase[2];
    [SerializeField] private TileBase[] bottomEdges = new TileBase[2];
    [SerializeField] private TileBase[] leftEdges = new TileBase[2];

    [Header("Playable floor")]
    [SerializeField] private TileBase[] floorTiles = new TileBase[4];

    public bool IsConfigured => topLeft != null && topRight != null && bottomLeft != null
        && bottomRight != null && HasTiles(topEdges) && HasTiles(rightEdges)
        && HasTiles(bottomEdges) && HasTiles(leftEdges) && HasTiles(floorTiles);

    public IReadOnlyList<TileBase> FloorTiles => floorTiles;

    public string ConfigurationSummary => "corners=" + CountConfigured(topLeft, topRight, bottomLeft, bottomRight)
        + "/4, top=" + CountConfigured(topEdges) + "/" + ArrayLength(topEdges)
        + ", right=" + CountConfigured(rightEdges) + "/" + ArrayLength(rightEdges)
        + ", bottom=" + CountConfigured(bottomEdges) + "/" + ArrayLength(bottomEdges)
        + ", left=" + CountConfigured(leftEdges) + "/" + ArrayLength(leftEdges)
        + ", floor=" + CountConfigured(floorTiles) + "/" + ArrayLength(floorTiles);

    public TileBase GetTile(int x, int y, int width, int height, int seed)
    {
        bool left = x == 0;
        bool right = x == width - 1;
        bool bottom = y == 0;
        bool top = y == height - 1;
        if (top && left) return topLeft;
        if (top && right) return topRight;
        if (bottom && left) return bottomLeft;
        if (bottom && right) return bottomRight;
        if (top) return Pick(topEdges, x, y, seed);
        if (right) return Pick(rightEdges, x, y, seed);
        if (bottom) return Pick(bottomEdges, x, y, seed);
        if (left) return Pick(leftEdges, x, y, seed);
        return Pick(floorTiles, x, y, seed);
    }

    public TileBase GetFloorTile(int x, int y, int seed)
    {
        return Pick(floorTiles, x, y, seed);
    }

    // Atlas order is left-to-right, top-to-bottom.
    public void ConfigureFromAtlas(IReadOnlyList<TileBase> tiles)
    {
        if (tiles == null || tiles.Count != 16) return;
        topLeft = tiles[0];
        topEdges = new[] { tiles[1], tiles[2] };
        topRight = tiles[3];
        leftEdges = new[] { tiles[4], tiles[8] };
        floorTiles = new[] { tiles[5], tiles[6], tiles[9], tiles[10] };
        rightEdges = new[] { tiles[7], tiles[11] };
        bottomLeft = tiles[12];
        bottomEdges = new[] { tiles[13], tiles[14] };
        bottomRight = tiles[15];
    }

    private static bool HasTiles(TileBase[] tiles)
    {
        if (tiles == null || tiles.Length == 0) return false;
        for (int i = 0; i < tiles.Length; i++) if (tiles[i] == null) return false;
        return true;
    }

    private static int ArrayLength(TileBase[] tiles) => tiles != null ? tiles.Length : 0;

    private static int CountConfigured(params TileBase[] tiles)
    {
        if (tiles == null) return 0;
        int count = 0;
        for (int i = 0; i < tiles.Length; i++) if (tiles[i] != null) count++;
        return count;
    }

    private static TileBase Pick(TileBase[] tiles, int x, int y, int seed)
    {
        if (tiles == null || tiles.Length == 0) return null;
        unchecked
        {
            int hash = seed;
            hash = hash * 397 ^ x;
            hash = hash * 397 ^ y;
            hash ^= hash >> 16;
            return tiles[(hash & int.MaxValue) % tiles.Length];
        }
    }
}
