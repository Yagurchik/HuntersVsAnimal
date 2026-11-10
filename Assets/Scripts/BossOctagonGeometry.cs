using UnityEngine;

/// <summary>Square boss collision with four short, real 45-degree bevels.</summary>
public static class BossOctagonGeometry
{
    public const float CornerCut = 0.15f;

    public static Vector2[] CreatePoints(Vector2 size, float cornerCut = CornerCut)
    {
        float halfX = Mathf.Max(0.01f, size.x * 0.5f);
        float halfY = Mathf.Max(0.01f, size.y * 0.5f);
        float cut = Mathf.Clamp(cornerCut, 0.01f, Mathf.Min(halfX, halfY) - 0.01f);
        return new[]
        {
            new Vector2(-halfX + cut, -halfY),
            new Vector2( halfX - cut, -halfY),
            new Vector2( halfX, -halfY + cut),
            new Vector2( halfX,  halfY - cut),
            new Vector2( halfX - cut,  halfY),
            new Vector2(-halfX + cut,  halfY),
            new Vector2(-halfX,  halfY - cut),
            new Vector2(-halfX, -halfY + cut)
        };
    }
}
