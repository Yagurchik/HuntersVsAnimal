using UnityEngine;

[DefaultExecutionOrder(970)]
public sealed class SquirrelVineBinding : MonoBehaviour
{
    [SerializeField] private SpriteRenderer artwork;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 12f;
    private Brick host;
    private Transform anchor;
    private float startedAt;
    private float expiresAt;
    private Vector3 restAnchorScale;

    public static SquirrelVineBinding Apply(Brick enemy, SquirrelVineBinding prefab)
    {
        if (enemy == null || enemy.IsDefeated || prefab == null) return null;
        foreach (SquirrelVineBinding old in enemy.GetComponentsInChildren<SquirrelVineBinding>())
            if (old.host == enemy) { old.expiresAt = 0f; return old; }
        var binding = Instantiate(prefab, enemy.transform);
        binding.host = enemy;
        Transform visual = enemy.GetComponent<EnemyHitReaction>()?.VisualRoot;
        SpriteRenderer body = visual != null ? visual.GetComponentInChildren<SpriteRenderer>()
            : enemy.GetComponentInChildren<SpriteRenderer>();
        binding.anchor = body != null ? body.transform : enemy.transform;
        binding.restAnchorScale = binding.anchor.lossyScale;
        binding.startedAt = Time.time;
        return binding;
    }

    private void LateUpdate()
    {
        if (host == null || host.IsDefeated || artwork == null || frames == null || frames.Length == 0)
        { Destroy(gameObject); return; }
        EnemyTurnStun stun = host.GetComponent<EnemyTurnStun>();
        if (stun == null || !stun.IsStunned)
        {
            if (expiresAt == 0f) expiresAt = Time.time + 0.15f;
            artwork.color = new Color(1f, 1f, 1f, Mathf.Clamp01((expiresAt - Time.time) / 0.15f));
            if (Time.time >= expiresAt) { Destroy(gameObject); return; }
        }
        else { expiresAt = 0f; artwork.color = Color.white; }
        int frame = Mathf.FloorToInt((Time.time - startedAt) * framesPerSecond);
        frame = frame < frames.Length ? frame : 4 + (frame - 4) % Mathf.Max(1, frames.Length - 4);
        artwork.sprite = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
        Transform follow = anchor != null ? anchor : host.transform;
        transform.position = follow.position + Vector3.back * 0.03f;
        transform.rotation = follow.rotation;
        Vector3 size = follow.lossyScale;
        Vector3 parentScale = artwork.transform.parent.lossyScale;
        float width = host.Footprint.x * size.x / Mathf.Max(0.001f, restAnchorScale.x)
            / Mathf.Max(0.01f, artwork.sprite.bounds.size.x);
        float height = host.Footprint.y * size.y / Mathf.Max(0.001f, restAnchorScale.y)
            / Mathf.Max(0.01f, artwork.sprite.bounds.size.y);
        artwork.transform.localScale = new Vector3(width / Mathf.Max(0.001f, parentScale.x),
            height / Mathf.Max(0.001f, parentScale.y), 1f);
        artwork.enabled = host.levelGenerator == null || host.levelGenerator.Darkness == null
            || host.levelGenerator.Darkness.IsVisible(transform.position);
    }
}
