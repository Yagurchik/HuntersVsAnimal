using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class BattlefieldForestStyle : MonoBehaviour
{
    // The transparent frame opening leaves play visible; the border stays above room fog.
    public const int FrameSortingOrder = RoomDarkness.WorldOverlayOrder + 1;
    public const int DefenseStructureSortingOrder = -18;
    [Header("Artwork")]
    [SerializeField] private Sprite shadowFrame;
    [SerializeField] private Sprite breachVine;
    [SerializeField] private Material eyeMaterial;
    [SerializeField] private Material opaqueFrameMaterial;

    [Header("Restrained palette")]
    [SerializeField] private Color terrainTint = new Color(0.76f, 0.82f, 0.75f, 1f);
    [SerializeField] private Color frameTint = Color.white;
    [SerializeField] private Color boardEdgeTint = Color.white;
    [SerializeField] private Color vineTint = new Color(1f, 0.82f, 0.34f, 0.96f);
    [SerializeField] private Color eyeTint = new Color(1f, 0.76f, 0.18f, 0.96f);

    [Header("Layout")]
    [SerializeField] private BattlefieldFrameProfile frameProfile;
    [SerializeField, Range(0.35f, 1.2f)] private float vineSpriteHeight = 0.62f;
    [SerializeField] private SpriteRenderer frameRenderer;
    [SerializeField] private SpriteRenderer lowerFieldRenderer;
    [SerializeField] private SpriteRenderer[] boardEdgeRenderers = new SpriteRenderer[3];
    [SerializeField] private SpriteRenderer vineGlowRenderer;
    [SerializeField] private SpriteRenderer vineRenderer;
    [SerializeField] private ForestEyeBlinker[] blinkingEyes = new ForestEyeBlinker[3];

    public SpriteRenderer FrameRenderer => frameRenderer;
    public SpriteRenderer LowerFieldRenderer => lowerFieldRenderer;
    public IReadOnlyList<SpriteRenderer> BoardEdgeRenderers => boardEdgeRenderers;
    public BattlefieldFrameProfile FrameProfile => frameProfile;
    public SpriteRenderer VineRenderer => vineRenderer;
    public IReadOnlyList<ForestEyeBlinker> BlinkingEyes => blinkingEyes;

    public void Configure(Sprite frame, Sprite vine, Material eyes, BattlefieldFrameProfile profile)
    {
        frameProfile = profile;
        shadowFrame = profile != null ? profile.Artwork : frame;
        breachVine = vine;
        eyeMaterial = eyes;
    }

    public void SetOpaqueFrameMaterial(Material material) => opaqueFrameMaterial = material;

    public void Refresh(Stage room)
    {
        if (room == null) room = GetComponent<Stage>();
        if (room == null) return;

        // Older scenes can clone a lower-line room when building outer lines.
        if (frameProfile == null || frameProfile.DefenseLine != room.DefenseLine)
        {
            Vector2Int dimensions = room.BattlefieldSize;
            frameProfile = Resources.Load<BattlefieldFrameProfile>(
                "Battlefield/TwilightThicket/Frames/Frame_Line" + room.DefenseLine
                + "_" + dimensions.x + "x" + dimensions.y);
        }
        if (frameProfile != null) shadowFrame = frameProfile.Artwork;

        foreach (Tilemap map in room.GetComponentsInChildren<Tilemap>(true))
            map.color = terrainTint;

        Vector2 size = room.BattlefieldSize;
        float visualDepth = ResolveVisualDepth(room);
        frameRenderer = EnsureSpriteRenderer("Forest Shadow Frame", frameRenderer,
            FrameSortingOrder);
        ConfigureFrame(size, visualDepth);
        frameRenderer.sharedMaterial = opaqueFrameMaterial;
        ConfigureBoardEdges(size, visualDepth);

        lowerFieldRenderer = EnsureSpriteRenderer("Lower Field Continuation", lowerFieldRenderer, -19);
        Tilemap initialTiles = room.GetComponentInChildren<Tilemap>(true);
        RefreshLowerField(room, initialTiles);

        vineGlowRenderer = EnsureSpriteRenderer("Breach Vine Glow", vineGlowRenderer,
            FrameSortingOrder + 1);
        ConfigureVine(vineGlowRenderer, room, size.x + 0.16f,
            vineSpriteHeight * 1.18f, visualDepth,
            new Color(vineTint.r, vineTint.g, vineTint.b, 0.18f));
        vineRenderer = EnsureSpriteRenderer("Breach Vine", vineRenderer,
            FrameSortingOrder + 2);
        ConfigureVine(vineRenderer, room, size.x + 0.08f, vineSpriteHeight,
            visualDepth, vineTint);

        EnsureEyePairs(room, size, visualDepth);
        RefreshFrameSorting();
        room.GetComponent<DrownedBogVisuals>()?.Refresh();
    }

    public void RefreshFrameSorting()
    {
        if (frameRenderer != null) frameRenderer.sortingOrder = FrameSortingOrder;
        if (boardEdgeRenderers != null)
            foreach (SpriteRenderer edge in boardEdgeRenderers)
                if (edge != null) edge.sortingOrder = FrameSortingOrder + 1;
        if (vineGlowRenderer != null) vineGlowRenderer.sortingOrder = FrameSortingOrder + 1;
        if (vineRenderer != null) vineRenderer.sortingOrder = FrameSortingOrder + 2;
        if (blinkingEyes == null) return;
        foreach (ForestEyeBlinker eyes in blinkingEyes)
        {
            if (eyes == null) continue;
            if (eyes.LeftEye != null) eyes.LeftEye.sortingOrder = FrameSortingOrder + 3;
            if (eyes.RightEye != null) eyes.RightEye.sortingOrder = FrameSortingOrder + 3;
        }
    }

    // Fit the measured transparent opening, not the complete image, to the grid.
    // Each defense line has its own editable profile; animals do not get 9-slice stretched.
    private void GetFrameLayout(Vector2 fieldSize, out Vector2 size, out Vector2 center)
    {
        if (frameProfile != null) frameProfile.GetLayout(fieldSize, out size, out center);
        else { size = fieldSize; center = Vector2.zero; }
    }

    public Bounds GetFramingBounds(Bounds gameplayBounds)
    {
        if (shadowFrame == null) return gameplayBounds;
        GetFrameLayout(gameplayBounds.size, out Vector2 size, out Vector2 center);
        size += Vector2.one * ((frameProfile != null ? frameProfile.CameraMargin : 0f) * 2f);
        return new Bounds(gameplayBounds.center + (Vector3)center, new Vector3(size.x, size.y, 0f));
    }

    private void ConfigureFrame(Vector2 fieldSize, float depth)
    {
        GetFrameLayout(fieldSize, out Vector2 size, out Vector2 center);
        Color opaqueTint = frameProfile != null ? frameProfile.Tint : frameTint;
        opaqueTint.a = 1f;
        ConfigureSprite(frameRenderer, shadowFrame, size, center, depth, opaqueTint);
    }

    private void ConfigureBoardEdges(Vector2 fieldSize, float depth)
    {
        Transform obsoleteBottom = transform.Find("Orange Forest Edge Bottom");
        if (obsoleteBottom != null)
        {
            if (Application.isPlaying) Destroy(obsoleteBottom.gameObject);
            else DestroyImmediate(obsoleteBottom.gameObject);
        }
        if (boardEdgeRenderers == null || boardEdgeRenderers.Length != 3)
        {
            SpriteRenderer[] previous = boardEdgeRenderers;
            boardEdgeRenderers = new SpriteRenderer[3];
            if (previous != null && previous.Length >= 4)
            {
                boardEdgeRenderers[0] = previous[0];
                boardEdgeRenderers[1] = previous[1];
                boardEdgeRenderers[2] = previous[3];
            }
        }

        const float visualThickness = 0.82f;
        const float endpointOverlap = 0.14f;
        string[] names = { "Orange Forest Edge Top", "Orange Forest Edge Right",
            "Orange Forest Edge Left" };
        for (int i = 0; i < boardEdgeRenderers.Length; i++)
        {
            SpriteRenderer edge = EnsureSpriteRenderer(names[i], boardEdgeRenderers[i],
                FrameSortingOrder + 1);
            boardEdgeRenderers[i] = edge;
            bool vertical = i != 0;
            Vector2 lengthAndThickness = new Vector2(
                (vertical ? fieldSize.y : fieldSize.x) + endpointOverlap, visualThickness);
            Vector2 center = i == 0 ? new Vector2(0f, fieldSize.y * 0.5f)
                : i == 1 ? new Vector2(fieldSize.x * 0.5f, 0f)
                : new Vector2(-fieldSize.x * 0.5f, 0f);
            Color opaqueEdge = boardEdgeTint;
            opaqueEdge.a = 1f;
            ConfigureSprite(edge, breachVine, lengthAndThickness, center, depth, opaqueEdge);
            edge.sharedMaterial = opaqueFrameMaterial;
            edge.transform.localRotation = Quaternion.Euler(0f, 0f, i == 1 ? -90f : i == 2 ? 90f : 0f);
        }
    }

    public void RefreshLowerField(Stage room, Tilemap sourceTiles)
    {
        if (lowerFieldRenderer == null || room == null || sourceTiles == null) return;
        BoundsInt cells = sourceTiles.cellBounds;
        Sprite floorSprite = sourceTiles.GetSprite(new Vector3Int(cells.xMin, cells.yMin, cells.zMin));
        if (floorSprite == null)
        {
            lowerFieldRenderer.enabled = false;
            return;
        }
        float extension = frameProfile != null ? frameProfile.LowerFieldExtension : 1.25f;
        Vector2 size = room.BattlefieldSize;
        lowerFieldRenderer.enabled = true;
        lowerFieldRenderer.sprite = floorSprite;
        lowerFieldRenderer.drawMode = SpriteDrawMode.Tiled;
        lowerFieldRenderer.size = new Vector2(size.x, extension + 0.04f);
        lowerFieldRenderer.transform.localPosition = new Vector3(0f,
            -size.y * 0.5f - extension * 0.5f + 0.02f, ResolveVisualDepth(room));
        lowerFieldRenderer.transform.localScale = Vector3.one;
        lowerFieldRenderer.transform.localRotation = Quaternion.identity;
        lowerFieldRenderer.color = room.LowerFieldTint * sourceTiles.color;
    }

    private SpriteRenderer EnsureSpriteRenderer(string objectName, SpriteRenderer current,
        int sortingOrder)
    {
        if (current == null)
        {
            Transform existing = transform.Find(objectName);
            if (existing == null)
            {
                GameObject visual = new GameObject(objectName);
                existing = visual.transform;
                existing.SetParent(transform, false);
            }
            current = existing.GetComponent<SpriteRenderer>();
            if (current == null) current = existing.gameObject.AddComponent<SpriteRenderer>();
        }
        current.sortingOrder = sortingOrder;
        current.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        current.receiveShadows = false;
        return current;
    }

    private void ConfigureSprite(SpriteRenderer renderer, Sprite sprite, Vector2 worldSize,
        Vector2 localPosition, float localDepth, Color tint)
    {
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.color = tint;
        renderer.transform.localPosition = new Vector3(localPosition.x, localPosition.y, localDepth);
        renderer.transform.localRotation = Quaternion.identity;
        if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
        {
            renderer.transform.localScale = Vector3.one;
            renderer.enabled = false;
            return;
        }
        renderer.enabled = true;
        renderer.transform.localScale = new Vector3(
            worldSize.x / sprite.bounds.size.x,
            worldSize.y / sprite.bounds.size.y,
            1f);
    }

    private void ConfigureVine(SpriteRenderer renderer, Stage room, float width, float height,
        float localDepth, Color tint)
    {
        ConfigureSprite(renderer, breachVine, new Vector2(width, height),
            new Vector2(0f, room.DefenseBoundaryY - room.transform.position.y), localDepth, tint);
    }

    private void EnsureEyePairs(Stage room, Vector2 size, float localDepth)
    {
        if (blinkingEyes == null || blinkingEyes.Length != 3)
            blinkingEyes = new ForestEyeBlinker[3];
        // The bottom spectators face away from us. Eyes belong to the three
        // forward-facing animals on the top rail, outside the enemy grid.
        for (int i = 0; i < blinkingEyes.Length; i++)
        {
            ForestEyeBlinker eyes = blinkingEyes[i];
            if (eyes == null)
            {
                Transform existing = transform.Find("Forest Eyes " + (i + 1));
                if (existing == null)
                {
                    GameObject eyeObject = new GameObject("Forest Eyes " + (i + 1));
                    existing = eyeObject.transform;
                    existing.SetParent(transform, false);
                }
                eyes = existing.GetComponent<ForestEyeBlinker>();
                if (eyes == null) eyes = existing.gameObject.AddComponent<ForestEyeBlinker>();
                blinkingEyes[i] = eyes;
            }
            Vector2 position = FramePixelToLocal(frameProfile != null ? frameProfile.EyePixel(i) : Vector2.zero, size);
            eyes.transform.localPosition = new Vector3(position.x, position.y, localDepth);
            eyes.transform.localRotation = Quaternion.identity;
            eyes.Configure(eyeMaterial, eyeTint, FrameSortingOrder + 3);
        }
    }

    private Vector2 FramePixelToLocal(Vector2 pixel, Vector2 fieldSize)
    {
        if (shadowFrame == null) return Vector2.zero;
        GetFrameLayout(fieldSize, out Vector2 size, out Vector2 center);
        return center - size * 0.5f + new Vector2(
            pixel.x / shadowFrame.rect.width * size.x, pixel.y / shadowFrame.rect.height * size.y);
    }

    private static float ResolveVisualDepth(Stage room)
    {
        if (room.PlayerObject != null)
            return room.transform.InverseTransformPoint(room.PlayerObject.transform.position).z;
        LevelGenerator generator = room.GetComponentInChildren<LevelGenerator>(true);
        return generator != null
            ? room.transform.InverseTransformPoint(generator.transform.position).z
            : 0f;
    }
}
