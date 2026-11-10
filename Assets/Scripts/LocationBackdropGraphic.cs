using UnityEngine;
using UnityEngine.UI;

// Authored in Asset.unity, behind the decorative HUD. Only its four outer
// panels contain geometry, leaving the actual battlefield unobscured.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class LocationBackdropGraphic : MaskableGraphic
{
    [SerializeField] private Texture2D[] locationArtwork = new Texture2D[6];
    [SerializeField] private Vector2 windowMin = new Vector2(0.065f, 0.125f);
    [SerializeField] private Vector2 windowMax = new Vector2(0.935f, 0.875f);
    [SerializeField, Range(0f, 1f)] private float opacity = 0.88f;
    [SerializeField, Range(1, 6)] private int previewLocation = 1;
    private int activeLocation;

    public Rect BoardWindow => Rect.MinMaxRect(windowMin.x, windowMin.y,
        windowMax.x, windowMax.y);

    public override Texture mainTexture
    {
        get
        {
            int index = Mathf.Clamp(activeLocation > 0 ? activeLocation : previewLocation,
                1, locationArtwork.Length) - 1;
            Texture texture = locationArtwork[index];
            return texture != null ? texture : Texture2D.whiteTexture;
        }
    }

    public void ShowLocation(int index)
    {
        int next = Mathf.Clamp(index, 1, 6);
        if (activeLocation == next)
        {
            return;
        }

        activeLocation = next;
        SetMaterialDirty();
        SetVerticesDirty();
    }

    // The cut-out follows the real board edges on every screen aspect ratio.
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

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        float left = Mathf.Lerp(rect.xMin, rect.xMax, windowMin.x);
        float right = Mathf.Lerp(rect.xMin, rect.xMax, windowMax.x);
        float bottom = Mathf.Lerp(rect.yMin, rect.yMax, windowMin.y);
        float top = Mathf.Lerp(rect.yMin, rect.yMax, windowMax.y);
        Color tint = new Color(1f, 1f, 1f, opacity) * color;
        AddPanel(vh, rect, new Rect(rect.xMin, rect.yMin,
            left - rect.xMin, rect.height), tint);
        AddPanel(vh, rect, new Rect(right, rect.yMin,
            rect.xMax - right, rect.height), tint);
        AddPanel(vh, rect, new Rect(left, top,
            right - left, rect.yMax - top), tint);
        AddPanel(vh, rect, new Rect(left, rect.yMin,
            right - left, bottom - rect.yMin), tint);
    }

    private static void AddPanel(VertexHelper vh, Rect canvas,
        Rect panel, Color tint)
    {
        if (panel.width <= 0f || panel.height <= 0f)
        {
            return;
        }

        int first = vh.currentVertCount;
        AddVertex(vh, canvas, new Vector2(panel.xMin, panel.yMin), tint);
        AddVertex(vh, canvas, new Vector2(panel.xMin, panel.yMax), tint);
        AddVertex(vh, canvas, new Vector2(panel.xMax, panel.yMax), tint);
        AddVertex(vh, canvas, new Vector2(panel.xMax, panel.yMin), tint);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }

    private static void AddVertex(VertexHelper vh, Rect canvas,
        Vector2 position, Color tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = tint;
        vertex.uv0 = new Vector2(
            Mathf.InverseLerp(canvas.xMin, canvas.xMax, position.x),
            Mathf.InverseLerp(canvas.yMin, canvas.yMax, position.y));
        vh.AddVert(vertex);
    }
}
