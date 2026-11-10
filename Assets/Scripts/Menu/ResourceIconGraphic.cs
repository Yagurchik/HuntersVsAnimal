using UnityEngine;
using UnityEngine.UI;

public enum ResourceIconKind { Acorn, Gold, Seed, Diamond }

// Small vector icons keep the four resource badges sharp at every UI scale.
public sealed class ResourceIconGraphic : MaskableGraphic
{
    [SerializeField] private ResourceIconKind kind;

    public ResourceIconKind Kind
    {
        get => kind;
        set { kind = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        switch (kind)
        {
            case ResourceIconKind.Acorn:
                Ellipse(mesh, new Vector2(0f, -0.09f), 0.27f, 0.31f,
                    new Color(0.46f, 0.25f, 0.12f), 0f);
                Ellipse(mesh, new Vector2(-0.08f, -0.06f), 0.08f, 0.2f,
                    new Color(0.72f, 0.48f, 0.23f), -15f);
                Polygon(mesh, new Color(0.75f, 0.54f, 0.28f),
                    new Vector2(-0.34f, 0.09f), new Vector2(-0.27f, 0.27f),
                    new Vector2(0f, 0.35f), new Vector2(0.27f, 0.27f),
                    new Vector2(0.34f, 0.09f));
                Polygon(mesh, new Color(0.41f, 0.27f, 0.13f),
                    new Vector2(-0.04f, 0.33f), new Vector2(0.04f, 0.33f),
                    new Vector2(0.09f, 0.46f), new Vector2(0.01f, 0.46f));
                break;
            case ResourceIconKind.Gold:
                Ellipse(mesh, Vector2.zero, 0.39f, 0.39f, new Color(0.48f, 0.27f, 0.09f), 0f);
                Ellipse(mesh, Vector2.zero, 0.33f, 0.33f, new Color(0.96f, 0.72f, 0.25f), 0f);
                Ellipse(mesh, new Vector2(-0.04f, 0.04f), 0.24f, 0.24f,
                    new Color(1f, 0.84f, 0.43f), 0f);
                Polygon(mesh, new Color(0.7f, 0.43f, 0.12f),
                    new Vector2(-0.04f, -0.18f), new Vector2(0.05f, -0.18f),
                    new Vector2(0.05f, 0.18f), new Vector2(-0.04f, 0.18f));
                break;
            case ResourceIconKind.Seed:
                Polygon(mesh, new Color(0.38f, 0.64f, 0.31f),
                    new Vector2(-0.03f, -0.34f), new Vector2(0.04f, -0.34f),
                    new Vector2(0.04f, 0.13f), new Vector2(-0.03f, 0.13f));
                Ellipse(mesh, new Vector2(-0.19f, 0.13f), 0.2f, 0.1f,
                    new Color(0.55f, 0.78f, 0.4f), 35f);
                Ellipse(mesh, new Vector2(0.19f, 0.13f), 0.2f, 0.1f,
                    new Color(0.41f, 0.67f, 0.32f), -35f);
                Ellipse(mesh, new Vector2(0f, -0.3f), 0.17f, 0.13f,
                    new Color(0.78f, 0.59f, 0.3f), 0f);
                break;
            case ResourceIconKind.Diamond:
                Polygon(mesh, new Color(0.42f, 0.77f, 0.86f),
                    new Vector2(0f, -0.43f), new Vector2(-0.37f, 0.12f),
                    new Vector2(-0.23f, 0.39f), new Vector2(0.23f, 0.39f),
                    new Vector2(0.37f, 0.12f));
                Polygon(mesh, new Color(0.76f, 0.96f, 0.98f),
                    new Vector2(-0.23f, 0.39f), new Vector2(0.23f, 0.39f),
                    new Vector2(0.12f, 0.12f), new Vector2(-0.12f, 0.12f));
                Polygon(mesh, new Color(0.22f, 0.55f, 0.68f),
                    new Vector2(0f, -0.43f), new Vector2(0.37f, 0.12f),
                    new Vector2(0.12f, 0.12f));
                break;
        }
    }

    private void Ellipse(VertexHelper mesh, Vector2 center, float radiusX, float radiusY,
        Color fill, float rotationDegrees)
    {
        const int segments = 20;
        int start = mesh.currentVertCount;
        mesh.AddVert(Point(center), fill * color, Vector2.zero);
        float radians = rotationDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle) * radiusX;
            float y = Mathf.Sin(angle) * radiusY;
            mesh.AddVert(Point(center + new Vector2(x * cosine - y * sine,
                x * sine + y * cosine)), fill * color, Vector2.zero);
            if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
        }
    }

    private void Polygon(VertexHelper mesh, Color fill, params Vector2[] vertices)
    {
        int start = mesh.currentVertCount;
        for (int i = 0; i < vertices.Length; i++)
            mesh.AddVert(Point(vertices[i]), fill * color, Vector2.zero);
        for (int i = 1; i < vertices.Length - 1; i++)
            mesh.AddTriangle(start, start + i, start + i + 1);
    }

    private Vector3 Point(Vector2 normalized)
    {
        Rect bounds = rectTransform.rect;
        float size = Mathf.Min(bounds.width, bounds.height);
        return new Vector3(bounds.center.x + normalized.x * size,
            bounds.center.y + normalized.y * size);
    }
}
