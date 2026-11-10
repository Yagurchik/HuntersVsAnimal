using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animal), typeof(Rigidbody2D), typeof(ProjectilePhysics2D))]
public sealed class HoneyBadgerProjectile : ProjectileDamageModifier
{
    private static readonly List<HoneyBadgerProjectile> ActiveChains =
        new List<HoneyBadgerProjectile>();

    [FormerlySerializedAs("maximumNonLethalHits")]
    [SerializeField, Min(1)] private int maximumAttacks = 5;
    [SerializeField, Min(1)] private int maximumConsecutiveAttacksPerTarget = 1;
    [SerializeField, Min(1), Tooltip("Extra attacks added to the current chain by every kill.")]
    private int attacksAddedPerKill = 3;
    [SerializeField, Min(0.1f), Tooltip("Maximum horizontal or vertical cell offset. Radius 1 includes diagonal neighbours.")]
    private float chainJumpRadius = 1.2f;
    [SerializeField, Range(0f, 2f), Tooltip("Added part of base damage after every kill in one chain.")]
    private float damageIncreasePerKill = 0.25f;
    [SerializeField, Min(1f)] private float chainTravelSpeed = 24f;
    [SerializeField, Min(0f)] private float pauseBetweenAttacks = 0.08f;

    [Header("Enemy retaliation")]
    [SerializeField, Min(0f)] private float poisonCloudDamageMultiplier = 0.25f;
    [SerializeField, Min(0.1f)] private float poisonCloudRadius = 1.5f;
    [SerializeField, Min(1)] private int poisonCloudDurationTurns = 3;
    [SerializeField] private Sprite poisonPuffSprite;
    [SerializeField, Range(0f, 1f), Tooltip("Chance to release a poison cloud after each chain attack.")]
    private float attackPoisonCloudChance = 0.05f;

    [Header("Ultimate")]
    [SerializeField, Min(0f)] private float ultimateDamageBonus = 10f;

    [Header("Chain visual")]
    [SerializeField] private Sprite dustBrawlSprite;
    [SerializeField, Min(0.1f)] private float dustBrawlWorldSize = 1.35f;
    [SerializeField] private float dustBrawlSpinSpeed = 95f;
    private Animal animal;
    private Rigidbody2D body;
    private ProjectilePhysics2D flight;
    private Collider2D[] colliders;
    private Coroutine attackRoutine;
    private bool chainActive;
    private int nonLethalHits;
    private int chainHits;
    private int chainKills;
    private int transferredAttacks;
    private float originalFlightSpeed;
    private float chainBaseDamage;
    private SpriteRenderer dustBrawlRenderer;
    private Vector3 dustBrawlBaseScale;
    private bool ultimateEnabled;
    private bool mergedIntoOther;
    private bool chainFinished;
    private Brick currentTarget;

    public int NonLethalHits => nonLethalHits;
    public int MaximumNonLethalHits => MaximumAttacks;
    public int MaximumAttacks => Mathf.Max(1, maximumAttacks);
    public int MaximumConsecutiveAttacksPerTarget => Mathf.Max(1, maximumConsecutiveAttacksPerTarget);
    public int AttacksAddedPerKill => Mathf.Max(1, attacksAddedPerKill);
    public float ChainJumpRadius => Mathf.Max(0.1f, chainJumpRadius);
    public float DamageIncreasePerKill => Mathf.Max(0f, damageIncreasePerKill);
    public int ChainKills => chainKills;
    public int CurrentChainLength => MaximumAttacks + chainKills * AttacksAddedPerKill
        + Mathf.Max(0, transferredAttacks);
    public int RemainingChainAttacks => Mathf.Max(0, CurrentChainLength - chainHits);
    public int TransferredAttacks => Mathf.Max(0, transferredAttacks);
    public float CurrentDamageMultiplier => 1f + chainKills * DamageIncreasePerKill;
    public float CurrentAttackDamage => chainBaseDamage * CurrentDamageMultiplier
        + (ultimateEnabled
            ? Mathf.Max(0f, ultimateDamageBonus) * (chainHits + 1)
            : 0f);
    public Sprite DustBrawlSprite => dustBrawlSprite;
    public bool IsChainActive => chainActive;
    public bool IsFinishingChain => chainFinished;
    public bool PreventsRecall => chainActive;
    public bool HasUltimate => ultimateEnabled;
    public float PoisonCloudDamageMultiplier => Mathf.Max(0f, poisonCloudDamageMultiplier);
    public float PoisonCloudRadius => Mathf.Max(0.1f, poisonCloudRadius);
    public int PoisonCloudDurationTurns => Mathf.Max(1, poisonCloudDurationTurns);
    public float AttackPoisonCloudChance => Mathf.Clamp01(attackPoisonCloudChance);
    public float UltimateDamageBonus => Mathf.Max(0f, ultimateDamageBonus);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveChains()
    {
        ActiveChains.Clear();
    }

    public void ConfigureRunUpgrades(float attackRadius, int totalAttacks,
        int consecutiveAttacksPerTarget, bool hasUltimate)
    {
        chainJumpRadius = Mathf.Max(0.1f, attackRadius);
        maximumAttacks = Mathf.Max(1, totalAttacks);
        maximumConsecutiveAttacksPerTarget = Mathf.Max(1, consecutiveAttacksPerTarget);
        ultimateEnabled = hasUltimate;
    }

    private void Awake()
    {
        animal = GetComponent<Animal>(); body = GetComponent<Rigidbody2D>();
        flight = GetComponent<ProjectilePhysics2D>();
        colliders = GetComponentsInChildren<Collider2D>(true);
        ProjectileFlightVisual.Ensure(gameObject, 660f);
        EnsureDustBrawlVisual();
    }

    public override float ModifyDamage(Brick enemy, float baseDamage)
    {
        if (chainFinished || mergedIntoOther) return 0f;
        if (enemy != null && !enemy.IsDefeated && !chainActive)
            BeginChain(enemy, Mathf.Max(0f, baseDamage));
        return 0f;
    }

    private void BeginChain(Brick firstTarget, float damage)
    {
        chainActive = true;
        nonLethalHits = 0;
        chainHits = 0;
        chainKills = 0;
        transferredAttacks = 0;
        chainBaseDamage = Mathf.Max(0f, damage);
        mergedIntoOther = false;
        currentTarget = firstTarget;
        if (!ActiveChains.Contains(this)) ActiveChains.Add(this);
        originalFlightSpeed = flight != null ? flight.TargetSpeed : body.velocity.magnitude;
        flight?.StopFlight();
        if (body != null) { body.velocity = Vector2.zero; body.angularVelocity = 0f; body.simulated = false; }
        SetColliders(false);
        SetDustBrawlVisible(true);
        attackRoutine = StartCoroutine(AttackChain(firstTarget, damage));
    }

    private IEnumerator AttackChain(Brick firstTarget, float damage)
    {
        yield return null;
        Brick target = firstTarget;
        Brick lastTarget = null;
        int consecutiveAttacks = 0;
        LevelGenerator room = target != null ? target.levelGenerator : null;
        while (target != null && room != null && !room.IsLost)
        {
            HoneyBadgerProjectile survivor = MergeChainsTargeting(target);
            if (survivor != this || mergedIntoOther) yield break;
            yield return TravelTo(target);
            if (mergedIntoOther) yield break;
            if (target == null || target.IsDefeated)
            {
                target = FindNextTarget(room, transform.position, null);
                currentTarget = target;
                consecutiveAttacks = 0;
                continue;
            }
            survivor = MergeChainsTargeting(target);
            if (survivor != this || mergedIntoOther) yield break;
            UniversalBossAbility boss = target.GetComponent<UniversalBossAbility>();
            if (boss != null && boss.TryConsumeProjectile(animal))
            {
                target.PlayProjectileImpact(transform.position, Vector2.up);
                yield break;
            }
            Vector2 direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            target.PlayProjectileImpact(transform.position,
                direction.sqrMagnitude > 0.001f ? -direction : Vector2.up);
            float attackDamage = CurrentAttackDamage;
            Vector3 attackPosition = target.transform.position;
            target.TakeProjectileDamage(attackDamage,
                animal != null ? animal.ComboVolleyId : 0,
                CombatDamageSource.HoneyBadger);
            bool releasedAttackCloud = Random.value < AttackPoisonCloudChance;
            if (releasedAttackCloud)
                SpawnPoisonCloud(room, attackPosition, attackDamage);
            lastTarget = target;
            bool killed = target == null || target.IsDefeated;
            chainHits++;
            if (killed)
            {
                chainKills++;
                consecutiveAttacks = 0;
            }
            else
            {
                nonLethalHits++;
                consecutiveAttacks++;
                if (animal != null && animal.TakeEnemyDamage(target.ProjectileContactDamage))
                {
                    if (!releasedAttackCloud)
                        SpawnPoisonCloud(room, target.transform.position, damage);
                    Destroy(gameObject);
                    yield break;
                }
            }
            if (chainHits >= CurrentChainLength) { Destroy(gameObject); yield break; }
            Brick previous = target;
            Vector2 jumpOrigin = transform.position;
            if (pauseBetweenAttacks > 0f) yield return new WaitForSeconds(pauseBetweenAttacks);
            if (!killed && consecutiveAttacks < MaximumConsecutiveAttacksPerTarget)
            {
                target = previous;
            }
            else
            {
                target = FindNextTarget(room, jumpOrigin, killed ? null : previous);
                consecutiveAttacks = 0;
            }
            currentTarget = target;
        }
        ResumeOrdinaryFall(lastTarget);
    }

    private HoneyBadgerProjectile MergeChainsTargeting(Brick target)
    {
        if (!chainActive || mergedIntoOther || target == null || target.IsDefeated) return this;
        currentTarget = target;
        HoneyBadgerProjectile survivor = this;
        for (int i = ActiveChains.Count - 1; i >= 0; i--)
        {
            HoneyBadgerProjectile candidate = ActiveChains[i];
            if (candidate == null)
            {
                ActiveChains.RemoveAt(i);
                continue;
            }
            if (!candidate.chainActive || candidate.mergedIntoOther
                || candidate.currentTarget != target) continue;
            if (IsStronger(candidate, survivor)) survivor = candidate;
        }

        for (int i = ActiveChains.Count - 1; i >= 0; i--)
        {
            HoneyBadgerProjectile donor = ActiveChains[i];
            if (donor == null)
            {
                ActiveChains.RemoveAt(i);
                continue;
            }
            if (donor == survivor || !donor.chainActive || donor.mergedIntoOther
                || donor.currentTarget != target) continue;
            donor.MergeInto(survivor);
        }
        return survivor;
    }

    private static bool IsStronger(HoneyBadgerProjectile candidate,
        HoneyBadgerProjectile current)
    {
        if (candidate.CurrentAttackDamage > current.CurrentAttackDamage + 0.0001f) return true;
        if (candidate.CurrentAttackDamage + 0.0001f < current.CurrentAttackDamage) return false;
        return candidate.GetInstanceID() < current.GetInstanceID();
    }

    private void MergeInto(HoneyBadgerProjectile survivor)
    {
        if (survivor == null || survivor == this || mergedIntoOther || !chainActive) return;
        survivor.transferredAttacks += RemainingChainAttacks;
        mergedIntoOther = true;
        chainActive = false;
        currentTarget = null;
        ActiveChains.Remove(this);
        SetDustBrawlVisible(false);
        Destroy(gameObject);
    }

    private void SpawnPoisonCloud(LevelGenerator room, Vector3 position, float baseDamage)
    {
        HoneyBadgerPoisonCloud.Spawn(room, position,
            Mathf.Max(0.1f, baseDamage * Mathf.Max(0f, poisonCloudDamageMultiplier)),
            Mathf.Max(0.1f, poisonCloudRadius), Mathf.Max(1, poisonCloudDurationTurns),
            poisonPuffSprite);
    }

    private IEnumerator TravelTo(Brick target)
    {
        if (target == null) yield break;
        Vector3 start = transform.position;
        float duration = Mathf.Max(0.025f,
            Vector2.Distance(start, target.transform.position) / Mathf.Max(1f, chainTravelSpeed));
        float elapsed = 0f;
        while (elapsed < duration && !mergedIntoOther && target != null && !target.IsDefeated)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.LerpUnclamped(start, target.transform.position,
                t * t * (3f - 2f * t));
            yield return null;
        }
        if (target != null && !target.IsDefeated) transform.position = target.transform.position;
    }

    private Brick FindNextTarget(LevelGenerator room, Vector2 origin, Brick previous)
    {
        Brick weakest = null;
        float lowestHealth = float.PositiveInfinity;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < room.EnemyCollection.Count; i++)
        {
            GameObject value = room.EnemyCollection[i];
            if (value == null || !value.TryGetComponent(out Brick candidate) || candidate.IsDefeated) continue;
            if (candidate == previous) continue;
            Vector2 offset = (Vector2)candidate.transform.position - origin;
            float horizontalDistance = Mathf.Abs(offset.x);
            float verticalDistance = Mathf.Abs(offset.y);
            if (candidate.UsesBossHealth)
            {
                Vector2Int footprint = candidate.Footprint;
                horizontalDistance = Mathf.Max(0f, horizontalDistance - (footprint.x - 1) * 0.5f);
                verticalDistance = Mathf.Max(0f, verticalDistance - (footprint.y - 1) * 0.5f);
            }
            if (Mathf.Max(horizontalDistance, verticalDistance)
                > ChainJumpRadius + 0.0001f) continue;
            float distance = horizontalDistance * horizontalDistance
                + verticalDistance * verticalDistance;
            if (candidate.hp > lowestHealth + 0.0001f) continue;
            if (Mathf.Abs(candidate.hp - lowestHealth) <= 0.0001f
                && distance >= closestDistance) continue;
            lowestHealth = candidate.hp;
            closestDistance = distance;
            weakest = candidate;
        }
        return weakest;
    }

    private void ResumeOrdinaryFall(Brick lastTarget)
    {
        chainFinished = true;
        chainActive = false; attackRoutine = null;
        currentTarget = null;
        ActiveChains.Remove(this);
        SetDustBrawlVisible(false);
        if (lastTarget != null)
        {
            Collider2D targetCollider = lastTarget.GetComponent<Collider2D>();
            if (targetCollider != null)
            {
                Vector3 position = transform.position;
                position.y = Mathf.Min(position.y, targetCollider.bounds.min.y
                    - ProjectilePhysics2D.GetCollisionRadius(gameObject) - 0.025f);
                transform.position = position;
                if (body != null) body.position = position;
            }
        }
        // The chain has exhausted its attacks. Keep its hitboxes disabled for
        // the final descent: a nearby ghost (or any other enemy) must not
        // reflect the badger back upward or start the chain again.
        SetColliders(false);
        if (body != null) body.simulated = true;
        if (flight != null) flight.Launch(Vector2.down, Mathf.Max(1f, originalFlightSpeed));
        else if (body != null) body.velocity = Vector2.down * Mathf.Max(1f, originalFlightSpeed);
    }

    private void EnsureDustBrawlVisual()
    {
        if (dustBrawlSprite == null) return;
        Transform visual = transform.Find("Dust Brawl Visual");
        if (visual == null)
        {
            GameObject visualObject = new GameObject("Dust Brawl Visual");
            visual = visualObject.transform;
            visual.SetParent(transform, false);
        }
        dustBrawlRenderer = visual.GetComponent<SpriteRenderer>();
        if (dustBrawlRenderer == null) dustBrawlRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
        dustBrawlRenderer.sprite = dustBrawlSprite;
        SpriteRenderer source = GetComponent<SpriteRenderer>();
        if (source != null)
        {
            dustBrawlRenderer.sharedMaterial = source.sharedMaterial;
            dustBrawlRenderer.sortingLayerID = source.sortingLayerID;
            dustBrawlRenderer.sortingOrder = source.sortingOrder + 3;
        }
        visual.localPosition = Vector3.zero;
        float parentScale = Mathf.Max(0.0001f,
            Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
        float spriteSize = Mathf.Max(0.0001f,
            Mathf.Max(dustBrawlSprite.bounds.size.x, dustBrawlSprite.bounds.size.y));
        float localScale = dustBrawlWorldSize / (spriteSize * parentScale);
        dustBrawlBaseScale = Vector3.one * localScale;
        visual.localScale = dustBrawlBaseScale;
        dustBrawlRenderer.enabled = false;
    }

    private void SetDustBrawlVisible(bool visible)
    {
        if (dustBrawlRenderer == null) EnsureDustBrawlVisual();
        if (dustBrawlRenderer == null) return;
        dustBrawlRenderer.enabled = visible;
        if (!visible) dustBrawlRenderer.transform.localScale = dustBrawlBaseScale;
    }

    private void LateUpdate()
    {
        if (dustBrawlRenderer == null || !dustBrawlRenderer.enabled) return;
        Transform visual = dustBrawlRenderer.transform;
        visual.rotation = Quaternion.Euler(0f, 0f, Time.time * dustBrawlSpinSpeed);
        visual.localScale = dustBrawlBaseScale * (1f + Mathf.Sin(Time.time * 15f) * 0.045f);
    }

    private void SetColliders(bool value)
    {
        if (colliders == null) colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = value;
    }

    private void OnDestroy()
    {
        ActiveChains.Remove(this);
        if (attackRoutine != null) StopCoroutine(attackRoutine);
    }
    private void OnValidate()
    {
        maximumAttacks = Mathf.Max(1, maximumAttacks);
        maximumConsecutiveAttacksPerTarget = Mathf.Max(1, maximumConsecutiveAttacksPerTarget);
        attacksAddedPerKill = Mathf.Max(1, attacksAddedPerKill);
        chainJumpRadius = Mathf.Max(0.1f, chainJumpRadius);
        damageIncreasePerKill = Mathf.Max(0f, damageIncreasePerKill);
        chainTravelSpeed = Mathf.Max(1f, chainTravelSpeed);
        pauseBetweenAttacks = Mathf.Max(0f, pauseBetweenAttacks);
        poisonCloudDamageMultiplier = Mathf.Max(0f, poisonCloudDamageMultiplier);
        poisonCloudRadius = Mathf.Max(0.1f, poisonCloudRadius);
        poisonCloudDurationTurns = Mathf.Max(1, poisonCloudDurationTurns);
        attackPoisonCloudChance = Mathf.Clamp01(attackPoisonCloudChance);
        ultimateDamageBonus = Mathf.Max(0f, ultimateDamageBonus);
        dustBrawlWorldSize = Mathf.Max(0.1f, dustBrawlWorldSize);
    }
}
