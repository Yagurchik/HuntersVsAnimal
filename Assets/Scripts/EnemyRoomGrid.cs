using System.Collections.Generic;
using UnityEngine;

// Logical cell occupancy: neither sprite animation nor collider dimensions define the grid.
public sealed class EnemyRoomGrid
{
    private readonly bool[,] occupied;
    private readonly Vector3 origin;
    private readonly int columns;
    private readonly int rows;

    public EnemyRoomGrid(LevelGenerator room, int rowCount)
    {
        columns = room.Columns;
        rows = Mathf.Clamp(rowCount, 1, room.Rows);
        origin = room.transform.position;
        occupied = new bool[columns, rows];
        foreach (GameObject enemy in room.EnemyCollection)
        {
            if (enemy == null || !enemy.TryGetComponent(out Brick brick) || !brick.OccupiesGrid)
            {
                continue;
            }

            Reserve(Anchor(brick.transform.position, brick.Footprint), brick, true);
        }
    }

    public Vector2Int Anchor(Vector3 center, Vector2Int size) => new Vector2Int(
        Mathf.RoundToInt(center.x - origin.x - (size.x - 1) * 0.5f),
        Mathf.RoundToInt(origin.y - center.y - (size.y - 1) * 0.5f));

    public Vector3 Center(Vector2Int cell, Vector2Int size) => origin
        + new Vector3(cell.x + (size.x - 1) * 0.5f, -cell.y - (size.y - 1) * 0.5f, 0f);

    public bool CanPlace(Vector2Int cell, Vector2Int size)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x + size.x > columns || cell.y + size.y > rows)
        {
            return false;
        }

        for (int y = cell.y; y < cell.y + size.y; y++)
        {
            for (int x = cell.x; x < cell.x + size.x; x++)
            {
                if (occupied[x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    public void Reserve(Vector2Int cell, Vector2Int size, bool value = true)
    {
        for (int y = Mathf.Max(0, cell.y); y < Mathf.Min(rows, cell.y + size.y); y++)
        {
            for (int x = Mathf.Max(0, cell.x); x < Mathf.Min(columns, cell.x + size.x); x++)
            {
                occupied[x, y] = value;
            }
        }
    }

    public bool CanPlace(Vector2Int cell, Brick brick)
    {
        if (brick == null || cell.x < 0 || cell.y < 0
            || cell.x + brick.Footprint.x > columns || cell.y + brick.Footprint.y > rows)
        {
            return false;
        }

        for (int y = 0; y < brick.Footprint.y; y++)
        {
            for (int x = 0; x < brick.Footprint.x; x++)
            {
                if ((brick.UsesBossHealth || brick.OccupiesFootprintOffset(new Vector2Int(x, y)))
                && occupied[cell.x + x, cell.y + y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    public void Reserve(Vector2Int cell, Brick brick, bool value = true)
    {
        if (brick == null)
        {
            return;
        }

        for (int y = 0; y < brick.Footprint.y; y++)
        {
            for (int x = 0; x < brick.Footprint.x; x++)
            {
                if ((brick.UsesBossHealth || brick.OccupiesFootprintOffset(new Vector2Int(x, y)))
                && cell.x + x >= 0 && cell.x + x < columns && cell.y + y >= 0 && cell.y + y < rows)
                {
                    occupied[cell.x + x, cell.y + y] = value;
                }
            }
        }
    }

    public List<Vector2Int> FreeAnchors(Brick brick)
    {
        var result = new List<Vector2Int>();
        if (brick == null)
        {
            return result;
        }

        for (int y = 0; y <= rows - brick.Footprint.y; y++)
        {
            for (int x = 0; x <= columns - brick.Footprint.x; x++)
        {
            var cell = new Vector2Int(x, y);
            if (CanPlace(cell, brick))
                {
                    result.Add(cell);
                }
            }
        }

        return result;
    }

    public List<Vector2Int> FreeAnchors(Vector2Int size)
    {
        var result = new List<Vector2Int>();
        for (int y = 0; y <= rows - size.y; y++)
        {
            for (int x = 0; x <= columns - size.x; x++)
        {
            var cell = new Vector2Int(x, y);
            if (CanPlace(cell, size))
                {
                    result.Add(cell);
                }
            }
        }

        return result;
    }

    public bool TryFindNearest(Brick summoner, out Vector3 position)
    {
        Vector2Int anchor = Anchor(summoner.transform.position, summoner.Footprint);
        Vector2Int size = summoner.Footprint;
        int bestDistance = int.MaxValue, ties = 0;
        position = default;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
        {
            var cell = new Vector2Int(x, y);
            if (!CanPlace(cell, Vector2Int.one))
                {
                    continue;
                }

                int dx = Mathf.Max(anchor.x - x, x - (anchor.x + size.x - 1), 0);
            int dy = Mathf.Max(anchor.y - y, y - (anchor.y + size.y - 1), 0);
            int distance = Mathf.Max(dx, dy);
            if (distance > bestDistance)
                {
                    continue;
                }

                if (distance < bestDistance) { bestDistance = distance; ties = 0; }
            if (Random.Range(0, ++ties) == 0)
                {
                    position = Center(cell, Vector2Int.one);
                }
            }
        }

        return ties > 0;
    }

    // Plan the complete group before moving a single object. Larger enemies are
    // packed first, with backtracking; singles fill any remaining random cells.
    public bool TryPlan(IReadOnlyList<Brick> enemies, out Dictionary<Brick, Vector3> placements)
    {
        placements = new Dictionary<Brick, Vector3>();
        var sorted = new List<Brick>();
        foreach (Brick brick in enemies)
        {
            if (brick != null && brick.OccupiesGrid && !sorted.Contains(brick))
            {
                sorted.Add(brick);
            }
        }

        sorted.Sort((a, b) => b.OccupiedCellCount.CompareTo(a.OccupiedCellCount));
        int area = 0;
        foreach (Brick brick in sorted)
        {
            area += brick.OccupiedCellCount;
        }

        if (FreeAnchors(Vector2Int.one).Count < area)
        {
            return false;
        }

        return Pack(sorted, 0, placements);
    }

    private bool Pack(List<Brick> enemies, int index, Dictionary<Brick, Vector3> placements)
    {
        if (index == enemies.Count)
        {
            return true;
        }

        Brick brick = enemies[index];
        Vector2Int size = brick.Footprint;
        List<Vector2Int> candidates = FreeAnchors(brick);
        if (size == Vector2Int.one && brick.OccupiedCellCount == 1)
        {
            if (candidates.Count < enemies.Count - index)
            {
                return false;
            }

            for (int i = index; i < enemies.Count; i++)
            {
                int chosen = Random.Range(0, candidates.Count);
                placements[enemies[i]] = Center(candidates[chosen], Vector2Int.one);
                candidates.RemoveAt(chosen);
            }
            return true;
        }
        while (candidates.Count > 0)
        {
            int chosen = Random.Range(0, candidates.Count);
            Vector2Int cell = candidates[chosen];
            candidates.RemoveAt(chosen);
            Reserve(cell, brick);
            placements[brick] = Center(cell, size);
            if (Pack(enemies, index + 1, placements))
            {
                return true;
            }

            placements.Remove(brick);
            Reserve(cell, brick, false);
        }
        return false;
    }
}
