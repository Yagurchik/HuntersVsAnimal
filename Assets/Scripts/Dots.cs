using System.Collections.Generic;
using UnityEngine;

public class Dots : MonoBehaviour
{
    public struct TrajectoryPath
    {
        public Vector3 Start;
        public Vector2 HitPoint;
        public Vector2 ReflectionEnd;
        public bool IncludeReflection;

        public TrajectoryPath(Vector3 start, Vector2 hitPoint, Vector2 reflectionEnd,
            bool includeReflection)
        {
            Start = start;
            HitPoint = hitPoint;
            ReflectionEnd = reflectionEnd;
            IncludeReflection = includeReflection;
        }
    }

    public Sprite dot;
    [Range(0.01f, 1f)] public float size = 0.12f;
    [Range(0.1f, 1f)] public float delta = 0.55f;
    [Range(0f, 1f)] public float alpha = 0.65f;

    [Header("Dashed trajectory")]
    [SerializeField, Range(0.5f, 3f)] private float dashLengthMultiplier = 2f;
    [SerializeField, Range(0.05f, 0.5f)] private float dashThicknessMultiplier = 0.2f;
    [SerializeField] private Color trajectoryColor = new Color(0.055f, 0.04f, 0.025f, 1f);

    [Header("Impact marker")]
    [SerializeField] private Color impactColor = new Color(1f, 0.34f, 0.04f, 1f);
    [SerializeField, Min(0.05f)] private float impactRadius = 0.23f;
    [SerializeField, Min(0.005f)] private float impactLineWidth = 0.035f;
    [SerializeField] private float impactRotationSpeed = 170f;

    [SerializeField, Min(1)] private int prewarmCount = 32;
    [SerializeField, Min(8)] private int maxDots = 96;
    [SerializeField, Min(0.1f)] private float fadeSpeed = 8f;
    [SerializeField] private int sortingOrder = 100;

    public static Dots instance;

    private readonly List<SpriteRenderer> dotRenderers = new List<SpriteRenderer>();
    private int usedDotCount;
    private float visibility;
    private bool drawRequested;
    private Object currentOwner;
    private bool impactRequested;
    private Vector3 impactPosition;
    private LineRenderer impactMarker;
    private Material impactMaterial;

    public int VisibleDotCount => visibility > 0.001f ? usedDotCount : 0;
    public bool ImpactMarkerVisible => impactMarker != null && impactMarker.enabled;
    public int ImpactMarkerSideCount => impactMarker != null ? impactMarker.positionCount : 0;
    public Vector3 ImpactMarkerPosition => impactPosition;

    public void Awake()
    {
        if (instance == null || instance == this)
        {
            instance = this;
        }
    }

    private void Start()
    {
        EnsurePool(Mathf.Min(prewarmCount, maxDots));
        EnsureImpactMarker();
        ApplyVisibility();
    }

    private void LateUpdate()
    {
        float targetVisibility = drawRequested ? 1f : 0f;
        visibility = Mathf.MoveTowards(
            visibility,
            targetVisibility,
            fadeSpeed * Time.unscaledDeltaTime);

        ApplyVisibility();
        drawRequested = false;
        if (visibility <= 0.001f)
        {
            usedDotCount = 0;
            impactRequested = false;
            currentOwner = null;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
        if (impactMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(impactMaterial);
            }
            else
            {
                DestroyImmediate(impactMaterial);
            }
        }
    }

    public void DrawTrajectory(
        Vector2 start,
        Vector2 hitPoint,
        Vector2 reflectionEnd,
        bool includeReflection)
    {
        DrawTrajectory(this, start, hitPoint, reflectionEnd, includeReflection, includeReflection);
    }

    public void DrawTrajectory(
        Object owner,
        Vector3 start,
        Vector2 hitPoint,
        Vector2 reflectionEnd,
        bool includeReflection)
    {
        DrawTrajectory(owner, start, hitPoint, reflectionEnd, includeReflection, includeReflection);
    }

    public void DrawTrajectory(
        Object owner,
        Vector3 start,
        Vector2 hitPoint,
        Vector2 reflectionEnd,
        bool includeReflection,
        bool showImpact)
    {
        DrawTrajectories(owner, new[]
        {
            new TrajectoryPath(start, hitPoint, reflectionEnd, includeReflection)
        }, hitPoint, showImpact);
    }

    public void DrawTrajectories(Object owner, IReadOnlyList<TrajectoryPath> paths,
        Vector2 impactPoint, bool showImpact)
    {
        if (paths == null || paths.Count == 0)
        {
            Hide(owner);
            return;
        }

        currentOwner = owner;
        drawRequested = true;
        impactRequested = showImpact;
        impactPosition = new Vector3(impactPoint.x, impactPoint.y, paths[0].Start.z);

        float totalLength = 0f;
        for (int i = 0; i < paths.Count; i++)
        {
            TrajectoryPath path = paths[i];
            totalLength += Vector2.Distance(path.Start, path.HitPoint);
            if (path.IncludeReflection)
            {
                totalLength += Vector2.Distance(path.HitPoint, path.ReflectionEnd);
            }
        }
        int distanceSlots = Mathf.Max(1, maxDots - paths.Count);
        float spacing = Mathf.Max(0.05f, delta, totalLength / distanceSlots);

        usedDotCount = 0;
        EnsurePool(maxDots);
        for (int i = 0; i < paths.Count && usedDotCount < maxDots; i++)
        {
            WritePathDots(paths[i], spacing);
        }
    }

    private void WritePathDots(TrajectoryPath path, float spacing)
    {
        Vector2 origin = path.Start;
        float firstLength = Vector2.Distance(origin, path.HitPoint);
        float secondLength = path.IncludeReflection
            ? Vector2.Distance(path.HitPoint, path.ReflectionEnd) : 0f;
        float pathLength = firstLength + secondLength;
        int pathDotCount = Mathf.FloorToInt(pathLength / spacing) + 1;
        Vector2 firstDirection = firstLength > 0.0001f
            ? (path.HitPoint - origin) / firstLength : Vector2.zero;
        Vector2 secondDirection = secondLength > 0.0001f
            ? (path.ReflectionEnd - path.HitPoint) / secondLength : Vector2.zero;

        for (int i = 0; i < pathDotCount && usedDotCount < maxDots; i++)
        {
            float distance = Mathf.Min(i * spacing, pathLength);
            bool onFirstSegment = distance <= firstLength || secondLength <= 0f;
            Vector2 position = onFirstSegment
                ? origin + firstDirection * Mathf.Min(distance, firstLength)
                : path.HitPoint + secondDirection * Mathf.Min(distance - firstLength, secondLength);
            Vector2 direction = onFirstSegment ? firstDirection : secondDirection;
            SpriteRenderer renderer = dotRenderers[usedDotCount++];
            renderer.transform.position = new Vector3(position.x, position.y, path.Start.z);
            renderer.transform.rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.AngleAxis(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                    Vector3.forward) : Quaternion.identity;
            renderer.transform.localScale = new Vector3(size * dashLengthMultiplier,
                size * dashThicknessMultiplier, 1f);
        }
    }

    public void DrawDottedLine(Vector2 start, Vector2 end)
    {
        DrawTrajectory(start, end, end, false);
    }

    public void Hide()
    {
        drawRequested = false;
    }

    public void Hide(Object owner, bool immediate = false)
    {
        // An inactive room must not erase another room's preview.
        if (currentOwner != owner)
        {
            return;
        }

        drawRequested = false;
        if (immediate)
        {
            visibility = 0f;
            usedDotCount = 0;
            impactRequested = false;
            ApplyVisibility();
            currentOwner = null;
        }
    }

    private void EnsurePool(int requiredCount)
    {
        while (dotRenderers.Count < requiredCount)
        {
            GameObject dotObject = new GameObject("Aim Dot " + dotRenderers.Count);
            dotObject.transform.SetParent(transform, false);
            dotObject.transform.localScale = new Vector3(size * dashLengthMultiplier,
                size * dashThicknessMultiplier, 1f);

            SpriteRenderer renderer = dotObject.AddComponent<SpriteRenderer>();
            renderer.sprite = dot;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            dotRenderers.Add(renderer);
        }
    }

    private void ApplyVisibility()
    {
        for (int i = 0; i < dotRenderers.Count; i++)
        {
            SpriteRenderer renderer = dotRenderers[i];
            bool shouldShow = i < usedDotCount && visibility > 0.001f;
            renderer.enabled = shouldShow;
            if (!shouldShow)
            {
                continue;
            }

            float pathProgress = usedDotCount > 1
                ? (float)i / (usedDotCount - 1)
                : 0f;
            float pathAlpha = Mathf.Lerp(1f, 0.4f, pathProgress);
            Color color = trajectoryColor;
            color.a *= alpha * visibility * pathAlpha;
            renderer.color = color;
        }

        EnsureImpactMarker();
        bool showImpact = impactRequested && visibility > 0.001f;
        impactMarker.enabled = showImpact;
        if (!showImpact)
        {
            return;
        }

        impactMarker.transform.position = impactPosition;
        impactMarker.transform.rotation = Quaternion.AngleAxis(
            impactRotationSpeed * Time.unscaledTime, Vector3.forward);
        float revealScale = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(visibility));
        impactMarker.transform.localScale = Vector3.one * revealScale;
        Color markerColor = impactColor;
        markerColor.a *= visibility;
        impactMarker.startColor = impactMarker.endColor = markerColor;
    }

    private void EnsureImpactMarker()
    {
        if (impactMarker != null)
        {
            return;
        }

        GameObject marker = new GameObject("Aim Impact Hexagon");
        marker.transform.SetParent(transform, false);
        impactMarker = marker.AddComponent<LineRenderer>();
        impactMarker.useWorldSpace = false;
        impactMarker.loop = true;
        impactMarker.positionCount = 6;
        impactMarker.startWidth = impactMarker.endWidth = impactLineWidth;
        impactMarker.numCornerVertices = 1;
        impactMarker.alignment = LineAlignment.View;
        impactMarker.sortingOrder = sortingOrder + 2;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            impactMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            impactMarker.sharedMaterial = impactMaterial;
        }
        for (int i = 0; i < 6; i++)
        {
            float angle = (30f + i * 60f) * Mathf.Deg2Rad;
            impactMarker.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f)
                * impactRadius);
        }
        impactMarker.enabled = false;
    }

    private void OnValidate()
    {
        size = Mathf.Clamp(size, 0.01f, 1f);
        delta = Mathf.Clamp(delta, 0.1f, 1f);
        alpha = Mathf.Clamp01(alpha);
        dashLengthMultiplier = Mathf.Clamp(dashLengthMultiplier, 0.5f, 3f);
        dashThicknessMultiplier = Mathf.Clamp(dashThicknessMultiplier, 0.05f, 0.5f);
        impactRadius = Mathf.Max(0.05f, impactRadius);
        impactLineWidth = Mathf.Max(0.005f, impactLineWidth);
    }
}
