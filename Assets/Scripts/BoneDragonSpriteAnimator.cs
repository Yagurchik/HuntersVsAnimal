using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class BoneDragonSpriteAnimator : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite[]> IdleCache = new Dictionary<string, Sprite[]>();

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private EnemyHitReaction hitReaction;
    [SerializeField] private string idleFramesResourcePath = "Enemies/BoneDragon/IdleFrames";
    [SerializeField] private string damageResourcePath = "Enemies/BoneDragon/BoneDragonDamage";
    [SerializeField, Min(2)] private int uniqueIdleFrameCount = 8;
    [SerializeField] private bool pingPong = true;
    [SerializeField, Min(1f)] private float framesPerSecond = 12f;
    [SerializeField, Min(0.1f)] private float frameWorldSize = 4.3359375f;

    private Sprite[] idleFrames;
    private float elapsed;

    public int IdleFrameCount => idleFrames != null ? idleFrames.Length : 0;
    public float FramesPerSecond => Mathf.Max(1f, framesPerSecond);

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (hitReaction == null) hitReaction = GetComponent<EnemyHitReaction>();

        idleFrames = CreateIdleFrames();

        if (hitReaction != null && hitReaction.DamageSprite == null)
            hitReaction.SetDamageSprite(Resources.Load<Sprite>(damageResourcePath));
    }

    private void OnEnable()
    {
        elapsed = 0f;
        if (spriteRenderer != null && idleFrames != null && idleFrames.Length > 0)
            spriteRenderer.sprite = idleFrames[0];
    }

    private void Update()
    {
        if (spriteRenderer == null || idleFrames == null || idleFrames.Length == 0) return;
        elapsed += Time.deltaTime;
        int frame = Mathf.FloorToInt(elapsed * FramesPerSecond) % idleFrames.Length;
        spriteRenderer.sprite = idleFrames[frame];
    }

    private Sprite[] CreateIdleFrames()
    {
        int safeFrameCount = Mathf.Max(2, uniqueIdleFrameCount);
        string cacheKey = idleFramesResourcePath + "|" + safeFrameCount + "|"
            + (pingPong ? "1" : "0") + "|" + Mathf.RoundToInt(frameWorldSize * 10000f);
        if (IdleCache.TryGetValue(cacheKey, out Sprite[] cached)) return cached;

        Sprite[] uniqueFrames = new Sprite[safeFrameCount];
        for (int frameIndex = 0; frameIndex < safeFrameCount; frameIndex++)
        {
            string frameName = "BoneDragonIdle_" + frameIndex.ToString("00");
            Texture2D texture = Resources.Load<Texture2D>(idleFramesResourcePath + "/" + frameName);
            if (texture == null)
            {
                Debug.LogError("Missing bone dragon idle frame: " + idleFramesResourcePath + "/" + frameName,
                    this);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            float pixelsPerUnit = Mathf.Max(texture.width, texture.height)
                / Mathf.Max(0.1f, frameWorldSize);
            uniqueFrames[frameIndex] = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                pixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            uniqueFrames[frameIndex].name = frameName;
        }

        if (!pingPong || safeFrameCount <= 2)
        {
            IdleCache.Add(cacheKey, uniqueFrames);
            return uniqueFrames;
        }

        Sprite[] frames = new Sprite[safeFrameCount * 2 - 2];
        for (int i = 0; i < safeFrameCount; i++)
            frames[i] = uniqueFrames[i];
        for (int i = safeFrameCount - 2; i > 0; i--)
            frames[safeFrameCount + safeFrameCount - 2 - i] = uniqueFrames[i];

        IdleCache.Add(cacheKey, frames);
        return frames;
    }

    private void OnValidate()
    {
        uniqueIdleFrameCount = Mathf.Max(2, uniqueIdleFrameCount);
        framesPerSecond = Mathf.Max(1f, framesPerSecond);
        frameWorldSize = Mathf.Max(0.1f, frameWorldSize);
    }
}
