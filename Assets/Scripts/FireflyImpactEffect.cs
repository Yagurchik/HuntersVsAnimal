using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireflyImpactEffect : ProjectileEnemyEffect
{
    public const string BurnEffectId = "firefly_burn";
    public const int MaximumBurnStacks = 3;

    [SerializeField, Min(0.1f), Tooltip("Light radius in cells for each active burn stack.")]
    private float lightRadius = 1f;
    [SerializeField, Min(0f), Tooltip("Damage dealt by one burn stack before run upgrades.")]
    private float baseBurnDamage = 25f;
    [SerializeField, Min(0f), HideInInspector] private float minimumBurnDamage = 1f;
    [SerializeField, Min(1)] private int burnTurns = 1;
    [SerializeField, Min(0f)] private float bonfireBaseDamage = 10f;
    [SerializeField, Min(0f)] private float bonfireDamagePerBurnUpgrade = 10f;
    private float runtimeBurnDamageMultiplier = 1f;
    private int burnUpgradeLevel;
    private bool eternalFire;

    public float LightRadius => Mathf.Max(0.1f, lightRadius);
    public float BaseBurnDamage => Mathf.Max(0f, baseBurnDamage);
    public float BurnDamage => Mathf.Max(BaseBurnDamage * Mathf.Max(0f, runtimeBurnDamageMultiplier),
        minimumBurnDamage);
    public int BurnTurns => Mathf.Max(1, burnTurns);
    public float BonfireDamage => bonfireBaseDamage
        + Mathf.Clamp(burnUpgradeLevel, 0, AnimalProjectileQueue.MaximumBranchLevel)
        * bonfireDamagePerBurnUpgrade;
    public bool HasEternalFire => eternalFire;

    public void ConfigureBurnDamageMultiplier(float multiplier, bool hasEternalFire = false,
        int upgradeLevel = 0)
    {
        runtimeBurnDamageMultiplier = Mathf.Max(0f, multiplier);
        eternalFire = hasEternalFire;
        burnUpgradeLevel = Mathf.Clamp(upgradeLevel, 0, AnimalProjectileQueue.MaximumBranchLevel);
    }

    public void ConfigureBurnDamage(float damage, bool hasEternalFire = false,
        int upgradeLevel = 0)
    {
        runtimeBurnDamageMultiplier = BaseBurnDamage > 0f ? Mathf.Max(0f, damage) / BaseBurnDamage : 0f;
        eternalFire = hasEternalFire;
        burnUpgradeLevel = Mathf.Clamp(upgradeLevel, 0, AnimalProjectileQueue.MaximumBranchLevel);
    }

    public float GetLightRadius(int stackCount)
    {
        return (LightRadius + ArtifactEquipmentBonuses.Get(ArtifactStat.FireflyLightRadiusBonus)) * Mathf.Max(1, stackCount);
    }

    public override void ApplyTo(Brick enemy)
    {
        if (enemy == null || enemy.IsDefeated) return;
        float burnDamage = BurnDamage;
        EnemyStatusEffects statuses = enemy.GetComponent<EnemyStatusEffects>();
        if (statuses == null) statuses = enemy.gameObject.AddComponent<EnemyStatusEffects>();
        int stackCount = statuses.ApplyPeriodicDamage(BurnEffectId, burnDamage,
            eternalFire ? int.MaxValue : BurnTurns,
            EnemyStatusEffects.StackingMode.AddDamage,
            eternalFire ? int.MaxValue : MaximumBurnStacks,
            eternalFire);
        enemy.RecordFireflyBonfireDamage(BonfireDamage);
        enemy.GetComponent<EnemyBurnTint>()?.Refresh();

        EnemyIllumination light = enemy.GetComponent<EnemyIllumination>();
        if (light == null) light = enemy.gameObject.AddComponent<EnemyIllumination>();
        light.Illuminate(GetLightRadius(stackCount));
    }
}
