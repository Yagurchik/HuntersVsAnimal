using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoarChargeTrail : MonoBehaviour
{
    [SerializeField] private Sprite dustSprite;
    [SerializeField, Min(0.05f)] private float puffSpacing = 0.22f;
    [SerializeField, Min(0.01f)] private float trailWidth = 0.13f;
    [SerializeField, Min(0.05f)] private float puffWorldSize = 0.32f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 1f;
    [SerializeField] private Color dustColor = new Color(0.78f, 0.61f, 0.39f, 0.86f);

    private Vector3 segmentStart;
    private bool hasActiveSegment;

    public static BoarChargeTrail Ensure(GameObject projectile)
    {
        BoarChargeTrail value = projectile.GetComponent<BoarChargeTrail>();
        return value != null ? value : projectile.AddComponent<BoarChargeTrail>();
    }

    private void Awake()
    {
        TrailRenderer obsoleteTrail = GetComponent<TrailRenderer>();
        if (obsoleteTrail != null) obsoleteTrail.enabled = false;
        LineRenderer obsoleteLine = GetComponent<LineRenderer>();
        if (obsoleteLine != null) obsoleteLine.enabled = false;
        BeginSegment(transform.position);
    }

    public void BeginSegment(Vector3 start)
    {
        segmentStart = start;
        hasActiveSegment = true;
    }

    public void CompleteSegment(Vector3 end)
    {
        if (!hasActiveSegment) return;
        hasActiveSegment = false;
        if (dustSprite == null || Vector3.Distance(segmentStart, end) < 0.01f) return;
        BoarDustTrailSegment.Create(dustSprite, segmentStart, end,
            Mathf.Max(0.05f, puffSpacing), Mathf.Max(0.01f, trailWidth),
            Mathf.Max(0.05f, puffWorldSize), Mathf.Max(0.05f, fadeDuration), dustColor);
    }

    private void OnDisable()
    {
        CompleteSegment(transform.position);
    }

    private void OnValidate()
    {
        puffSpacing = Mathf.Max(0.05f, puffSpacing);
        trailWidth = Mathf.Max(0.01f, trailWidth);
        puffWorldSize = Mathf.Max(0.05f, puffWorldSize);
        fadeDuration = Mathf.Max(0.05f, fadeDuration);
    }
}

public sealed class BoarDustTrailSegment : MonoBehaviour
{
    private SpriteRenderer[] puffs;
    private Color[] colors;
    private Vector3[] initialScales;
    private Vector3[] driftDirections;
    private float duration;
    private float elapsed;

    public static void Create(Sprite sprite, Vector3 start, Vector3 end, float spacing,
        float width, float worldSize, float lifetime, Color tint)
    {
        Vector2 travel = end - start;
        float distance = travel.magnitude;
        if (sprite == null || distance < 0.01f) return;

        GameObject root = new GameObject("Boar Dust Trail Segment");
        BoarDustTrailSegment segment = root.AddComponent<BoarDustTrailSegment>();
        int count = Mathf.Clamp(Mathf.CeilToInt(distance / spacing) + 1, 2, 96);
        segment.puffs = new SpriteRenderer[count];
        segment.colors = new Color[count];
        segment.initialScales = new Vector3[count];
        segment.driftDirections = new Vector3[count];
        segment.duration = Mathf.Max(0.05f, lifetime);

        Vector2 direction = travel / distance;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        float naturalSize = Mathf.Max(0.001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);
            GameObject puffObject = new GameObject("Dust Puff");
            puffObject.transform.SetParent(root.transform, false);
            Vector2 offset = normal * Random.Range(-width, width)
                + Random.insideUnitCircle * (width * 0.22f);
            puffObject.transform.position = Vector3.Lerp(start, end, t)
                + new Vector3(offset.x, offset.y, 0f);
            puffObject.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float size = worldSize * Random.Range(0.65f, 1.15f) / naturalSize;
            puffObject.transform.localScale = Vector3.one * size;

            SpriteRenderer renderer = puffObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            Color puffColor = tint;
            puffColor.a *= Random.Range(0.58f, 0.92f);
            renderer.color = puffColor;
            renderer.sortingOrder = RoomDarkness.WorldOverlayOrder + 8;
            segment.puffs[i] = renderer;
            segment.colors[i] = puffColor;
            segment.initialScales[i] = puffObject.transform.localScale;
            segment.driftDirections[i] = new Vector3(normal.x, normal.y, 0f)
                * Random.Range(-0.055f, 0.055f);
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float alpha = 1f - t;
        for (int i = 0; i < puffs.Length; i++)
        {
            SpriteRenderer puff = puffs[i];
            if (puff == null) continue;
            Color color = colors[i];
            color.a *= alpha;
            puff.color = color;
            puff.transform.localScale = initialScales[i] * Mathf.Lerp(0.82f, 1.12f, t);
            puff.transform.position += driftDirections[i] * Time.deltaTime;
        }
        if (elapsed >= duration) Destroy(gameObject);
    }
}
