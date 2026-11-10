using UnityEngine;
using System.Collections;

/// <summary>
/// Adds a short area pulse to every enemy hit without interrupting the
/// projectile's ordinary ricochet flight.
/// </summary>
public class HedgehogExplosion : MonoBehaviour
{
    private const float DefaultDamageMultiplier = 0.5f;

    [Header("Impact pulse")]
    [SerializeField, Min(0.1f)] private float explosionRadius = 1.2f;
    [SerializeField, Min(0.05f)] private float explosionDuration = 0.18f;
    [SerializeField, Range(0f, 1f)] private float damageMultiplier = DefaultDamageMultiplier;
    [SerializeField, Min(0f)] private float quillDamageGrowthPerHit;
    [SerializeField] private bool doubleExplosion;
    [SerializeField, Min(0f)] private float secondExplosionDelay = 0.09f;

    [Header("Flight visual")]
    [SerializeField, Min(0f)] private float spinDegreesPerSecond = 540f;

    private SpriteRenderer projectileRenderer;
    private SpriteRenderer areaRenderer;
    private float directDamage;
    private bool configured;

    public float ExplosionRadius => explosionRadius;
    public float ExplosionDuration => explosionDuration;
    public float AreaDamage => directDamage * damageMultiplier;
    public float QuillDamageGrowthPerHit => Mathf.Max(0f, quillDamageGrowthPerHit);
    public bool HasDoubleExplosion => doubleExplosion;
    public float SecondExplosionDelay => Mathf.Max(0f, secondExplosionDelay);
    public float SpinDegreesPerSecond => GetComponent<ProjectileFlightVisual>() != null
        ? GetComponent<ProjectileFlightVisual>().SpinDegreesPerSecond : spinDegreesPerSecond;

    private void Awake()
    {
        projectileRenderer = ProjectileFlightVisual.Ensure(gameObject, spinDegreesPerSecond).Renderer;
        Transform areaTransform = transform.Find("Circle");
        areaRenderer = areaTransform != null ? areaTransform.GetComponent<SpriteRenderer>() : null;

        // Older prefab variants still contain the firefly's circle. Hedgehogs
        // use detached quills instead, never the inherited aura.
        if (areaRenderer != null)
        {
            areaRenderer.enabled = false;
        }
    }

    private void Start()
    {
        if (configured) return;

        Animal animal = GetComponent<Animal>();
        directDamage = animal != null ? Mathf.Max(0f, animal.dmg) : 0f;
        configured = true;
    }

    public void Configure(float projectileDamage, float radius, float duration)
    {
        directDamage = Mathf.Max(0f, projectileDamage);
        explosionRadius = Mathf.Max(0.1f, radius);
        explosionDuration = Mathf.Max(0.05f, duration);
        configured = true;
    }

    public void Configure(float projectileDamage, float radius, float duration, float areaDamageMultiplier)
    {
        Configure(projectileDamage, radius, duration);
        damageMultiplier = Mathf.Clamp01(areaDamageMultiplier);
    }

    public void Configure(float projectileDamage, float radius, float duration,
        float areaDamageMultiplier, float quillDamageGrowth,
        bool enableDoubleExplosion, float repeatDelay = 0.09f)
    {
        Configure(projectileDamage, radius, duration, areaDamageMultiplier);
        quillDamageGrowthPerHit = Mathf.Max(0f, quillDamageGrowth);
        doubleExplosion = enableDoubleExplosion;
        secondExplosionDelay = Mathf.Max(0f, repeatDelay);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Brick enemy = collision.gameObject.GetComponentInParent<Brick>();
        if (enemy == null) return;
        UniversalBossAbility boss = enemy.GetComponent<UniversalBossAbility>();
        if (boss != null && boss.TryConsumeProjectile(GetComponent<Animal>()))
        {
            enemy.PlayProjectileImpact(transform.position, Vector2.up);
            return;
        }
        GhostPhasing ghost = enemy.GetComponent<GhostPhasing>();
        if (ghost != null && ghost.TryPassThrough(GetComponent<Animal>())) return;

        Vector3 impactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        SpawnImpactPulse(impactPoint);
    }

    private void SpawnImpactPulse(Vector3 impactPoint)
    {
        int comboVolleyId = GetComponent<Animal>()?.ComboVolleyId ?? 0;
        int turnNumber = GameManager.Instance != null ? GameManager.Instance.TurnNumber : 0;
        if (!doubleExplosion)
        {
            HedgehogExplosionPulse.Create(projectileRenderer, impactPoint, AreaDamage,
                explosionRadius, explosionDuration, comboVolleyId,
                QuillDamageGrowthPerHit, turnNumber);
            return;
        }

        HedgehogExplosionSequence.Create(projectileRenderer, impactPoint,
            AreaDamage, explosionRadius, explosionDuration, comboVolleyId,
            SecondExplosionDelay, QuillDamageGrowthPerHit, turnNumber);
    }

    private void OnValidate()
    {
        explosionRadius = Mathf.Max(0.1f, explosionRadius);
        explosionDuration = Mathf.Max(0.05f, explosionDuration);
        damageMultiplier = Mathf.Clamp01(damageMultiplier);
        quillDamageGrowthPerHit = Mathf.Max(0f, quillDamageGrowthPerHit);
        secondExplosionDelay = Mathf.Max(0f, secondExplosionDelay);
        spinDegreesPerSecond = Mathf.Max(0f, spinDegreesPerSecond);
    }
}

/// <summary>Keeps upgraded follow-up bursts alive even if the projectile is destroyed.</summary>
public sealed class HedgehogExplosionSequence : MonoBehaviour
{
    private bool comboEffectRegistered;
    private int comboVolleyId;

    public static HedgehogExplosionSequence Create(SpriteRenderer source, Vector3 position,
        float damage, float radius, float duration, int comboId, float repeatDelay,
        float damageGrowthPerHit, int turnNumber)
    {
        GameObject sequenceObject = new GameObject("Hedgehog Quill Burst Sequence");
        sequenceObject.transform.position = position;
        HedgehogExplosionSequence sequence =
            sequenceObject.AddComponent<HedgehogExplosionSequence>();
        sequence.comboVolleyId = comboId;
        sequence.comboEffectRegistered = ProjectileComboUI.Instance != null
            && ProjectileComboUI.Instance.BeginEffect(comboId);
        sequence.StartCoroutine(sequence.Play(source, position, damage,
            radius, duration, repeatDelay, damageGrowthPerHit, turnNumber));
        return sequence;
    }

    private IEnumerator Play(SpriteRenderer source, Vector3 position, float damage,
        float radius, float duration, float repeatDelay, float damageGrowthPerHit,
        int turnNumber)
    {
        HedgehogExplosionPulse.Create(source, position, damage, radius, duration,
            comboVolleyId, damageGrowthPerHit, turnNumber);
        yield return new WaitForSeconds(Mathf.Max(0f, repeatDelay));
        HedgehogExplosionPulse.Create(source, position, damage, radius, duration,
            comboVolleyId, damageGrowthPerHit, turnNumber);
        yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (comboEffectRegistered) ProjectileComboUI.Instance?.EndEffect(comboVolleyId);
        comboEffectRegistered = false;
    }
}
