using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(960)]
public sealed class SquirrelAcorn : MonoBehaviour
{
    public static float BlastRadius => 1f + ArtifactEquipmentBonuses.Get(ArtifactStat.SeedRadiusBonus);
    public const float BlastDamage = 100f;
    public const int BlastHitCount = 3;
    public const float BlastHitInterval = 0.13f;
    public const float BonfireDamage = 10f;
    public const int BonfireTurns = 1;
    private static readonly Dictionary<Brick, SquirrelAcorn> Planted = new Dictionary<Brick, SquirrelAcorn>();
    private static int activeBlasts;
    [SerializeField] private SpriteRenderer artwork;
    [SerializeField] private LineRenderer blastRing;
    [SerializeField] private ParticleSystem fireBurst;
    [SerializeField] private SpriteRenderer blastArtwork;
    [SerializeField] private Sprite[] blastFrames;
    [SerializeField, Min(0.25f)] private float blastDuration = 0.45f;
    [SerializeField] private SquirrelVineBinding bindingPrefab;
    [SerializeField, Range(0.1f, 1f)] private float relativeSize = 0.8f;
    [SerializeField] private Color pulseColor = new Color(1f, 0.52f, 0.16f, 1f);
    [SerializeField, Min(0.1f)] private float pulseFrequency = 1.8f;
    private Brick host;
    private int damageOwner = AnimalProjectileQueue.SquirrelIndex;
    private bool isArtifactSeed;
    public int DamageOwner => damageOwner;
    public bool IsArtifactSeed => isArtifactSeed;
    public int PlantedFrame { get; private set; }
    private LevelGenerator room;
    private SpriteRenderer enemyArtwork;
    private Transform anchor;
    private float earlyExplosionChance = 0.05f;
    private int bindingRank;
    private int comboVolleyId;
    private bool chainReaction;
    private bool detonated;
    private float plantedAt;
    private float blastUntil;
    private float blastStartedAt;
    private List<Brick> blastTargets;

    public Brick Host => host;
    public bool IsDetonated => detonated;
    public float EarlyExplosionChance => earlyExplosionChance;
    public float BindingChance => bindingRank * 0.03f;
    public static bool HasActiveBlasts => activeBlasts > 0;
    public float BlastDuration => blastDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { Planted.Clear(); activeBlasts = 0; }

    public static SquirrelAcorn FindOn(Brick enemy) => enemy != null
        && Planted.TryGetValue(enemy, out SquirrelAcorn seed) ? seed : null;

    public static SquirrelAcorn Plant(Brick enemy, SquirrelAcorn prefab, float detonationChance,
        int vinesRank, bool ultimate, int comboId, int owner = AnimalProjectileQueue.SquirrelIndex, bool fromArtifact = false)
    {
        if (enemy == null || enemy.IsDefeated || prefab == null || enemy.levelGenerator == null) return null;
        SquirrelAcorn seed = FindOn(enemy);
        if (seed == null)
        {
            seed = Instantiate(prefab, enemy.transform.position, Quaternion.identity, enemy.levelGenerator.transform);
            seed.host = enemy;
            seed.damageOwner = owner;
            seed.isArtifactSeed = fromArtifact;
            seed.PlantedFrame = Time.frameCount;
            Transform visual = enemy.GetComponent<EnemyHitReaction>()?.VisualRoot;
            seed.enemyArtwork = visual != null ? visual.GetComponentInChildren<SpriteRenderer>()
                : enemy.GetComponentInChildren<SpriteRenderer>();
            seed.anchor = seed.enemyArtwork != null ? seed.enemyArtwork.transform : enemy.transform;
            seed.plantedAt = Time.time;
            Planted[enemy] = seed;
        }
        if (seed.isArtifactSeed && !fromArtifact) return seed;
        seed.room = enemy.levelGenerator;
        seed.earlyExplosionChance = Mathf.Clamp(detonationChance, 0f, 0.2f);
        seed.bindingRank = Mathf.Clamp(vinesRank, 0, 3);
        seed.chainReaction = ultimate;
        seed.comboVolleyId = comboId;
        return seed;
    }

    public static void UpgradeAll(float detonationChance, int vinesRank, bool ultimate)
    {
        foreach (SquirrelAcorn seed in Planted.Values)
        {
            if (seed == null || seed.isArtifactSeed) continue;
            seed.earlyExplosionChance = Mathf.Clamp(detonationChance, 0f, 0.2f);
            seed.bindingRank = Mathf.Clamp(vinesRank, 0, 3);
            seed.chainReaction = ultimate;
        }
    }

    public static void NotifyEnemyDefeated(Brick enemy)
    {
        SquirrelAcorn seed = FindOn(enemy);
        if (seed != null) seed.Detonate();
    }

    public bool TryDetonateEarly(float chanceOverride = -1f)
    {
        if (detonated || host == null || host.IsDefeated) return false;
        float chance = chanceOverride >= 0f ? Mathf.Clamp(chanceOverride, 0f, 0.5f) : earlyExplosionChance;
        if (chance <= 0f || Random.value >= chance) return false;
        Detonate();
        return detonated;
    }

    private void Awake()
    {
        if (blastArtwork != null) blastArtwork.enabled = false;
        if (blastRing != null) blastRing.enabled = false;
        if (fireBurst != null)
        {
            fireBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (Application.isPlaying)
                FireflyFlameArt.Apply(fireBurst, fireBurst.GetComponent<ParticleSystemRenderer>());
        }
    }

    public void Detonate()
    {
        // Capture the room before normal defeat handlers remove or transfer
        // the host. The authored effect belongs to the room, so it survives death.
        if (host != null && host.levelGenerator != null) room = host.levelGenerator;
        if (detonated || room == null || room.IsLost) return;
        detonated = true;
        activeBlasts++;
        if (!object.ReferenceEquals(host, null) && Planted.TryGetValue(host, out SquirrelAcorn current) && current == this)
            Planted.Remove(host);
        Vector3 center = host != null ? host.transform.position : transform.position;
        transform.SetParent(room.transform, true);
        transform.position = center;
        transform.rotation = Quaternion.identity;
        if (artwork != null) artwork.enabled = false;
        StartCoroutine(ExplodeAfterHit(center));
    }

    private IEnumerator ExplodeAfterHit(Vector3 center)
    {
        // Finish the triggering damage and death callbacks before the blast.
        yield return null;
        if (room == null || room.IsLost) { Destroy(gameObject); yield break; }
        blastTargets = CombatSnapshotPool<Brick>.Rent();
        float radius = BlastRadius;
        foreach (GameObject candidate in room.EnemyCollection)
        {
            if (candidate == null || !candidate.TryGetComponent(out Brick enemy) || enemy.IsDefeated
                || Vector2.SqrMagnitude((Vector2)(enemy.transform.position - center)) > radius * radius + 0.0001f) continue;
            blastTargets.Add(enemy);
        }
        FireflyBonfire.Spawn(room, center, BonfireDamage * ArtifactEquipmentBonuses.Multiplier(ArtifactStat.SeedDamagePct), radius, radius, BonfireTurns,
            damageOwner);
        blastStartedAt = Time.time;
        if (blastArtwork != null && blastFrames != null && blastFrames.Length > 0)
        {
            blastArtwork.transform.localScale = Vector3.one * radius;
            blastArtwork.sprite = blastFrames[0];
            blastArtwork.color = Color.white;
            blastArtwork.enabled = true;
        }
        if (fireBurst != null) fireBurst.Play(true);
        float totalDamage = BlastDamage * ArtifactEquipmentBonuses.Multiplier(ArtifactStat.SeedDamagePct);
        float ordinaryHit = Mathf.Floor(totalDamage / BlastHitCount);
        try
        {
            for (int hit = 0; hit < BlastHitCount; hit++)
            {
                if (hit > 0) yield return new WaitForSeconds(BlastHitInterval);
                if (room == null || room.IsLost) { Destroy(gameObject); yield break; }
                float damage = hit == BlastHitCount - 1 ? totalDamage - ordinaryHit * (BlastHitCount - 1) : ordinaryHit;
                var neighbours = CombatSnapshotPool<SquirrelAcorn>.Rent();
                try
                {
                    foreach (Brick enemy in blastTargets)
                    {
                        if (enemy == null || enemy.IsDefeated || enemy.levelGenerator != room) continue;
                        if (hit == 0 && chainReaction)
                        {
                            SquirrelAcorn neighbour = FindOn(enemy);
                            if (neighbour != null && neighbour != this) neighbours.Add(neighbour);
                        }
                        // Each real hit restarts the enemy's normal shake. Vines roll once per explosion.
                        bool bind = hit == 0 && BindingChance > 0f && Random.value < BindingChance;
                        enemy.TakeProjectileDamage(damage, comboVolleyId, CombatDamageSource.Fire, damageOwner, false);
                        if (bind && !enemy.IsDefeated)
                        {
                            EnemyTurnStun.Apply(enemy, 1);
                            SquirrelVineBinding.Apply(enemy, bindingPrefab);
                        }
                    }
                    foreach (SquirrelAcorn neighbour in neighbours)
                        if (neighbour != null) neighbour.Detonate();
                }
                finally { CombatSnapshotPool<SquirrelAcorn>.Return(neighbours); }
                blastUntil = Time.time + BlastHitInterval;
                if (blastRing != null) blastRing.enabled = true;
                if (hit > 0 && fireBurst != null) fireBurst.Emit(4);
            }
        }
        finally { ReturnBlastTargets(); }
        // Never destroy the source before all three hits, even with slow frames or a shorter authored animation.
        Destroy(gameObject, Mathf.Max(0f, blastDuration - (Time.time - blastStartedAt)));
    }

    private void LateUpdate()
    {
        if (detonated)
        {
            if (blastArtwork != null && blastArtwork.enabled && blastFrames != null && blastFrames.Length > 0)
            {
                float progress = Mathf.Clamp01((Time.time - blastStartedAt) / blastDuration);
                int frame = Mathf.Min(blastFrames.Length - 1, Mathf.FloorToInt(progress * blastFrames.Length));
                blastArtwork.sprite = blastFrames[frame];
                // The generated final embers disappear smoothly before cleanup.
                blastArtwork.color = new Color(1, 1, 1, 1f - Mathf.InverseLerp(0.82f, 1f, progress));
            }
            if (blastRing != null && blastRing.enabled)
            {
                float t = Mathf.Clamp01(1f - (blastUntil - Time.time) / BlastHitInterval);
                blastRing.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, BlastRadius, t);
                blastRing.startColor = blastRing.endColor = new Color(1f, 0.55f, 0.12f, 0.9f * (1f - t));
            }
            return;
        }
        if (host == null || host.levelGenerator == null || host.levelGenerator.IsLost)
        { Destroy(gameObject); return; }
        if (host.IsDefeated || host.hp <= 0f) { Detonate(); return; }
        room = host.levelGenerator;
        if (transform.parent != room.transform) transform.SetParent(room.transform, true);
        Transform follow = anchor != null ? anchor : host.transform;
        transform.position = follow.position + Vector3.back * 0.025f;
        transform.rotation = follow.rotation;
        if (artwork != null && artwork.sprite != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin((Time.time - plantedAt) * pulseFrequency * Mathf.PI * 2f);
            Vector3 enemySize = enemyArtwork != null && enemyArtwork.sprite != null
                ? Vector3.Scale(enemyArtwork.sprite.bounds.size, follow.lossyScale)
                : (Vector3)(Vector2)host.Footprint;
            Vector3 spriteSize = artwork.sprite.bounds.size;
            float scale = relativeSize * Mathf.Lerp(0.94f, 1.06f, pulse)
                * Mathf.Min(Mathf.Abs(enemySize.x) / Mathf.Max(0.01f, spriteSize.x),
                    Mathf.Abs(enemySize.y) / Mathf.Max(0.01f, spriteSize.y));
            Vector3 parentScale = artwork.transform.parent.lossyScale;
            artwork.transform.localScale = new Vector3(scale / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
            Color tint = Color.Lerp(Color.white, pulseColor, Mathf.Lerp(0.2f, 0.75f, pulse));
            tint.a = Mathf.Lerp(0.35f, 0.75f, pulse);
            artwork.color = tint;
            artwork.enabled = room.Darkness == null || room.Darkness.IsVisible(transform.position);
        }
    }

    private void ReturnBlastTargets()
    {
        if (blastTargets == null) return;
        CombatSnapshotPool<Brick>.Return(blastTargets); blastTargets = null;
    }

    private void OnDestroy()
    {
        ReturnBlastTargets();
        if (detonated) activeBlasts = Mathf.Max(0, activeBlasts - 1);
        if (!object.ReferenceEquals(host, null) && Planted.TryGetValue(host, out SquirrelAcorn current) && current == this)
            Planted.Remove(host);
        if (detonated && Application.isPlaying)
        {
            GameManager.Instance?.NotifyProjectileCollectionChanged();
            GameManager.Instance?.TryCompletePlayerTurn();
        }
    }
}
