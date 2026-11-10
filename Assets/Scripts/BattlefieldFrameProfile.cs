using UnityEngine;

[CreateAssetMenu(menuName = "Forest Animal War/Battlefield frame", fileName = "BattlefieldFrame")]
public sealed class BattlefieldFrameProfile : ScriptableObject
{
    [SerializeField, Range(1, 4)] private int defenseLine = 1;
    [SerializeField] private Vector2Int fieldSize = new Vector2Int(8, 10);
    [SerializeField] private Sprite artwork;
    [Tooltip("Transparent opening in sprite pixels, measured from the bottom-left.")]
    [SerializeField] private Rect openingPixels = new Rect(140f, 212f, 842f, 1010f);
    [SerializeField, Range(0f, 0.3f)] private float fieldGap = 0.03f;
    [SerializeField, Range(0f, 0.3f)] private float cameraMargin = 0.08f;
    [Tooltip("Visual-only floor depth below the playable cells, up to the lower animal branch.")]
    [SerializeField, Range(0f, 1.5f)] private float lowerFieldExtension = 1f;
    [SerializeField] private Color tint = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Vector2[] eyePixels = {
        new Vector2(272f, 1298f), new Vector2(562f, 1310f), new Vector2(831f, 1300f)
    };

    public int DefenseLine => defenseLine;
    public Vector2Int FieldSize => fieldSize;
    public Sprite Artwork => artwork;
    public Color Tint => tint;
    public float CameraMargin => cameraMargin;
    public float LowerFieldExtension => lowerFieldExtension;
    public Rect OpeningNormalized => artwork != null ? new Rect(
        openingPixels.x / artwork.rect.width, openingPixels.y / artwork.rect.height,
        openingPixels.width / artwork.rect.width, openingPixels.height / artwork.rect.height)
        : new Rect(0f, 0f, 1f, 1f);

    public void Initialize(int line, Vector2Int size, Sprite sprite)
    {
        defenseLine = line;
        fieldSize = size;
        artwork = sprite;
        if (sprite != null)
        {
            float scale = sprite.rect.width / 1122f;
            float top = Mathf.Round(180f * scale), bottom = Mathf.Round(212f * scale);
            openingPixels = new Rect(140f * scale, bottom, 842f * scale, size.y * 128f);
            eyePixels = new[] {
                new Vector2(272f * scale, sprite.rect.height - 104f * top / 180f),
                new Vector2(562f * scale, sprite.rect.height - 92f * top / 180f),
                new Vector2(831f * scale, sprite.rect.height - 102f * top / 180f)
            };
        }
    }

    public void SetArtwork(Sprite sprite, Rect clearOpeningPixels)
    {
        artwork = sprite;
        openingPixels = clearOpeningPixels;
        fieldGap = 0f;
    }

    public Vector2 EyePixel(int index) => eyePixels != null && index < eyePixels.Length
        ? eyePixels[index] : Vector2.zero;

    public void GetLayout(Vector2 gameplaySize, out Vector2 size, out Vector2 center)
    {
        Rect opening = OpeningNormalized;
        Vector2 inner = gameplaySize + Vector2.one * (2f * fieldGap);
        size = new Vector2(inner.x / Mathf.Max(0.1f, opening.width),
            inner.y / Mathf.Max(0.1f, opening.height));
        center = Vector2.Scale(Vector2.one * 0.5f - opening.center, size);
    }
}
