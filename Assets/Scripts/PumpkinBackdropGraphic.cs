using System;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class PumpkinBackdropGraphic : MaskableGraphic
{
    [Serializable]
    public sealed class LocationPalette
    {
        public Color margin;
        public Color edge;
        public Color ornament;
        public Color accent;
    }

    [SerializeField] private LocationPalette[] locationPalettes = new LocationPalette[6];
    [SerializeField] private Color marginColor = new Color(0.20f, 0.075f, 0.028f, 0.56f);
    [SerializeField] private Color edgeColor = new Color(0.055f, 0.018f, 0.012f, 0.72f);
    [SerializeField] private Color pumpkinPattern = new Color(0.72f, 0.27f, 0.055f, 0.28f);
    [SerializeField] private Color leafPattern = new Color(0.28f, 0.38f, 0.13f, 0.30f);
    [SerializeField] private Vector2 windowMin = new Vector2(0.065f, 0.125f);
    [SerializeField] private Vector2 windowMax = new Vector2(0.935f, 0.875f);
    private int activeLocation;

    public Color CurrentEdgeColor => edgeColor;

    public void SetBoardWindow(Vector2 min, Vector2 max)
    {
        if ((windowMin - min).sqrMagnitude < 0.0000001f
            && (windowMax - max).sqrMagnitude < 0.0000001f)
        {
            return;
        }

        windowMin = min;
        windowMax = max;
        SetVerticesDirty();
    }

    public void SetPalette(int locationIndex, Color margin, Color edge,
        Color ornament, Color accent)
    {
        if (locationPalettes == null || locationPalettes.Length != 6)
        {
            locationPalettes = new LocationPalette[6];
        }

        int index = Mathf.Clamp(locationIndex, 1, 6) - 1;
        locationPalettes[index] = new LocationPalette {
            margin = margin, edge = edge, ornament = ornament, accent = accent
        };
    }

    public void ShowLocation(int locationIndex)
    {
        int next = Mathf.Clamp(locationIndex, 1, 6);
        if (activeLocation == next)
        {
            return;
        }

        activeLocation = next;
        LocationPalette theme = locationPalettes != null
            && locationPalettes.Length >= next ? locationPalettes[next - 1] : null;
        if (theme == null)
        {
            return;
        }

        marginColor = theme.margin;
        edgeColor = theme.edge;
        pumpkinPattern = theme.ornament;
        leafPattern = theme.accent;
        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        float left = Mathf.Lerp(rect.xMin, rect.xMax, windowMin.x);
        float right = Mathf.Lerp(rect.xMin, rect.xMax, windowMax.x);
        float bottom = Mathf.Lerp(rect.yMin, rect.yMax, windowMin.y);
        float top = Mathf.Lerp(rect.yMin, rect.yMax, windowMax.y);

        // Four transparent panels leave the battlefield itself untouched.
        AddRect(vertexHelper, new Rect(rect.xMin, rect.yMin, left - rect.xMin, rect.height), marginColor);
        AddRect(vertexHelper, new Rect(right, rect.yMin, rect.xMax - right, rect.height), marginColor);
        AddRect(vertexHelper, new Rect(left, top, right - left, rect.yMax - top), marginColor);
        AddRect(vertexHelper, new Rect(left, rect.yMin, right - left, bottom - rect.yMin), marginColor);

        float edgeWidth = Mathf.Max(8f, rect.width * 0.022f);
        AddRect(vertexHelper, new Rect(rect.xMin, rect.yMin, edgeWidth, rect.height), edgeColor);
        AddRect(vertexHelper, new Rect(rect.xMax - edgeWidth, rect.yMin, edgeWidth, rect.height), edgeColor);

        float thickness = Mathf.Max(1.5f, Mathf.Min(rect.width, rect.height) * 0.0026f);
        AddSideVine(vertexHelper, rect, true, thickness);
        AddSideVine(vertexHelper, rect, false, thickness);
        AddHorizontalOrnaments(vertexHelper, rect, true, thickness);
        AddHorizontalOrnaments(vertexHelper, rect, false, thickness);
    }

    private void AddSideVine(VertexHelper vh, Rect rect, bool leftSide, float thickness)
    {
        float baseX = Mathf.Lerp(rect.xMin, rect.xMax, leftSide ? 0.036f : 0.964f);
        float wave = rect.width * 0.013f;
        Vector2 previous = Vector2.zero;
        const int segments = 44;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float direction = leftSide ? 1f : -1f;
            Vector2 point = new Vector2(
                baseX + Mathf.Sin(t * Mathf.PI * 5f) * wave * direction,
                Mathf.Lerp(rect.yMin, rect.yMax, t));
            if (i > 0)
            {
                AddLine(vh, previous, point, thickness, leafPattern);
            }

            previous = point;

            if (i > 2 && i < segments - 2 && i % 5 == 0)
            {
                float leafDirection = ((i / 5) % 2 == 0 ? 1f : -1f) * direction;
                Vector2 leafCenter = point + Vector2.right * leafDirection * rect.width * 0.011f;
                float angle = leafDirection > 0f ? 32f : -32f;
                AddRotatedQuad(vh, leafCenter,
                    new Vector2(rect.width * 0.022f, rect.height * 0.0065f), angle, leafPattern);
            }
        }
    }

    private void AddHorizontalOrnaments(VertexHelper vh, Rect rect, bool top, float thickness)
    {
        float y = Mathf.Lerp(rect.yMin, rect.yMax, top ? 0.965f : 0.035f);
        float start = Mathf.Lerp(rect.xMin, rect.xMax, 0.13f);
        float end = Mathf.Lerp(rect.xMin, rect.xMax, 0.87f);
        AddLine(vh, new Vector2(start, y), new Vector2(end, y), thickness * 0.72f, pumpkinPattern);

        const int ornaments = 9;
        for (int i = 0; i < ornaments; i++)
        {
            float t = ornaments == 1 ? 0.5f : i / (float)(ornaments - 1);
            Vector2 center = new Vector2(Mathf.Lerp(start, end, t), y);
            float size = i == ornaments / 2 ? 12f : 8f;
            AddRotatedQuad(vh, center, Vector2.one * size, 45f, pumpkinPattern);
            if (i % 2 == 1)
            {
                float direction = top ? -1f : 1f;
                AddRotatedQuad(vh, center + Vector2.up * direction * 10f,
                    new Vector2(13f, 5f), i % 4 == 1 ? 28f : -28f, leafPattern);
            }
        }
    }

    private static void AddRect(VertexHelper vh, Rect rect, Color color)
    {
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        AddQuad(vh,
            new Vector2(rect.center.x, rect.center.y),
            new Vector2(rect.width, rect.height), 0f, color);
    }

    private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 direction = b - a;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        AddQuad(vh, (a + b) * 0.5f,
            new Vector2(direction.magnitude, thickness),
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, color);
    }

    private static void AddRotatedQuad(VertexHelper vh, Vector2 center, Vector2 size, float angle, Color color)
    {
        AddQuad(vh, center, size, angle, color);
    }

    private static void AddQuad(VertexHelper vh, Vector2 center, Vector2 size, float angle, Color color)
    {
        int first = vh.currentVertCount;
        float radians = angle * Mathf.Deg2Rad;
        Vector2 x = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * size.x * 0.5f;
        Vector2 y = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * size.y * 0.5f;
        vh.AddVert(center - x - y, color, Vector2.zero);
        vh.AddVert(center - x + y, color, Vector2.up);
        vh.AddVert(center + x + y, color, Vector2.one);
        vh.AddVert(center + x - y, color, Vector2.right);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }
}
