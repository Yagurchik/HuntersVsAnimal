using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArmoredSkeletonArmor : EnemyProjectileDamageModifier
{
    [SerializeField, Range(0f, 1f), Tooltip("Damage retained by a direct hit against the helmet.")]
    private float topDamageMultiplier = 0.5f;
    [SerializeField, Range(0f, 1f), Tooltip("Normalized collider height where the armored upper zone begins.")]
    private float protectedZoneStart = 0.5f;
    [SerializeField, Tooltip("Collider used to split armored and unarmored impact zones.")]
    private Collider2D armorCollider;

    public float TopDamageMultiplier => Mathf.Clamp01(topDamageMultiplier);
    public float ProtectedZoneStart => Mathf.Clamp01(protectedZoneStart);
    public bool LastHitWasBlocked { get; private set; }

    private void Awake()
    {
        if (armorCollider == null)
        {
            armorCollider = GetComponent<Collider2D>();
        }
    }

    private void OnValidate()
    {
        topDamageMultiplier = Mathf.Clamp01(topDamageMultiplier);
        protectedZoneStart = Mathf.Clamp01(protectedZoneStart);
        if (armorCollider == null)
        {
            armorCollider = GetComponent<Collider2D>();
        }
    }

    public override float ModifyDamage(Brick enemy, Animal projectile,
        Vector2 worldImpactPoint, float baseDamage)
    {
        LastHitWasBlocked = IsProtectedPoint(worldImpactPoint);
        return LastHitWasBlocked
            ? Mathf.Max(0f, baseDamage) * TopDamageMultiplier
            : Mathf.Max(0f, baseDamage);
    }

    public bool IsProtectedPoint(Vector2 worldPoint)
    {
        if (armorCollider == null)
        {
            armorCollider = GetComponent<Collider2D>();
        }

        if (armorCollider == null)
        {
            return worldPoint.y >= transform.position.y;
        }

        Bounds bounds = armorCollider.bounds;
        float threshold = Mathf.Lerp(bounds.min.y, bounds.max.y, ProtectedZoneStart);
        return worldPoint.y >= threshold;
    }
}
