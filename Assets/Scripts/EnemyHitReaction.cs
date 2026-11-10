using UnityEngine;

// Runs after Animator evaluation, but before darkness hides enemy renderers.
[DefaultExecutionOrder(800)]
[DisallowMultipleComponent]
public sealed class EnemyHitReaction : MonoBehaviour
{
    [SerializeField, Tooltip("Child containing graphics only; never assign the physics root.")]
    private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Tooltip("Optional damage pose. Leave empty until this enemy has its own artwork.")]
    private Sprite damageSprite;
    [SerializeField, Min(0.01f)] private float duration = 0.18f;
    [SerializeField, Min(0f)] private float shakeDistance = 0.065f;
    [SerializeField, Min(1f)] private float shakeFrequency = 35f;
    private Brick enemy;
    private Vector3 restPosition;
    private Sprite restingSprite;
    private Sprite fallbackSprite;
    private float remaining;

    public bool IsReacting => remaining > 0f;
    public Transform VisualRoot => visualRoot;
    public Sprite DamageSprite => damageSprite;

    public void SetDamageSprite(Sprite value)
    {
        damageSprite = value;
    }

    private void Awake()
    {
        enemy = GetComponent<Brick>();
        if (visualRoot == null) visualRoot = transform.Find("Visual");
        if (spriteRenderer == null && visualRoot != null)
            spriteRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>();
        if (visualRoot != null && visualRoot != transform) restPosition = visualRoot.localPosition;
        fallbackSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (fallbackSprite == null)
            fallbackSprite = GetComponent<EnemyFirstEncounterProfile>()?.Portrait;
    }

    public void Play()
    {
        if (visualRoot == null || visualRoot == transform) return;
        if (remaining <= 0f && spriteRenderer != null)
            restingSprite = spriteRenderer.sprite != null
                ? spriteRenderer.sprite : fallbackSprite;
        remaining = Mathf.Max(0.01f, duration);
    }

    public void Cancel() => Restore();

    private void LateUpdate()
    {
        if (remaining <= 0f || visualRoot == null)
        {
            RestoreMissingSprite();
            return;
        }
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        if (remaining <= 0f) { Restore(); return; }
        float phase = (duration - remaining) * shakeFrequency * Mathf.PI * 2f;
        float strength = shakeDistance * remaining / Mathf.Max(0.01f, duration);
        visualRoot.localPosition = restPosition
            + new Vector3(Mathf.Sin(phase), Mathf.Cos(phase * 1.37f) * 0.35f, 0f) * strength;
        if (damageSprite != null && spriteRenderer != null) spriteRenderer.sprite = damageSprite;
    }

    private void RestoreMissingSprite()
    {
        if (spriteRenderer == null || (enemy != null && enemy.IsDefeated)) return;
        if (spriteRenderer.sprite != null)
        {
            if (spriteRenderer.sprite != damageSprite)
                fallbackSprite = spriteRenderer.sprite;
            return;
        }
        if (fallbackSprite != null) spriteRenderer.sprite = fallbackSprite;
    }

    private void Restore()
    {
        if (visualRoot != null && visualRoot != transform) visualRoot.localPosition = restPosition;
        // Only undo our own pose; a live Animator may already have supplied its next frame.
        if (damageSprite != null && spriteRenderer != null && spriteRenderer.sprite == damageSprite)
            spriteRenderer.sprite = restingSprite != null ? restingSprite : fallbackSprite;
        remaining = 0f;
    }

    private void OnDisable() => Restore();
}
