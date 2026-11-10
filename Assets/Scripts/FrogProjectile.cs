using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animal), typeof(ProjectilePhysics2D))]
public sealed class FrogProjectile : ProjectileDamageModifier, IProjectileHitResolved
{
    [SerializeField] private FrogRider riderPrefab;
    private Animal animal;
    private Brick landingTarget;
    private float jumpRadius = 2f;
    private int chorusRank;
    private bool ultimate;
    private bool resolved;

    public FrogRider RiderPrefab => riderPrefab;

    private void Awake() => animal = GetComponent<Animal>();

    public void ConfigureRunUpgrades(float radius, int supportRank, bool hasUltimate)
    {
        jumpRadius = Mathf.Max(2f, radius);
        chorusRank = Mathf.Clamp(supportRank, 0, 3);
        ultimate = hasUltimate;
    }

    public override float ModifyDamage(Brick enemy, float baseDamage)
    {
        // A full enemy behaves as an ordinary physical ricochet, without a bite.
        if (resolved || enemy == null || enemy.IsDefeated || animal.IsReturning
            || riderPrefab == null || FrogRider.CountOn(enemy) >= FrogRider.MaximumPerEnemy)
        {
            return 0f;
        }

        resolved = true;
        landingTarget = enemy;
        GetComponent<ProjectilePhysics2D>()?.StopFlight();
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null) { body.velocity = Vector2.zero; body.simulated = false; }
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
        {
            collider.enabled = false;
        }

        return 0f;
    }

    public void OnProjectileHitResolved(Brick enemy, Vector2 impactPoint, Vector2 impactNormal)
    {
        if (landingTarget == null)
        {
            return;
        }

        Brick target = landingTarget;
        landingTarget = null;
        FrogRider rider = Instantiate(riderPrefab, transform.position, Quaternion.identity,
            target.levelGenerator != null ? target.levelGenerator.transform : null);
        rider.Initialize(target, animal.dmg, jumpRadius, chorusRank, ultimate, animal.ComboVolleyId);
        Destroy(gameObject);
    }
}
