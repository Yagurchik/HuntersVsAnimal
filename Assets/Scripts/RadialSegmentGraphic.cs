using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RadialSegmentGraphic : MaskableGraphic
{
    [SerializeField, Range(0f, 0.95f)] private float innerRadius = 0.58f;
    [SerializeField] private float startAngle = -90f;
    [SerializeField] private float endAngle = 90f;
    [SerializeField, Range(3, 96)] private int segments = 48;

    public void Configure(float normalizedInnerRadius, float fromAngle, float toAngle, int segmentCount)
    {
        innerRadius = Mathf.Clamp(normalizedInnerRadius, 0f, 0.95f);
        startAngle = fromAngle;
        endAngle = toAngle;
        segments = Mathf.Clamp(segmentCount, 3, 96);
        SetVerticesDirty();
    }

    public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
    {
        if (!base.Raycast(screenPoint, eventCamera)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;
        float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
        float distance = Vector2.Distance(localPoint, rect.center);
        if (distance < outer * innerRadius || distance > outer)
        {
            return false;
        }

        if (Mathf.Abs(endAngle - startAngle) >= 359.9f)
        {
            return true;
        }

        float pointAngle = Mathf.Atan2(localPoint.y - rect.center.y,
            localPoint.x - rect.center.x) * Mathf.Rad2Deg;
        float sweep = Mathf.Repeat(endAngle - startAngle, 360f);
        float fromStart = Mathf.Repeat(pointAngle - startAngle, 360f);
        return fromStart <= sweep;
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
        float inner = outer * innerRadius;
        Vector2 center = rect.center;

        for (int i = 0; i < segments; i++)
        {
            float t0 = i / (float)segments;
            float t1 = (i + 1) / (float)segments;
            float angle0 = Mathf.Lerp(startAngle, endAngle, t0) * Mathf.Deg2Rad;
            float angle1 = Mathf.Lerp(startAngle, endAngle, t1) * Mathf.Deg2Rad;

            Vector2 direction0 = new Vector2(Mathf.Cos(angle0), Mathf.Sin(angle0));
            Vector2 direction1 = new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1));

            int vertex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center + direction0 * inner);
            AddVertex(vertexHelper, center + direction0 * outer);
            AddVertex(vertexHelper, center + direction1 * outer);
            AddVertex(vertexHelper, center + direction1 * inner);

            vertexHelper.AddTriangle(vertex, vertex + 1, vertex + 2);
            vertexHelper.AddTriangle(vertex, vertex + 2, vertex + 3);
        }
    }

    private void AddVertex(VertexHelper vertexHelper, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = position;
        vertexHelper.AddVert(vertex);
    }
}
