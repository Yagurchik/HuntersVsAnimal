using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Brick))]
public sealed class EnemySpecialistTrait : EnemyProjectileDamageModifier, IEnemyDefeatHandler
{
    public enum Kind
    {
        BarkGuard, Thornling, LeafEater, ShadowWeaver,
        BogLeech, BogMossHealer, BogTinnik,
        BayShellback, BayTideHerald, BayPuffer, BaySaltbearer,
        SnowIceface, SnowWisp, SnowRunesmith, SnowDoppel,
        FireSlag, FireEmber, FireAshCollector, FireBrander
    }

    [SerializeField] private Kind kind;
    [SerializeField] private float effectRadius = 1f;
    private Brick enemy;
    private WinterEnemyForm winter;
    private int lastVolley = int.MinValue;
    private int lastAbilityTurn = int.MinValue;
    private bool firstHitUsed;
    private bool secondChanceUsed;
    private Animal leechProjectile;
    private readonly HashSet<Animal> touchedProjectiles = new HashSet<Animal>();
    private readonly List<Brick> neighbors = new List<Brick>(16);
    private static readonly HashSet<EnemySpecialistTrait> shadowWeavers =
        new HashSet<EnemySpecialistTrait>();

    public Kind Specialization => kind;
    private bool WinterPowerActive => winter == null || winter.IsFrozen;

    private void Awake()
    {
        enemy = GetComponent<Brick>();
        winter = GetComponent<WinterEnemyForm>();
    }

    private void OnEnable()
    {
        if (kind == Kind.ShadowWeaver) shadowWeavers.Add(this);
    }

    private void OnDisable()
    {
        if (kind == Kind.ShadowWeaver) shadowWeavers.Remove(this);
    }

    public static bool HasActiveShadow(RoomDarkness room)
    {
        foreach (EnemySpecialistTrait shadow in shadowWeavers)
            if (shadow != null && shadow.enemy != null && !shadow.enemy.IsDefeated
                && shadow.enemy.levelGenerator != null
                && shadow.enemy.levelGenerator.Darkness == room)
                return true;
        return false;
    }

    public static void CollectShadowPositions(RoomDarkness room, List<Vector3> positions)
    {
        positions.Clear();
        foreach (EnemySpecialistTrait shadow in shadowWeavers)
            if (shadow != null && shadow.enemy != null && !shadow.enemy.IsDefeated
                && shadow.enemy.levelGenerator != null
                && shadow.enemy.levelGenerator.Darkness == room)
                positions.Add(shadow.transform.position);
    }

    public static bool HasNearbyTinnik(Brick target)
    {
        if (target == null || target.UsesBossHealth || target.levelGenerator == null)
            return false;
        var nearby = new List<Brick>(8);
        target.levelGenerator.CollectNearbyEnemies(target.transform.position, 1f,
            true, target, nearby);
        for (int i = 0; i < nearby.Count; i++)
        {
            EnemySpecialistTrait trait = nearby[i] != null
                ? nearby[i].GetComponent<EnemySpecialistTrait>() : null;
            if (trait != null && trait.kind == Kind.BogTinnik && !nearby[i].IsDefeated)
                return true;
        }
        return false;
    }

    public static void NotifyEnemyDefeated(Brick defeatedEnemy)
    {
        if (defeatedEnemy == null || defeatedEnemy.levelGenerator == null) return;
        LevelGenerator room = defeatedEnemy.levelGenerator;
        var nearby = new List<Brick>(12);
        room.CollectNearbyEnemies(defeatedEnemy.transform.position, 1f, true,
            defeatedEnemy, nearby);
        for (int i = 0; i < nearby.Count; i++)
        {
            Brick candidate = nearby[i];
            if (candidate == null || candidate.IsDefeated || candidate.UsesBossHealth) continue;
            EnemySpecialistTrait trait = candidate.GetComponent<EnemySpecialistTrait>();
            if (trait == null || trait.kind != Kind.FireAshCollector) continue;
            if (defeatedEnemy.IsSummonedVariant) continue;
            int volley = Player.CurrentVolleyId;
            if (trait.lastVolley == volley && trait.firstHitUsed) continue;
            trait.lastVolley = volley;
            trait.firstHitUsed = true;
            candidate.Heal(candidate.MaxHp * 0.2f);
        }
    }

    private void BeginVolley(Animal projectile)
    {
        int volley = projectile != null && projectile.ComboVolleyId > 0
            ? projectile.ComboVolleyId : Player.CurrentVolleyId;
        if (volley == lastVolley) return;
        lastVolley = volley;
        firstHitUsed = false;
        if (kind == Kind.BogLeech) secondChanceUsed = false;
        leechProjectile = null;
        touchedProjectiles.Clear();
    }

    public override float ModifyDamage(Brick target, Animal projectile,
        Vector2 impactPoint, float baseDamage)
    {
        if (enemy == null || enemy.IsDefeated || projectile == null) return baseDamage;
        BeginVolley(projectile);
        float damage = Mathf.Max(0f, baseDamage);
        CombatDamageSource projectileSource = CombatDamagePalette.FromProjectile(projectile);
        bool fireProjectile = projectileSource == CombatDamageSource.Firefly
            || projectileSource == CombatDamageSource.Fire;
        switch (kind)
        {
            case Kind.BarkGuard:
                if (impactPoint.y >= enemy.transform.position.y) damage *= 0.7f;
                break;
            case Kind.LeafEater:
                if (!fireProjectile && HasNearbyEnemy()) damage *= 0.8f;
                break;
            case Kind.BayShellback:
                if (!fireProjectile && !firstHitUsed)
                {
                    damage *= 0.5f;
                    firstHitUsed = true;
                }
                break;
            case Kind.Thornling:
            case Kind.BayPuffer:
                if (touchedProjectiles.Add(projectile))
                    DamageProjectile(projectile, kind == Kind.Thornling ? 1f : 2f);
                break;
            case Kind.BogLeech:
                if (!firstHitUsed)
                {
                    firstHitUsed = true;
                    leechProjectile = projectile;
                }
                break;
        }
        return damage;
    }

    public void AfterProjectileHit(Animal projectile)
    {
        if (kind == Kind.BogLeech && enemy != null && !enemy.IsDefeated
            && firstHitUsed && !secondChanceUsed && projectile == leechProjectile)
        {
            DamageProjectile(projectile, 1f);
            enemy.Heal(enemy.MaxHp * 0.08f);
            secondChanceUsed = true;
        }
    }

    public float ModifyIncomingDamage(float damage, CombatDamageSource source, bool directHit)
    {
        if (enemy == null || enemy.IsDefeated) return damage;
        bool fire = source == CombatDamageSource.Fire || source == CombatDamageSource.Firefly;
        if (kind == Kind.SnowIceface && WinterPowerActive)
        {
            if (fire) damage *= 1.5f;
            else if (directHit) damage *= 0.85f;
        }
        if (kind == Kind.FireSlag)
        {
            if (fire) damage *= 0.5f;
            else if (directHit) damage *= 1.15f;
        }
        if (kind == Kind.SnowDoppel && WinterPowerActive && directHit && !fire
            && !secondChanceUsed && damage >= enemy.hp)
        {
            secondChanceUsed = true;
            damage = Mathf.Max(0f, enemy.hp - 1f);
        }
        return damage;
    }

    public void CompleteEnemyTurn(int turnNumber)
    {
        if (enemy == null || enemy.IsDefeated || enemy.levelGenerator == null) return;
        if (lastAbilityTurn == turnNumber) return;
        lastAbilityTurn = turnNumber;
        switch (kind)
        {
            case Kind.BogMossHealer:
                if (turnNumber % 2 == 0)
                {
                    Brick target = PickMostWoundedNeighbor();
                    if (target != null) target.Heal(target.MaxHp * 0.12f);
                }
                break;
            case Kind.BayTideHerald:
                if (turnNumber % 3 == 2)
                {
                    Brick target = PickFrontmostNeighbor();
                    if (target != null)
                        EnemySpecialistBuffs.Ensure(target).GrantExtraStep();
                }
                break;
            case Kind.SnowRunesmith:
                if (WinterPowerActive && turnNumber % 2 == 0)
                {
                    Brick target = PickFrontmostNeighbor();
                    if (target != null)
                        EnemySpecialistBuffs.Ensure(target).GrantShield(
                            target.MaxHp * 0.2f, enemy);
                }
                break;
            case Kind.FireBrander:
                if (turnNumber % 2 == 0)
                {
                    Brick target = PickFrontmostNeighbor();
                    if (target != null)
                        EnemySpecialistBuffs.Ensure(target).GrantBrand(enemy);
                }
                break;
        }
    }

    public bool TryHandleDefeat(Brick defeated)
    {
        if (defeated == null) return false;
        if (kind == Kind.FireEmber) Explode(defeated);
        if (kind == Kind.BaySaltbearer)
            BrineHazard.Spawn(defeated.levelGenerator, defeated.transform.position);
        return false;
    }

    private void Explode(Brick defeated)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(defeated.transform.position, 1f);
        var enemies = new HashSet<Brick>();
        var projectiles = new HashSet<Animal>();
        for (int i = 0; i < hits.Length; i++)
        {
            Brick other = hits[i].GetComponentInParent<Brick>();
            if (other != null)
            {
                if (other != defeated && !other.IsDefeated && !other.UsesBossHealth
                    && other.levelGenerator == defeated.levelGenerator) enemies.Add(other);
                continue;
            }
            Animal projectile = hits[i].GetComponentInParent<Animal>();
            if (projectile != null) projectiles.Add(projectile);
        }
        EnemyImpactEffectPool.PlayBogExplosion(defeated.transform.position, true, 1f);
        foreach (Brick other in enemies)
            if (other != null && !other.IsDefeated)
                other.TakeDamage(defeated.MaxHp * 0.25f, CombatDamageSource.Fire);
        foreach (Animal projectile in projectiles) DamageProjectile(projectile, 2f);
    }

    private bool HasNearbyEnemy()
    {
        CollectNeighbors();
        for (int i = 0; i < neighbors.Count; i++)
            if (IsOrdinaryAlly(neighbors[i])) return true;
        return false;
    }

    private Brick PickMostWoundedNeighbor()
    {
        CollectNeighbors();
        Brick best = null;
        float missing = 0f;
        for (int i = 0; i < neighbors.Count; i++)
        {
            Brick other = neighbors[i];
            if (!IsOrdinaryAlly(other)) continue;
            float deficit = other.MaxHp - other.hp;
            if (deficit > missing) { best = other; missing = deficit; }
        }
        return best;
    }

    private Brick PickFrontmostNeighbor()
    {
        CollectNeighbors();
        Brick best = null;
        for (int i = 0; i < neighbors.Count; i++)
        {
            Brick other = neighbors[i];
            if (!IsOrdinaryAlly(other)) continue;
            if (best == null || other.LowestCellY < best.LowestCellY) best = other;
        }
        return best;
    }

    private void CollectNeighbors()
    {
        neighbors.Clear();
        if (enemy != null && enemy.levelGenerator != null)
            enemy.levelGenerator.CollectNearbyEnemies(enemy.transform.position,
                Mathf.Max(0.1f, effectRadius), true, enemy, neighbors);
    }

    private bool IsOrdinaryAlly(Brick other) => other != null && !other.IsDefeated
        && !other.UsesBossHealth && other.levelGenerator == enemy.levelGenerator;

    private static void DamageProjectile(Animal projectile, float amount)
    {
        if (projectile != null && projectile.TakeEnemyDamage(amount))
            Destroy(projectile.gameObject);
    }
}
