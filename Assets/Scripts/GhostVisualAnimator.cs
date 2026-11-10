using UnityEngine;

// Animated entirely on the editable Visual child; the physics body stays on its grid cell.
[DisallowMultipleComponent]
[DefaultExecutionOrder(750)]
public sealed class GhostVisualAnimator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0f)] private float floatHeight = 0.055f;
    [SerializeField, Min(0f)] private float floatCyclesPerSecond = 0.7f;
    [SerializeField, Range(0f, 0.3f)] private float swayDegrees = 3f;
    [SerializeField, Range(0f, 0.5f)] private float shimmerStrength = 0.13f;
    [SerializeField, Min(0.01f)] private float phaseFlashSeconds = 0.2f;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private float restAlpha;
    private float phaseRemaining;
    private float phaseOffset;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        restAlpha = spriteRenderer != null ? spriteRenderer.color.a : 1f;
        phaseOffset = Random.Range(0f, 2f * Mathf.PI);
    }

    public void FlashPhase() => phaseRemaining = Mathf.Max(0.01f, phaseFlashSeconds);

    private void LateUpdate()
    {
        float phase = Time.time * floatCyclesPerSecond * Mathf.PI * 2f + phaseOffset;
        transform.localPosition = restPosition + Vector3.up * (Mathf.Sin(phase) * floatHeight);
        transform.localRotation = restRotation *
            Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.6f) * swayDegrees);
        if (spriteRenderer == null)
        {
            return;
        }

        phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.deltaTime);
        float shimmer = 1f - shimmerStrength * (0.5f + 0.5f * Mathf.Sin(phase * 1.35f));
        float fade = phaseRemaining > 0f ? 0.3f : 1f;
        Color color = spriteRenderer.color;
        color.a = restAlpha * shimmer * fade;
        spriteRenderer.color = color;
    }

    private void OnDisable()
    {
        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
        if (spriteRenderer == null)
        {
            return;
        }

        Color color = spriteRenderer.color;
        color.a = restAlpha;
        spriteRenderer.color = color;
    }
}
