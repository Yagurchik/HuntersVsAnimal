using System.Collections.Generic;
using UnityEngine;

public static class ArtifactCombatEffects
{
    private static int secondaryDepth;
    private static SquirrelAcorn seedPrefab;
    private static BeaverDam damPrefab;
    public static bool IsSecondary => secondaryDepth > 0;
    private static bool Owned(int owner) => owner >= 0 && owner < AnimalProjectileQueue.AnimalTypeCount;
    private static bool Roll(float chance) => chance > 0f && Random.value < chance;

    public static float ModifyDamage(float damage, CombatDamageSource source, int owner, bool periodic)
    {
        if (!Owned(owner) || IsSecondary) return damage;
        damage *= MetaProgression.PumpkinProjectileDamageMultiplier;
        if (source == CombatDamageSource.Fire) damage *= ArtifactEquipmentBonuses.Multiplier(ArtifactStat.FireDamagePct);
        if (source == CombatDamageSource.Poison) damage *= ArtifactEquipmentBonuses.Multiplier(ArtifactStat.PoisonDamagePct);
        if (IsDirect(source, periodic) && Roll(ArtifactEquipmentBonuses.CriticalChance)) damage *= 1.5f;
        return damage;
    }

    public static void BeforeDirectHit(Brick enemy, CombatDamageSource source, int owner)
    {
        if (!Owned(owner) || IsSecondary || !IsDirect(source, false)) return;
        var seed = SquirrelAcorn.FindOn(enemy);
        if (seed != null)
        {
            if (seed.PlantedFrame < Time.frameCount) seed.TryDetonateEarly(ArtifactEquipmentBonuses.SeedEarlyChance);
            return;
        }
        if (!Roll(ArtifactEquipmentBonuses.SeedPlantChance)) return;
        if (seedPrefab == null) seedPrefab = Resources.Load<SquirrelAcorn>("Effects/Artifacts/Acorn");
        SquirrelAcorn.Plant(enemy, seedPrefab, 0f, 0, false, 0, owner, true);
    }

    public static void OnDamage(Brick enemy, float healthLost, CombatDamageSource source, int owner, bool periodic)
    {
        if (!Owned(owner) || healthLost <= 0f) return;
        GameManager.Instance?.Front?.RestoreBarricade(healthLost * ArtifactEquipmentBonuses.Get(ArtifactStat.LifeStealPct));
        Fortify(enemy, healthLost * ArtifactEquipmentBonuses.Get(ArtifactStat.DefenseConversionPct));
        if (IsSecondary || !IsDirect(source, periodic)) return;
        Vector3 origin = enemy.transform.position;
        // Freeze neighbour references before a secondary hit removes the host.
        var splash = ArtifactEquipmentBonuses.SplashChance > 0f ? Neighbours(enemy, ArtifactEquipmentBonuses.SplashRadius) : null;
        var chain = ArtifactEquipmentBonuses.ChainStrikeChance > 0f ? Neighbours(enemy, ArtifactEquipmentBonuses.ChainStrikeRadius) : null;
        var lightning = ArtifactEquipmentBonuses.LightningChance > 0f ? Neighbours(enemy, 1f, true) : null;
        var artwork = enemy.GetComponentInChildren<SpriteRenderer>();

        if (enemy.hp > 0f && !enemy.IsDefeated)
        {
            float poison = healthLost * ArtifactEquipmentBonuses.Fraction(ArtifactStat.PoisonOnHitPct)
                * ArtifactEquipmentBonuses.Multiplier(ArtifactStat.PoisonDamagePct);
            if (poison > 0f)
            {
                var status = enemy.GetComponent<EnemyStatusEffects>();
                if (status == null) status = enemy.gameObject.AddComponent<EnemyStatusEffects>();
                status.ApplyPeriodicDamage("artifact_poison_" + owner, poison, 1,
                    EnemyStatusEffects.StackingMode.AddDamage, 8, false, CombatDamageSource.Poison, owner, true);
            }
            if (Roll(ArtifactEquipmentBonuses.StunChance)) EnemyTurnStun.Apply(enemy);
        }

        // Secondary hits keep the original owner and already calculated damage.
        // They may heal/fortify, but cannot start another artifact proc.
        secondaryDepth++;
        try
        {
            if (enemy.hp > 0f && !enemy.IsDefeated && Roll(ArtifactEquipmentBonuses.EchoChance))
                Hit(enemy, healthLost * ArtifactEquipmentBonuses.Fraction(ArtifactStat.EchoDamagePct), source, owner);
            if (splash != null && splash.Count > 0 && Roll(ArtifactEquipmentBonuses.SplashChance))
            {
                HedgehogExplosionPulse.Create(artwork, origin, 0f, ArtifactEquipmentBonuses.SplashRadius, 0.18f);
                foreach (var target in splash) Hit(target, healthLost * ArtifactEquipmentBonuses.Fraction(ArtifactStat.SplashDamagePct), source, owner);
            }
            if (chain != null && chain.Count > 0 && Roll(ArtifactEquipmentBonuses.ChainStrikeChance))
            {
                var target = chain[Random.Range(0, chain.Count)];
                if (Alive(target))
                {
                    ArtifactLightningEffect.Flash(origin, target.transform.position, true);
                    Hit(target, healthLost * ArtifactEquipmentBonuses.Fraction(ArtifactStat.ChainStrikeDamagePct), source, owner);
                }
            }
            if (lightning != null && lightning.Count > 0 && Roll(ArtifactEquipmentBonuses.LightningChance))
            {
                lightning.Sort((a, b) => (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));
                for (int i = 0; i < Mathf.Min(3, lightning.Count); i++)
                    if (Alive(lightning[i]))
                    {
                        ArtifactLightningEffect.Flash(origin, lightning[i].transform.position);
                        Hit(lightning[i], 25f, CombatDamageSource.Other, owner);
                    }
            }
        }
        finally { secondaryDepth--; ReleaseNeighbourBuffer(splash); ReleaseNeighbourBuffer(chain); ReleaseNeighbourBuffer(lightning); }
    }

    private static void Hit(Brick target, float damage, CombatDamageSource source, int owner)
    {
        if (Alive(target) && damage > 0f) target.TakeProjectileDamage(damage, 0, source, owner, false);
    }
    private static bool Alive(Brick target) => target != null && !target.IsDefeated && target.hp > 0f;
    private static List<Brick> Neighbours(Brick enemy, float radius, bool square = false)
    {
        var result = CombatSnapshotPool<Brick>.Rent();
        if (enemy.levelGenerator != null)
            enemy.levelGenerator.CollectNearbyEnemies(enemy.transform.position, radius, square, enemy, result);
        return result;
    }
    public static void ReleaseNeighbourBuffer(List<Brick> values) => CombatSnapshotPool<Brick>.Return(values);

    private static void Fortify(Brick enemy, float amount)
    {
        if (amount <= 0f || enemy.levelGenerator == null) return;
        var stage = enemy.levelGenerator.GetComponentInParent<Stage>();
        if (stage == null || enemy.levelGenerator.IsLost) return;
        if (stage.DefenseLine == 1)
        {
            var barricade = stage.GetComponent<MainRoomBarricade>();
            if (barricade == null) barricade = GameManager.Instance?.Front?.Barricade;
            barricade?.AddBeaverHealth(amount);
            return;
        }
        var dam = stage.GetComponentInChildren<BeaverDam>(true);
        if (dam == null)
        {
            if (damPrefab == null) damPrefab = Resources.Load<BeaverDam>("Effects/Artifacts/DefenseDam");
            if (damPrefab == null) return;
            dam = Object.Instantiate(damPrefab, stage.transform);
        }
        dam.AddHealth(amount, stage);
    }

    private static bool IsDirect(CombatDamageSource source, bool periodic)
        => !periodic && source != CombatDamageSource.Fire && source != CombatDamageSource.Poison
            && source != CombatDamageSource.AcornExplosion;
}
