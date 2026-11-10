using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class DragonBossIdleAnimator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField, Min(1f)] private float framesPerSecond = 6f;

    private float elapsed;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
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
        int cycleLength = idleFrames.Length > 2 ? idleFrames.Length * 2 - 2 : idleFrames.Length;
        int step = Mathf.FloorToInt(elapsed * Mathf.Max(1f, framesPerSecond)) % cycleLength;
        int index = step < idleFrames.Length ? step : cycleLength - step;
        if (idleFrames[index] != null) spriteRenderer.sprite = idleFrames[index];
    }

    private void OnValidate() => framesPerSecond = Mathf.Max(1f, framesPerSecond);
}
