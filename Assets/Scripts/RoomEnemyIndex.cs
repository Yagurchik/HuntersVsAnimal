using System.Collections.Generic;
using UnityEngine;

// Index enemy centers in world-space unit cells. Precise circle/square tests
// remain identical to combat rules, including fractional positions during steps.
public sealed class RoomEnemyIndex
{
    private readonly LevelGenerator owner;
    private readonly Dictionary<Vector2Int, List<Brick>> cells = new Dictionary<Vector2Int, List<Brick>>();
    private readonly List<List<Brick>> bucketStorage = new List<List<Brick>>();
    private int builtFrame = -1;
    private int builtCount = -1;
    public RoomEnemyIndex(LevelGenerator room) => owner = room;
    public void Invalidate() => builtFrame = -1;
    private void Rebuild()
    {
        if (builtFrame == Time.frameCount && builtCount == owner.EnemyCollection.Count)
        {
            return;
        }

        foreach (var bucket in bucketStorage)
        {
            bucket.Clear();
        }

        cells.Clear();
        int used = 0;
        foreach (var candidate in owner.EnemyCollection)
        {
            if (candidate == null || !candidate.TryGetComponent(out Brick enemy))
            {
                continue;
            }

            Vector3 point = enemy.transform.position;
            var key = new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
            if (!cells.TryGetValue(key, out var bucket))
            {
                if (used == bucketStorage.Count)
                {
                    bucketStorage.Add(new List<Brick>(4));
                }

                bucket = bucketStorage[used++]; cells.Add(key, bucket);
            }
            bucket.Add(enemy);
        }
        builtFrame = Time.frameCount; builtCount = owner.EnemyCollection.Count;
    }
    public void Collect(Vector2 center, float radius, bool square, Brick excluded, List<Brick> result)
    {
        Rebuild();
        float squareRadius = radius * radius + 0.001f;
        int xMin = Mathf.FloorToInt(center.x - radius - 0.001f), xMax = Mathf.FloorToInt(center.x + radius + 0.001f);
        int yMin = Mathf.FloorToInt(center.y - radius - 0.001f), yMax = Mathf.FloorToInt(center.y + radius + 0.001f);
        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
        {
            if (!cells.TryGetValue(new Vector2Int(x, y), out var bucket))
                {
                    continue;
                }

                foreach (var enemy in bucket)
            {
                if (enemy == null || enemy == excluded || enemy.IsDefeated || enemy.hp <= 0f)
                    {
                        continue;
                    }

                    Vector2 gap = (Vector2)enemy.transform.position - center;
                if (square ? Mathf.Max(Mathf.Abs(gap.x), Mathf.Abs(gap.y)) <= radius + 0.001f : gap.sqrMagnitude <= squareRadius)
                    {
                        result.Add(enemy);
                    }
                }
        }
        }
    }
}
