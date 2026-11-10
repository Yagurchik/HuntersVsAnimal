using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RoomDarknessGraphic : MaskableGraphic
{
    public readonly struct LightDisc
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly float Softness;

        public LightDisc(Vector2 center, float radius, float softness)
        {
            Center = center;
            Radius = Mathf.Max(0.01f, radius);
            Softness = Mathf.Max(0.01f, softness);
        }
    }

    public readonly struct ShadowDisc
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly float Softness;

        public ShadowDisc(Vector2 center, float radius, float softness)
        {
            Center = center;
            Radius = Mathf.Max(0.01f, radius);
            Softness = Mathf.Max(0.01f, softness);
        }
    }

    private const float MeshCellSize = 0.25f;
    private float litFraction = 1f;
    private float softness = 0.5f;
    private readonly List<LightDisc> lights = new List<LightDisc>();
    private readonly List<ShadowDisc> shadows = new List<ShadowDisc>();
    private float[] alphaSamples = new float[0];
    private Rect sampleArea;
    private Color sampleColor;
    private bool samplesDirty = true;

    public int LightCount => lights.Count;

    public void Configure(float lighting, float edgeWidth, Color tint)
    {
        Configure(lighting, edgeWidth, tint, null);
    }

    public void Configure(float lighting, float edgeWidth, Color tint, IReadOnlyList<LightDisc> localLights)
    {
        Configure(lighting, edgeWidth, tint, localLights, null);
    }

    public void Configure(float lighting, float edgeWidth, Color tint,
        IReadOnlyList<LightDisc> localLights, IReadOnlyList<ShadowDisc> localShadows)
    {
        lighting = Mathf.Clamp01(lighting);
        edgeWidth = Mathf.Max(0f, edgeWidth);
        tint.a = Mathf.Clamp01(tint.a);
        raycastTarget = false;
        int lightCount = localLights != null ? localLights.Count : 0;
        int shadowCount = localShadows != null ? localShadows.Count : 0;
        bool changed = litFraction != lighting || softness != edgeWidth || !color.Equals(tint)
            || lights.Count != lightCount || shadows.Count != shadowCount;
        for (int i = 0; !changed && i < lightCount; i++)
            changed = !lights[i].Center.Equals(localLights[i].Center) || lights[i].Radius != localLights[i].Radius
                || lights[i].Softness != localLights[i].Softness;
        for (int i = 0; !changed && i < shadowCount; i++)
            changed = !shadows[i].Center.Equals(localShadows[i].Center)
                || shadows[i].Radius != localShadows[i].Radius
                || shadows[i].Softness != localShadows[i].Softness;
        if (!changed) return;
        litFraction = lighting;
        softness = edgeWidth;
        color = tint;
        lights.Clear();
        if (localLights != null)
            for (int i = 0; i < localLights.Count; i++) lights.Add(localLights[i]);
        shadows.Clear();
        if (localShadows != null)
            for (int i = 0; i < localShadows.Count; i++) shadows.Add(localShadows[i]);
        samplesDirty = true;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (litFraction >= 1f && shadows.Count == 0) return;
        Rect area = rectTransform.rect;
        if (lights.Count > 0 || shadows.Count > 0)
        {
            AddLightGrid(mesh, area);
            return;
        }
        float start = Mathf.Lerp(area.yMin, area.yMax, litFraction);
        float end = Mathf.Min(area.yMax, start + (litFraction <= 0f ? 0f : softness));
        const int segments = 12;
        for (int i = 0; i < segments && end > start; i++)
        {
            float a = (float)i / segments;
            float b = (float)(i + 1) / segments;
            AddStrip(mesh, area, Mathf.Lerp(start, end, a), Mathf.Lerp(start, end, b),
                Mathf.SmoothStep(0f, 1f, a), Mathf.SmoothStep(0f, 1f, b));
        }
        AddStrip(mesh, area, end, area.yMax, 1f, 1f);
    }

    private void AddStrip(VertexHelper mesh, Rect area, float bottom, float top, float bottomAlpha, float topAlpha)
    {
        if (top <= bottom) return;
        int first = mesh.currentVertCount;
        Color low = color;
        Color high = color;
        low.a *= bottomAlpha;
        high.a *= topAlpha;
        mesh.AddVert(new Vector3(area.xMin, bottom), low, Vector2.zero);
        mesh.AddVert(new Vector3(area.xMax, bottom), low, Vector2.zero);
        mesh.AddVert(new Vector3(area.xMax, top), high, Vector2.zero);
        mesh.AddVert(new Vector3(area.xMin, top), high, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first + 2, first + 3, first);
    }

    public float GetAlphaAt(Vector2 localPosition)
    {
        Rect area = rectTransform.rect;
        float start = Mathf.Lerp(area.yMin, area.yMax, litFraction);
        float end = Mathf.Min(area.yMax, start + (litFraction <= 0f ? 0f : softness));
        return SampleAlpha(localPosition, start, end);
    }

    private float SampleAlpha(Vector2 localPosition, float start, float end)
    {
        float alpha;
        if (localPosition.y <= start) alpha = litFraction <= 0f ? 1f : 0f;
        else if (end <= start) alpha = 1f;
        else alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, localPosition.y));

        for (int i = 0; i < shadows.Count; i++)
        {
            ShadowDisc shadow = shadows[i];
            float distance = Vector2.Distance(localPosition, shadow.Center);
            float strength = distance <= shadow.Radius ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(shadow.Radius, shadow.Radius + shadow.Softness, distance));
            alpha = Mathf.Max(alpha, 0.8f * strength);
        }

        for (int i = 0; i < lights.Count && alpha > 0f; i++)
        {
            LightDisc light = lights[i];
            float distance = Vector2.Distance(localPosition, light.Center);
            float lightMultiplier = distance <= light.Radius ? 0f
                : Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(light.Radius, light.Radius + light.Softness, distance));
            alpha *= lightMultiplier;
        }
        return alpha * color.a;
    }

    private void AddLightGrid(VertexHelper mesh, Rect area)
    {
        int columns = Mathf.Max(1, Mathf.CeilToInt(area.width / MeshCellSize));
        int rows = Mathf.Max(1, Mathf.CeilToInt(area.height / MeshCellSize));
        int stride = columns + 1;
        int sampleCount = stride * (rows + 1);
        if (alphaSamples.Length < sampleCount) alphaSamples = new float[sampleCount];
        if (samplesDirty || !sampleArea.Equals(area) || !sampleColor.Equals(color))
        {
            float start = Mathf.Lerp(area.yMin, area.yMax, litFraction);
            float end = Mathf.Min(area.yMax, start + (litFraction <= 0f ? 0f : softness));
            // Adjacent quads share corners; calculate each light sample only once.
            for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= columns; column++)
                alphaSamples[row * stride + column] = SampleAlpha(new Vector2(
                    Mathf.Lerp(area.xMin, area.xMax, (float)column / columns),
                    Mathf.Lerp(area.yMin, area.yMax, (float)row / rows)), start, end);
            samplesDirty = false;
            sampleArea = area;
            sampleColor = color;
        }
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            float x0 = Mathf.Lerp(area.xMin, area.xMax, (float)column / columns);
            float x1 = Mathf.Lerp(area.xMin, area.xMax, (float)(column + 1) / columns);
            float y0 = Mathf.Lerp(area.yMin, area.yMax, (float)row / rows);
            float y1 = Mathf.Lerp(area.yMin, area.yMax, (float)(row + 1) / rows);
            float a00 = alphaSamples[row * stride + column];
            float a10 = alphaSamples[row * stride + column + 1];
            float a11 = alphaSamples[(row + 1) * stride + column + 1];
            float a01 = alphaSamples[(row + 1) * stride + column];
            if (a00 <= 0.001f && a10 <= 0.001f && a11 <= 0.001f && a01 <= 0.001f) continue;
            int first = mesh.currentVertCount;
            Color c00 = color; c00.a = a00;
            Color c10 = color; c10.a = a10;
            Color c11 = color; c11.a = a11;
            Color c01 = color; c01.a = a01;
            mesh.AddVert(new Vector3(x0, y0), c00, Vector2.zero);
            mesh.AddVert(new Vector3(x1, y0), c10, Vector2.zero);
            mesh.AddVert(new Vector3(x1, y1), c11, Vector2.zero);
            mesh.AddVert(new Vector3(x0, y1), c01, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 3, first);
        }
    }
}
