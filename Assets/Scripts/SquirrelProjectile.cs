using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animal))]
public sealed class SquirrelProjectile : ProjectileEnemyEffect
{
    [SerializeField] private SquirrelAcorn acornPrefab;
    private float earlyExplosionChance = 0.05f;
    private int bindingRank;
    private bool chainReaction;

    public SquirrelAcorn AcornPrefab => acornPrefab;

    public void ConfigureRunUpgrades(float detonationChance, int vinesRank, bool hasUltimate)
    {
        earlyExplosionChance = Mathf.Clamp(detonationChance, 0.05f, 0.2f);
        bindingRank = Mathf.Clamp(vinesRank, 0, 3);
        chainReaction = hasUltimate;
    }

    public override void ApplyTo(Brick enemy)
    {
        Animal animal = GetComponent<Animal>();
        if (animal == null || animal.IsReturning || enemy == null || enemy.IsDefeated) return;
        bool alreadyPlanted = SquirrelAcorn.FindOn(enemy) != null;
        SquirrelAcorn seed = SquirrelAcorn.Plant(enemy, acornPrefab, earlyExplosionChance, bindingRank,
            chainReaction, animal.ComboVolleyId);
        // The planting hit never rolls. Only another squirrel collision can
        // detonate a living host's acorn before its death.
        if (alreadyPlanted && seed != null && !seed.IsArtifactSeed) seed.TryDetonateEarly();
    }
}
