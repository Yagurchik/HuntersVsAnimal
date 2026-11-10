using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(EnemyDarknessVisibility))]
[RequireComponent(typeof(EnemyIllumination))]
[RequireComponent(typeof(EnemyStatusEffects))]
[RequireComponent(typeof(EnemyHitReaction))]
[RequireComponent(typeof(EnemyBurnTint))]
public class Brick : MonoBehaviour
{
    public static event System.Action<Brick> BecameActive;

    public enum ImpactEffectStyle
    {
        None,
        WhiteDust,
        BogSplash
    }

    [SerializeField, Tooltip("Template HP. In a room, health uses the ordinary base, type/line multiplier and campaign level.")]
    public float hp;
    [SerializeField, Tooltip("Damage dealt to projectiles. A boss deals 1% of current HP to a beaver dam; barricade damage is fixed on DefenseLineRuntime.")]
    public float dmg;
    [Header("Health balance")]
    [SerializeField, Min(0.01f), Tooltip("Non-boss HP = ordinary enemy HP × this coefficient × level/wave growth.")]
    private float healthCoefficient = 1f;
    [SerializeField, Tooltip("Use the boss HP multiplier for the line where this enemy first spawns.")]
    private bool bossHealth;
    [SerializeField, Min(0.01f), Tooltip("Share of the line boss HP; split heirs use one quarter.")]
    private float bossHealthFraction = 1f;
    [SerializeField, Min(0), Tooltip("Experience awarded when this enemy is defeated.")]
    private int experienceReward = 10;
    [SerializeField, Tooltip("Poison damage does not affect this enemy; ignited poison still deals fire damage.")]
    private bool immuneToPoison;
    [SerializeField] private Image hpbar;
    [SerializeField, Tooltip("Occupied grid cells, independent of sprite and collider scale.")]
    private Vector2Int footprint = Vector2Int.one;
    [SerializeField, Tooltip("Optional occupied cells inside the footprint, counted from its top-left cell. "
        + "Leave empty for a solid rectangle.")]
    private List<Vector2Int> occupiedCellOffsets = new List<Vector2Int>();

    [Header("Projectile impact effect")]
    [SerializeField] private ImpactEffectStyle impactEffectStyle = ImpactEffectStyle.WhiteDust;
    [SerializeField, Tooltip("Optional per-enemy effect prefab. It overrides the built-in style above.")]
    private GameObject impactEffectPrefab;
    [SerializeField, Min(0.1f)] private float impactEffectLifetime = 1f;
    [SerializeField] private Color impactEffectColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField, Range(1, 32)] private int impactParticleCount = 11;
    [SerializeField, Min(0.1f)] private float impactEffectScale = 1f;

    [Header("Firefly death bonfire")]
    [SerializeField] private bool leaveFireflyBonfire = true;
    [SerializeField, Min(1)] private int bonfireDurationTurns = 3;
    [SerializeField, Min(0.1f)] private float bonfireDamageMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float bonfireRadiusMultiplier = 1f;
    private float fireflyBonfireDamage = 10f;

    [Header("Enemy animation")]
    [SerializeField, Min(0f), Tooltip("Seconds spent gliding to the next grid cell.")]
    private float stepDuration = 0.28f;
    [SerializeField, Min(0f), Tooltip("Optional jump height; zero gives the ghost's glide.")]
    private float stepJumpHeight;
    [SerializeField, Range(0f, 0.3f)] private float stepStretch;
    [SerializeField, Range(0f, 0.3f)] private float landingSquash;
    [SerializeField, Tooltip("Optional Animator used by this enemy's sprite animations.")]
    private Animator spriteAnimator;
    [SerializeField, Tooltip("Optional Animator trigger fired when a step begins.")]
    private string stepAnimationTrigger = "Step";
    [SerializeField, Tooltip("Optional Animator trigger fired after a non-lethal hit.")]
    private string hitAnimationTrigger = "Hit";
    [SerializeField, Tooltip("Optional Animator trigger fired on defeat.")]
    private string deathAnimationTrigger = "Death";
    [SerializeField, Min(0f), Tooltip("Visual-only reveal when an enemy appears in a room.")]
    private float arrivalDuration = 0.16f;
    [SerializeField, Min(0f), Tooltip("Visual-only lunge and retreat before a room breach resolves.")]
    private float breachDuration = 0.42f;

    //[SerializeField] private TextMeshProUGUI text;
    private float maxHp;
    private bool defeated;
    private Coroutine stepAnimation;
    private Coroutine visualTransition;
    private Transform transitionVisual;
    private Vector3 transitionRestScale;
    private SpriteRenderer[] transitionSprites;
    private float[] transitionBaseAlphas;
    private Vector3 restingLocalScale;
    private EnemyCombatTextUI combatText;
    private EnemyHitReaction hitReaction;
    private SpriteRenderer[] orderedSprites;
    private int[] authoredSpriteOrderOffsets;
    private int appliedColumnOrder = int.MinValue;
    private readonly List<ProjectileEnemyEffect> projectileEffects = new List<ProjectileEnemyEffect>();
    private readonly List<ProjectileDamageModifier> projectileDamageModifiers = new List<ProjectileDamageModifier>();
    private readonly List<MonoBehaviour> projectileHitObservers = new List<MonoBehaviour>();
    private readonly List<EnemyProjectileDamageModifier> enemyProjectileDamageModifiers =
        new List<EnemyProjectileDamageModifier>();
    private readonly List<MonoBehaviour> defeatBehaviours = new List<MonoBehaviour>();
    private ZombieResurrection zombieResurrection;
    private BossEnemyTurnController bossTurnController;
    private bool summonedVariant;
    public LevelGenerator levelGenerator;
    // A grave is stationary, but remains a shootable enemy until broken or revived.
    public bool IsDefeated => defeated && (zombieResurrection == null
        || !zombieResurrection.IsAwaitingResurrection);
    public bool IsSummonedVariant => summonedVariant;
    // Event targets such as the Golden Acorn never take a step.
    public bool IsStationary { get; set; }

    // Clock siege events: an elite or a treasure keeps its prefab, gains
    // health and is tinted so it stands out from ordinary enemies.
    public void ApplyClockEventVariant(float healthMultiplier, Color tint)
    {
        if (defeated)
        {
            return;
        }

        float multiplier = Mathf.Max(1f, healthMultiplier);
        hp *= multiplier;
        maxHp *= multiplier;
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            Color color = renderer.color;
            renderer.color = new Color(color.r * tint.r, color.g * tint.g, color.b * tint.b, color.a);
        }
        GetComponent<EnemyBurnTint>()?.CaptureCurrentAsBaseColors();
        UpdateHealthUI();
    }
    public int ExperienceReward => Mathf.Max(0, experienceReward);
    public bool OccupiesGrid => !defeated || (zombieResurrection != null && zombieResurrection.BlocksCell);
    public float MaxHp => maxHp;
    public float ProjectileContactDamage => Mathf.Max(0f, dmg);
    public float BreachDamage => Mathf.Max(0f, hp) * 0.01f;
    public float HealthCoefficient => Mathf.Max(0.01f, healthCoefficient);
    public bool UsesBossHealth => bossHealth;
    public float BossHealthFraction => Mathf.Max(0.01f, bossHealthFraction);
    public bool ImmuneToPoison => immuneToPoison;
    public bool IsStepping { get; private set; }
    public bool IsVisualTransitioning => visualTransition != null;
    public Vector3 StepStartPosition { get; private set; }
    public Vector3 StepTargetPosition { get; private set; }
    public float StepAnimationDuration => Mathf.Max(0f, stepDuration);
    public ImpactEffectStyle ImpactStyle => impactEffectStyle;
    public GameObject ImpactEffectPrefab => impactEffectPrefab;
    public Vector2Int Footprint => new Vector2Int(Mathf.Clamp(footprint.x, 1, LevelGenerator.MaxColumns),
        Mathf.Clamp(footprint.y, 1, Stage.MaxBattlefieldHeight));
    public bool HasCustomFootprint => occupiedCellOffsets != null && occupiedCellOffsets.Count > 0;
    public int OccupiedCellCount
    {
        get
        {
            if (!HasCustomFootprint)
            {
                return Footprint.x * Footprint.y;
            }

            int count = 0;
            for (int i = 0; i < occupiedCellOffsets.Count; i++)
            {
                if (IsOffsetInsideFootprint(occupiedCellOffsets[i]))
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
        }
    }
    public int LowestOccupiedRowOffset
    {
        get
        {
            if (!HasCustomFootprint)
            {
                return Footprint.y - 1;
            }

            int lowest = 0;
            for (int i = 0; i < occupiedCellOffsets.Count; i++)
            {
                if (IsOffsetInsideFootprint(occupiedCellOffsets[i]))
                {
                    lowest = Mathf.Max(lowest, occupiedCellOffsets[i].y);
                }
            }

            return lowest;
        }
    }
    public float LowestCellY => transform.position.y + (Footprint.y - 1) * 0.5f
        - LowestOccupiedRowOffset;
    public Vector3 CombatTextAnchor => transform.position + Vector3.up * ((Footprint.y - 1) * 0.5f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeEvents()
    {
        BecameActive = null;
    }

    public void ConfigureSummonedVariant(int reward, Color tintMultiplier)
    {
        experienceReward = Mathf.Max(0, reward);
        if (summonedVariant)
        {
            return;
        }

        summonedVariant = true;

        Color tint = new Color(Mathf.Clamp01(tintMultiplier.r), Mathf.Clamp01(tintMultiplier.g),
            Mathf.Clamp01(tintMultiplier.b), Mathf.Clamp01(tintMultiplier.a));
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            Color color = renderer.color;
            renderer.color = new Color(color.r * tint.r, color.g * tint.g,
                color.b * tint.b, color.a * tint.a);
        }
        // Firefly orange is blended over this green base and restores it when the burn expires.
        GetComponent<EnemyBurnTint>()?.CaptureCurrentAsBaseColors();
    }

    public void SetSummonedHealth(float health)
    {
        if (defeated || !summonedVariant)
        {
            return;
        }

        hp = maxHp = Mathf.Max(1f, health);
        UpdateHealthUI();
    }

    public void InheritBossHealth(Brick parent)
    {
        if (!bossHealth || defeated || parent == null)
        {
            return;
        }

        hp = maxHp = Mathf.Max(1f, parent.MaxHp * BossHealthFraction);
        UpdateHealthUI();
    }

    // Boss consumption is not a player kill: no XP, kill counter, resurrection,
    // death bonfire, or ordinary defeat effects are triggered.
    public float VanishForBossAbsorption()
    {
        if (!OccupiesGrid)
        {
            return 0f;
        }

        float stolenHealth = Mathf.Max(0f, hp);
        defeated = true;
        if (stepAnimation != null)
        {
            StopCoroutine(stepAnimation);
        }

        stepAnimation = null;
        IsStepping = false;
        LevelGenerator room = levelGenerator;
        levelGenerator = null;
        room?.DestroyEnemy(gameObject, false);
        combatText?.Unregister(this);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return stolenHealth;
    }

    public void AbsorbHealth(float amount)
    {
        if (defeated || amount <= 0f)
        {
            return;
        }

        maxHp = Mathf.Min(float.MaxValue, maxHp + amount);
        hp = Mathf.Min(maxHp, hp + amount);
        UpdateHealthUI();
    }

    public void Heal(float amount)
    {
        if (defeated || amount <= 0f)
        {
            return;
        }

        hp = Mathf.Min(maxHp, hp + amount);
        UpdateHealthUI();
    }

    private void Awake()
    {
        restingLocalScale = transform.localScale;
        if (spriteAnimator == null)
        {
            spriteAnimator = GetComponentInChildren<Animator>();
        }

        hitReaction = GetComponent<EnemyHitReaction>();
        zombieResurrection = GetComponent<ZombieResurrection>();
        bossTurnController = GetComponent<BossEnemyTurnController>();
        if (hitReaction == null)
        {
            hitReaction = gameObject.AddComponent<EnemyHitReaction>();
        }

        if (GetComponent<EnemyBurnTint>() == null)
        {
            gameObject.AddComponent<EnemyBurnTint>();
        }
        // Also cover older scene instances/prefabs that predate the visual component.
        if (GetComponent<EnemyDarknessVisibility>() == null)
        {
            gameObject.AddComponent<EnemyDarknessVisibility>();
        }

        if (GetComponent<EnemyIllumination>() == null)
        {
            gameObject.AddComponent<EnemyIllumination>();
        }

        if (GetComponent<EnemyStatusEffects>() == null)
        {
            gameObject.AddComponent<EnemyStatusEffects>();
        }

        LevelGenerator spawnRoom = GetComponentInParent<LevelGenerator>();
        if (spawnRoom != null)
        {
            if (!bossHealth)
            {
                hp = spawnRoom.OrdinaryEnemyBaseHealth * HealthCoefficient;
            }
            else if (LevelProgression.Instance != null)
            {
                hp = spawnRoom.OrdinaryEnemyBaseHealth
                    * LevelProgression.Instance.GetBossHealthMultiplier(ClockSiegeDirector.BossHealthLineFor(spawnRoom.DefenseLine))
                    * BossHealthFraction;
            }
        }
        if (LevelProgression.Instance != null)
        {
            hp *= LevelProgression.Instance.EnemyHealthMultiplier;
        }
        if (GameManager.Instance != null && GameManager.Instance.IsSurvivalMode)
        {
            hp *= GameManager.Instance.CurrentSurvivalHealthMultiplier;
            dmg *= GameManager.Instance.CurrentSurvivalDamageMultiplier;
        }

        maxHp = hp;
        CacheSpriteOrderOffsets();
        // The authored foreground UI now owns the bar and its number. Keep the
        // old world-space bar inactive on existing enemy prefab variants.
        if (hpbar != null)
        {
            Canvas oldBar = hpbar.GetComponentInParent<Canvas>();
            if (oldBar != null && oldBar.transform.IsChildOf(transform))
            {
                oldBar.gameObject.SetActive(false);
            }
        }
        combatText = EnemyCombatTextUI.Ensure();
        combatText.Register(this, hp);
        UpdateHealthUI();
    }

    private void OnEnable()
    {
        BecameActive?.Invoke(this);
    }

    private void OnTransformChildrenChanged()
    {
        CacheSpriteOrderOffsets();
    }

    private void CacheSpriteOrderOffsets()
    {
        orderedSprites = GetComponentsInChildren<SpriteRenderer>(true);
        authoredSpriteOrderOffsets = new int[orderedSprites.Length];
        int currentColumnOrder = appliedColumnOrder == int.MinValue ? 0 : appliedColumnOrder;
        for (int i = 0; i < orderedSprites.Length; i++)
        {
            authoredSpriteOrderOffsets[i] = orderedSprites[i].sortingOrder - currentColumnOrder;
        }

        appliedColumnOrder = int.MinValue;
    }

    private void LateUpdate()
    {
        if (orderedSprites == null)
        {
            CacheSpriteOrderOffsets();
        }
        // Grid columns have unit spacing. Give the right-hand head the higher
        // sprite order without changing the collider, occupied cells or HUD.
        LevelGenerator room = levelGenerator != null
            ? levelGenerator : GetComponentInParent<LevelGenerator>();
        float column = transform.position.x - (room != null ? room.transform.position.x : 0f);
        int columnOrder = Mathf.Clamp(Mathf.RoundToInt(column * 2f), 0,
            LevelGenerator.MaxColumns * 2);
        if (columnOrder == appliedColumnOrder)
        {
            return;
        }

        for (int i = 0; i < orderedSprites.Length; i++)
        {
            if (orderedSprites[i] != null)
            {
                orderedSprites[i].sortingOrder = columnOrder + authoredSpriteOrderOffsets[i];
            }
        }

        appliedColumnOrder = columnOrder;
    }

    private void Start()
    {
        // The UI keeps its own per-level set, so transferred enemies and later
        // waves never repeat a card. Start also lets summoned variants finish
        // their immediate post-Instantiate configuration before being described.
        EnemyFirstEncounterUI.ReportSpawned(this);
    }

    public void ApplySurvivalGrowth(float healthFactor, float damageFactor)
    {
        if (defeated)
        {
            return;
        }

        float safeHealthFactor = Mathf.Max(1f, healthFactor);
        float safeDamageFactor = Mathf.Max(1f, damageFactor);
        hp *= safeHealthFactor;
        maxHp *= safeHealthFactor;
        dmg *= safeDamageFactor;
        UpdateHealthUI();
    }

    public void ApplyCampaignWaveHealth(float waveMultiplier)
    {
        if (defeated || GameManager.Instance == null || GameManager.Instance.IsSurvivalMode)
        {
            return;
        }

        float safeMultiplier = Mathf.Max(1f, waveMultiplier);
        hp *= safeMultiplier;
        maxHp *= safeMultiplier;
        UpdateHealthUI();
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Animal"))
        {
            Animal animal = collision.gameObject.GetComponentInParent<Animal>();
            if (animal != null)
            {
                GhostPhasing ghost = GetComponent<GhostPhasing>();
                if (ghost != null && ghost.TryPassThrough(animal))
                {
                    return;
                }

                BoarProjectile boar = animal.GetComponent<BoarProjectile>();
                if (boar != null && boar.PassesThroughEnemies)
                {
                    return;
                }

                ContactPoint2D contact = collision.contactCount > 0
                    ? collision.GetContact(0) : default;
                Vector2 impactPoint = collision.contactCount > 0
                    ? contact.point : (Vector2)animal.transform.position;
                Vector2 impactNormal = collision.contactCount > 0
                    ? contact.normal : Vector2.up;
                ApplyProjectileHit(animal, impactPoint, impactNormal);
            }
        }
    }

    public bool ApplyProjectileHit(Animal animal, Vector2 impactPoint, Vector2 impactNormal)
    {
        if (animal == null || IsDefeated)
        {
            return false;
        }

        GhostPhasing ghost = GetComponent<GhostPhasing>();
        if (ghost != null && ghost.TryPassThrough(animal))
        {
            return false;
        }

        UniversalBossAbility universalBoss = GetComponent<UniversalBossAbility>();
        if (universalBoss != null && universalBoss.TryConsumeProjectile(animal))
        {
            PlayProjectileImpact(impactPoint, impactNormal);
            return true;
        }
        // Beaver bites supply their own authored sawdust on every damage tick.
        if (animal.GetComponent<BeaverProjectile>() == null)
        {
            PlayProjectileImpact(impactPoint, impactNormal);
        }

        projectileEffects.Clear();
        animal.GetComponents(projectileEffects);
        if (zombieResurrection == null || !zombieResurrection.IsAwaitingResurrection)
        {
            for (int i = 0; i < projectileEffects.Count; i++)
            {
                if (projectileEffects[i] != null)
                {
                    projectileEffects[i].ApplyTo(this);
                }
            }
        }

        float directDamage = animal.dmg;
        projectileDamageModifiers.Clear();
        animal.GetComponents(projectileDamageModifiers);
        for (int i = 0; i < projectileDamageModifiers.Count; i++)
        {
            if (projectileDamageModifiers[i] != null)
            {
                directDamage = projectileDamageModifiers[i].ModifyDamage(this, directDamage);
            }
        }

        enemyProjectileDamageModifiers.Clear();
        GetComponents(enemyProjectileDamageModifiers);
        for (int i = 0; i < enemyProjectileDamageModifiers.Count; i++)
        {
            if (enemyProjectileDamageModifiers[i] != null)
            {
                directDamage = enemyProjectileDamageModifiers[i]
                    .ModifyDamage(this, animal, impactPoint, directDamage);
            }
        }

        EnemySpecialistBuffs specialistBuffs = GetComponent<EnemySpecialistBuffs>();
        specialistBuffs?.OnProjectileHit(animal);
        TakeProjectileDamage(directDamage, animal.ComboVolleyId,
            CombatDamagePalette.FromProjectile(animal));
        GetComponent<EnemySpecialistTrait>()?.AfterProjectileHit(animal);
        projectileHitObservers.Clear();
        animal.GetComponents(projectileHitObservers);
        for (int i = 0; i < projectileHitObservers.Count; i++)
        {
            if (projectileHitObservers[i] is IProjectileHitResolved observer)
            {
                observer.OnProjectileHitResolved(this, impactPoint, impactNormal);
            }
        }

        return true;
    }

    private void PlayProjectileImpact(Collision2D collision)
    {
        ContactPoint2D contact = collision.contactCount > 0
            ? collision.GetContact(0)
            : default;
        Vector2 point = collision.contactCount > 0 ? contact.point : (Vector2)transform.position;
        Vector2 normal = collision.contactCount > 0 ? contact.normal : Vector2.up;
        PlayProjectileImpact(point, normal);
    }

    public void PlayProjectileImpact(Vector2 worldPoint, Vector2 surfaceNormal)
    {
        Vector3 point = new Vector3(worldPoint.x, worldPoint.y, transform.position.z);
        Vector2 normal = surfaceNormal.sqrMagnitude > 0.0001f
            ? surfaceNormal.normalized : Vector2.up;

        if (impactEffectPrefab != null)
        {
            float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
            GameObject effect = Instantiate(impactEffectPrefab, point,
                Quaternion.AngleAxis(angle, Vector3.forward));
            effect.transform.localScale *= Mathf.Max(0.1f, impactEffectScale);
            foreach (Renderer effectRenderer in effect.GetComponentsInChildren<Renderer>(true))
            {
                effectRenderer.sortingOrder = Mathf.Max(effectRenderer.sortingOrder,
                    RoomDarkness.WorldOverlayOrder + 20);
            }

            foreach (Canvas effectCanvas in effect.GetComponentsInChildren<Canvas>(true))
            {
                effectCanvas.overrideSorting = true;
                effectCanvas.sortingOrder = Mathf.Max(effectCanvas.sortingOrder,
                    RoomDarkness.WorldOverlayOrder + 20);
            }
            foreach (ParticleSystem particleSystem in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particleSystem.Play(true);
            }

            Destroy(effect, Mathf.Max(0.1f, impactEffectLifetime));
            return;
        }

        if (impactEffectStyle == ImpactEffectStyle.WhiteDust)
        {
            EnemyImpactEffectPool.PlayWhiteDust(point, normal, impactEffectColor,
                impactParticleCount, impactEffectScale);
        }
        else if (impactEffectStyle == ImpactEffectStyle.BogSplash)
        {
            EnemyImpactEffectPool.PlayBogSplash(point, normal,
                impactParticleCount, impactEffectScale);
        }
    }

    private void OnValidate()
    {
        healthCoefficient = Mathf.Max(0.01f, healthCoefficient);
        impactEffectLifetime = Mathf.Max(0.1f, impactEffectLifetime);
        impactParticleCount = Mathf.Clamp(impactParticleCount, 1, 32);
        impactEffectScale = Mathf.Max(0.1f, impactEffectScale);
        bonfireDurationTurns = Mathf.Max(1, bonfireDurationTurns);
        bonfireDamageMultiplier = Mathf.Max(0.1f, bonfireDamageMultiplier);
        bonfireRadiusMultiplier = Mathf.Max(0.1f, bonfireRadiusMultiplier);
        stepDuration = Mathf.Max(0f, stepDuration);
        arrivalDuration = Mathf.Max(0f, arrivalDuration);
        breachDuration = Mathf.Max(0f, breachDuration);
        stepJumpHeight = Mathf.Max(0f, stepJumpHeight);
        stepStretch = Mathf.Clamp(stepStretch, 0f, 0.3f);
        landingSquash = Mathf.Clamp(landingSquash, 0f, 0.3f);
        footprint = Footprint;
        if (occupiedCellOffsets == null)
        {
            occupiedCellOffsets = new List<Vector2Int>();
        }

        for (int i = occupiedCellOffsets.Count - 1; i >= 0; i--)
        {
            if (!IsOffsetInsideFootprint(occupiedCellOffsets[i])
                || occupiedCellOffsets.IndexOf(occupiedCellOffsets[i]) != i)
            {
                occupiedCellOffsets.RemoveAt(i);
            }
        }
    }
    public void TakeProjectileDamage(float damage, int comboVolleyId,
        CombatDamageSource source = CombatDamageSource.Other, int damageOwner = -1, bool directHit = true)
    {
        if (IsDefeated || damage <= 0f)
        {
            return;
        }

        ProjectileComboUI.Instance?.RegisterHit(comboVolleyId);
        ApplyDamage(damage, source, false, damageOwner, directHit, false,
            comboVolleyId > 0 ? comboVolleyId : Player.CurrentVolleyId);
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, CombatDamageSource.Other);
    }

    public void TakeDamage(float damage, CombatDamageSource source)
    {
        ApplyDamage(damage, source, false);
    }

    public void TakeAfterTurnDamage(float damage, CombatDamageSource source, int damageOwner = -1, bool alreadyModified = false)
    {
        ApplyDamage(damage, source, true, damageOwner, false, alreadyModified);
    }

    public void TakeFrogDamage(float damage, int comboVolleyId, bool periodic, bool directHit = true)
    {
        if (!periodic && !IsDefeated && damage > 0f)
        {
            ProjectileComboUI.Instance?.RegisterHit(comboVolleyId);
        }

        ApplyDamage(damage, CombatDamageSource.Frog, periodic, -1, !periodic && directHit, false, comboVolleyId);
    }

    public void RecordFireflyBonfireDamage(float damage)
    {
        fireflyBonfireDamage = Mathf.Max(fireflyBonfireDamage, damage);
    }

    public void TakePoisonDamage(float damage, bool ignited)
    {
        TakeAfterTurnDamage(damage,
            ignited ? CombatDamageSource.Fire : CombatDamageSource.Poison,
            AnimalProjectileQueue.HoneyBadgerIndex);
    }

    private void ApplyDamage(float damage, CombatDamageSource source, bool afterTurnEffect, int damageOwner = -1, bool directHit = false, bool alreadyModified = false, int comboVolleyId = -1)
    {
        if (IsDefeated || damage <= 0f || (immuneToPoison && source == CombatDamageSource.Poison))
        {
            return;
        }

        int artifactOwner = damageOwner >= 0 ? damageOwner : ProjectileDamageStats.IndexFor(source);
        if (!afterTurnEffect && directHit)
        {
            ArtifactCombatEffects.BeforeDirectHit(this, source, artifactOwner);
        }

        if (!alreadyModified)
        {
            damage = ArtifactCombatEffects.ModifyDamage(damage, source, artifactOwner, afterTurnEffect || !directHit);
        }

        if (directHit)
        {
            EnemySpecialistBuffs buffs = GetComponent<EnemySpecialistBuffs>();
            if (buffs != null)
            {
                damage = buffs.AbsorbDirectDamage(damage, source);
            }
        }
        if (damage <= 0f)
        {
            return;
        }

        EnemySpecialistTrait specialist = GetComponent<EnemySpecialistTrait>();
        if (specialist != null)
        {
            damage = specialist.ModifyIncomingDamage(damage, source, directHit);
        }

        if (source == CombatDamageSource.Poison
            && EnemySpecialistTrait.HasNearbyTinnik(this))
        {
            damage *= 0.5f;
        }

        float unboostedDamage = damage;
        float frogMultiplier = FrogRider.DamageMultiplier(levelGenerator);
        damage *= frogMultiplier;
        float healthBeforeHit = Mathf.Max(0f, hp);
        hp = Mathf.Max(0f, hp - damage);
        float healthLost = healthBeforeHit - hp;
        if (healthLost > 0f && (source == CombatDamageSource.Fire || source == CombatDamageSource.Firefly))
        {
            GetComponent<WinterEnemyForm>()?.OnFireDamage();
        }
        // Credit only extra HP actually removed by the chorus, including a
        // partial bonus on lethal hits. Excess damage beyond remaining HP is ignored.
        float frogSupportDamage = frogMultiplier > 1f ? Mathf.Max(0f, healthLost - unboostedDamage) : 0f;
        ProjectileDamageStats.Record(healthLost, source, damageOwner, frogSupportDamage);
        combatText = combatText != null ? combatText : EnemyCombatTextUI.Ensure();
        if (afterTurnEffect)
        {
            combatText.QueueAfterTurnDamage(this, damage, source);
        }
        else
        {
            combatText.ShowDamage(this, damage, source);
        }

        UpdateHealthUI();
        ArtifactCombatEffects.OnDamage(this, healthLost, source, artifactOwner, afterTurnEffect || !directHit);
        // A repeat hit may have already completed defeat during the artifact callback.
        if (IsDefeated || (zombieResurrection != null && zombieResurrection.IsAwaitingResurrection && hp > 0f))
        {
            return;
        }
        // Release every rider before defeat handlers remove the enemy's room
        // reference or turn its artwork into a grave/split form.
        if (hp <= 0f)
        {
            SquirrelAcorn.NotifyEnemyDefeated(this);
            FrogRider.NotifyEnemyDefeated(this, afterTurnEffect ? 0 : comboVolleyId);
        }
        if (zombieResurrection != null && zombieResurrection.IsAwaitingResurrection)
        {
            if (hp <= 0f)
            {
                TrySpawnFireflyBonfire(source);
                zombieResurrection.BreakGrave();
                CompleteDefeat();
            }
            return;
        }
        if (hp > 0f && source != CombatDamageSource.Frog && source != CombatDamageSource.Fire && source != CombatDamageSource.Poison)
        {
            FrogRider.NotifyEnemyDamaged(this, afterTurnEffect ? 0 : comboVolleyId);
        }

        hitReaction?.Play();
        if (hp > 0f)
        {
            SetAnimatorTrigger(hitAnimationTrigger);
            return;
        }

        defeated = true;
        SetAnimatorTrigger(deathAnimationTrigger);
        TrySpawnFireflyBonfire(source);
        if (TryHandleSpecialDefeat())
        {
            PrepareTemporaryDefeat();
            return;
        }
        CompleteDefeat();
    }

    private void CompleteDefeat()
    {
        EnemySpecialistTrait.NotifyEnemyDefeated(this);
        GameManager.Instance?.RegisterEnemyDefeated(this, experienceReward);
        if (levelGenerator != null)
        {
            levelGenerator.DestroyEnemy(gameObject, true);
        }
        else
        {
            GameManager.Instance?.Front?.RemoveTravellingEnemy(this);
        }

        levelGenerator = null;
        Destroy(gameObject);
    }

    private bool TryHandleSpecialDefeat()
    {
        defeatBehaviours.Clear();
        GetComponents(defeatBehaviours);
        for (int i = 0; i < defeatBehaviours.Count; i++)
        {
            if (defeatBehaviours[i] is IEnemyDefeatHandler handler && handler.TryHandleDefeat(this))
            {
                return true;
            }
        }

        return false;
    }

    private void PrepareTemporaryDefeat()
    {
        if (stepAnimation != null)
        {
            StopCoroutine(stepAnimation);
        }

        stepAnimation = null;
        IsStepping = false;
        if (restingLocalScale.sqrMagnitude > 0f)
        {
            transform.localScale = restingLocalScale;
        }

        if (zombieResurrection != null && zombieResurrection.IsAwaitingResurrection)
        {
            combatText?.Register(this, hp);
            if (hpbar != null)
            {
                hpbar.gameObject.SetActive(true);
            }

            UpdateHealthUI();
        }
        else
        {
            combatText?.Unregister(this);
            if (hpbar != null)
            {
                hpbar.gameObject.SetActive(false);
            }
        }
        GetComponent<EnemyStatusEffects>()?.ClearAll();
        GetComponent<EnemyIllumination>()?.Extinguish();
        GetComponent<EnemyBurnTint>()?.Refresh();
    }

    public bool Revive(float healthFraction)
    {
        if (!defeated || levelGenerator == null || levelGenerator.IsLost)
        {
            return false;
        }

        defeated = false;
        hp = Mathf.Max(1f, maxHp * Mathf.Clamp(healthFraction, 0.05f, 1f));
        if (hpbar != null)
        {
            hpbar.gameObject.SetActive(true);
        }

        combatText = combatText != null ? combatText : EnemyCombatTextUI.Ensure();
        combatText.Register(this, hp);
        UpdateHealthUI();
        levelGenerator.NotifyEnemyStateChanged();
        return true;
    }

    public bool OccupiesFootprintOffset(Vector2Int offset)
    {
        if (!IsOffsetInsideFootprint(offset))
        {
            return false;
        }

        if (!HasCustomFootprint)
        {
            return true;
        }

        return occupiedCellOffsets.Contains(offset);
    }

    private bool IsOffsetInsideFootprint(Vector2Int offset)
    {
        Vector2Int size = Footprint;
        return offset.x >= 0 && offset.y >= 0 && offset.x < size.x && offset.y < size.y;
    }

    private void TrySpawnFireflyBonfire(CombatDamageSource lethalSource)
    {
        if (!leaveFireflyBonfire || levelGenerator == null || levelGenerator.IsLost
            || !gameObject.activeInHierarchy)
        {
            return;
        }

        EnemyStatusEffects statuses = GetComponent<EnemyStatusEffects>();
        int stacks = statuses != null
            ? statuses.GetStackCount(FireflyImpactEffect.BurnEffectId) : 0;
        if (stacks <= 0 && lethalSource != CombatDamageSource.Firefly)
        {
            return;
        }

        EnemyIllumination light = GetComponent<EnemyIllumination>();
        float inheritedRadius = light != null && light.IsIlluminated
            ? light.LightRadius : Mathf.Max(1, stacks);
        float radius = Mathf.Max(0.1f, inheritedRadius * bonfireRadiusMultiplier);
        Vector2Int size = Footprint;
        Vector3 topLeft = transform.position
            + new Vector3(-(size.x - 1) * 0.5f, (size.y - 1) * 0.5f, 0f);
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
        {
            // Bosses reserve their whole rectangle, matching EnemyRoomGrid.
            if (!UsesBossHealth && !OccupiesFootprintOffset(new Vector2Int(x, y)))
                {
                    continue;
                }

                FireflyBonfire.Spawn(levelGenerator, topLeft + new Vector3(x, -y, 0f),
                Mathf.Max(0.1f, fireflyBonfireDamage * bonfireDamageMultiplier),
                radius, radius, Mathf.Max(1, bonfireDurationTurns));
        }
        }
    }
    public void Step()
    {
        if (defeated || IsStepping || levelGenerator == null || levelGenerator.IsLost || IsStationary)
        {
            return;
        }
        // Also resolve enemies already on the breach row (for example after loading a save).
        if (LowestCellY <= levelGenerator.DefenseBoundaryY + 0.0001f)
        {
            levelGenerator.RequestBreach(this);
            return;
        }
        EnemySpecialistBuffs movementBuffs = GetComponent<EnemySpecialistBuffs>();
        int extraSteps = movementBuffs != null ? movementBuffs.ConsumeExtraStep() : 0;
        EnemyTurnStun stun = GetComponent<EnemyTurnStun>();
        if (stun != null && stun.TryConsumeEnemyTurn())
        {
            return;
        }

        if (bossTurnController == null)
        {
            bossTurnController = GetComponent<BossEnemyTurnController>();
        }

        int requestedSteps = bossTurnController != null
            ? bossTurnController.MovementCellsPerTurn : 1;
        requestedSteps += extraSteps;
        float lastCellY = levelGenerator.transform.position.y - levelGenerator.Rows + 1f;
        int allowedSteps = 0;
        bool blockedByEnemy = false;
        for (int step = 1; step <= requestedSteps; step++)
        {
            Vector3 next = transform.position + Vector3.down * step;
            float nextLowestCellY = LowestCellY - step;
            if (nextLowestCellY < lastCellY - 0.0001f)
            {
                break;
            }

            if (levelGenerator.IsMovementBlockedByEnemy(this, next,
                    bossTurnController != null))
            {
                blockedByEnemy = true;
                break;
            }
            allowedSteps = step;
        }
        // Keep multi-cell boss moves atomic unless the final legal step reaches
        // the breach row or an enemy blocks the rest of the movement.
        bool reachesBoundary = allowedSteps > 0
            && LowestCellY - allowedSteps <= levelGenerator.DefenseBoundaryY + 0.0001f;
        if (bossTurnController != null && allowedSteps < requestedSteps
            && !reachesBoundary && !blockedByEnemy)
        {
            allowedSteps = 0;
        }

        if (allowedSteps <= 0)
        {
            return;
        }

        Vector3 destination = transform.position + Vector3.down * allowedSteps;
        if (!Application.isPlaying || stepDuration <= 0f || IsInUnwatchedRoom)
        {
            transform.position = destination;
            CompleteStep();
            return;
        }

        stepAnimation = StartCoroutine(AnimateSteps(allowedSteps));
    }

    // Rooms the camera is not showing resolve enemy moves instantly, so the
    // enemy phase only waits for the animations the player can actually see.
    private bool IsInUnwatchedRoom
    {
        get
        {
            DefenseLineRuntime front = GameManager.Instance != null ? GameManager.Instance.Front : null;
            return front != null && levelGenerator != null && front.CurrentRoomId >= 0
                && levelGenerator.RoomId != front.CurrentRoomId;
        }
    }

    public void PlayArrivalAnimation()
    {
        if (!Application.isPlaying || defeated)
        {
            return;
        }

        if (arrivalDuration <= 0f || IsInUnwatchedRoom)
        {
            if (visualTransition != null)
            {
                StopCoroutine(visualTransition);
            }

            visualTransition = null;
            RestoreVisualTransition();
            return;
        }
        StartVisualTransition(false, false, arrivalDuration);
    }

    public void PlayBreachAnimation(bool isAttacker)
    {
        if (!Application.isPlaying || defeated || breachDuration <= 0f || IsInUnwatchedRoom)
        {
            return;
        }

        StartVisualTransition(true, isAttacker, breachDuration);
    }

    public void CancelBreachAnimation()
    {
        if (visualTransition != null)
        {
            StopCoroutine(visualTransition);
        }

        visualTransition = null;
        RestoreVisualTransition();
    }

    private void StartVisualTransition(bool breaching, bool isAttacker, float duration)
    {
        if (visualTransition != null)
        {
            StopCoroutine(visualTransition);
        }

        RestoreVisualTransition();
        transitionVisual = transform.Find("Visual");
        if (transitionVisual != null)
        {
            transitionRestScale = transitionVisual.localScale;
        }

        transitionSprites = GetComponentsInChildren<SpriteRenderer>(true);
        transitionBaseAlphas = new float[transitionSprites.Length];
        for (int i = 0; i < transitionSprites.Length; i++)
        {
            transitionBaseAlphas[i] = transitionSprites[i].color.a;
        }

        visualTransition = StartCoroutine(AnimateVisualTransition(breaching, isAttacker, duration));
    }

    private IEnumerator AnimateVisualTransition(bool breaching, bool isAttacker, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !defeated)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            if (breaching)
            {
                float retreatStart = isAttacker ? 0.3f : 0f;
                float retreat = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((progress - retreatStart) / (1f - retreatStart)));
                float lunge = isAttacker
                    ? Mathf.Sin(Mathf.Clamp01(progress / 0.35f) * Mathf.PI) * 0.2f : 0f;
                ApplyVisualTransition(1f + lunge - retreat * 0.55f, 1f - retreat);
            }
            else
            {
                float scale = progress < 0.72f
                    ? Mathf.Lerp(1f, 1.08f, Mathf.SmoothStep(0f, 1f, progress / 0.72f))
                    : Mathf.Lerp(1.08f, 1f,
                        Mathf.SmoothStep(0f, 1f, (progress - 0.72f) / 0.28f));
                // Keep a newly spawned head visible from the first frame.
                ApplyVisualTransition(scale, Mathf.Lerp(0.3f, 1f,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.7f))));
            }
            yield return null;
            // The visual arrival must finish even if a discovery card pauses the game.
            elapsed += breaching ? Time.deltaTime : Time.unscaledDeltaTime;
        }
        if (breaching && !defeated)
        {
            ApplyVisualTransition(0.45f, 0f);
        }
        else
        {
            RestoreVisualTransition();
        }

        visualTransition = null;
    }

    private void ApplyVisualTransition(float scale, float alpha)
    {
        if (transitionVisual != null)
        {
            transitionVisual.localScale = transitionRestScale * scale;
        }

        for (int i = 0; i < transitionSprites.Length; i++)
        {
            SpriteRenderer sprite = transitionSprites[i];
            if (sprite == null)
            {
                continue;
            }

            Color color = sprite.color;
            color.a = transitionBaseAlphas[i] * alpha;
            sprite.color = color;
        }
    }

    private void RestoreVisualTransition()
    {
        if (transitionVisual != null)
        {
            transitionVisual.localScale = transitionRestScale;
        }

        if (transitionSprites != null && transitionBaseAlphas != null)
        {
            for (int i = 0; i < transitionSprites.Length; i++)
            {
                SpriteRenderer sprite = transitionSprites[i];
                if (sprite == null)
                {
                    continue;
                }

                Color color = sprite.color;
                color.a = transitionBaseAlphas[i];
                sprite.color = color;
            }
        }

        transitionVisual = null;
        transitionSprites = null;
        transitionBaseAlphas = null;
    }

    private IEnumerator AnimateSteps(int stepCount)
    {
        IsStepping = true;
        Vector3 baseScale = transform.localScale;
        restingLocalScale = baseScale;
        for (int step = 0; step < stepCount && !defeated
            && levelGenerator != null && !levelGenerator.IsLost; step++)
        {
            SetAnimatorTrigger(stepAnimationTrigger);
            Vector3 start = transform.position;
            Vector3 destination = start + Vector3.down;
            StepStartPosition = start;
            StepTargetPosition = destination;
            float elapsed = 0f;
            while (elapsed < stepDuration && !defeated
                && levelGenerator != null && !levelGenerator.IsLost)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / stepDuration);
                float eased = progress * progress * (3f - 2f * progress);
                float arc = Mathf.Sin(progress * Mathf.PI);
                float landing = progress > 0.72f
                    ? Mathf.Sin((progress - 0.72f) / 0.28f * Mathf.PI)
                    : 0f;

                transform.position = Vector3.LerpUnclamped(start, destination, eased)
                    + Vector3.up * (arc * stepJumpHeight);
                levelGenerator?.InvalidateEnemyQueries();
                transform.localScale = new Vector3(
                    baseScale.x * (1f - arc * stepStretch * 0.45f + landing * landingSquash),
                    baseScale.y * (1f + arc * stepStretch - landing * landingSquash),
                    baseScale.z);
                yield return null;
            }
            if (!defeated && levelGenerator != null && !levelGenerator.IsLost)
            {
                transform.position = destination;
                transform.localScale = baseScale;
            }
        }
        IsStepping = false;
        stepAnimation = null;
        CompleteStep();
    }

    private void CompleteStep()
    {
        levelGenerator?.InvalidateEnemyQueries();
        if (defeated || levelGenerator == null || levelGenerator.IsLost)
        {
            return;
        }

        float defenseBoundary = levelGenerator.DefenseBoundaryY;
        if (LowestCellY <= defenseBoundary + 0.0001f)
        {
            levelGenerator.RequestBreach(this);
        }
    }

    public float StrikeBarricadeAndReturn(Vector3 destination)
    {
        if (defeated)
        {
            return 0f;
        }

        float contactDamage = DefenseLineRuntime.BarricadeDamageOf(this);
        if (stepAnimation != null)
        {
            StopCoroutine(stepAnimation);
        }

        stepAnimation = null;
        IsStepping = false;
        if (restingLocalScale.sqrMagnitude > 0f)
        {
            transform.localScale = restingLocalScale;
        }

        transform.position = destination;
        PlayArrivalAnimation();
        levelGenerator?.NotifyEnemyStateChanged();
        return contactDamage;
    }

    private void SetAnimatorTrigger(string triggerName)
    {
        if (spriteAnimator == null || spriteAnimator.runtimeAnimatorController == null
            || string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        int triggerHash = Animator.StringToHash(triggerName);
        foreach (AnimatorControllerParameter parameter in spriteAnimator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Trigger
                || parameter.nameHash != triggerHash)
            {
                continue;
            }

            spriteAnimator.SetTrigger(triggerHash);
            return;
        }
    }
    private void OnDisable()
    {
        if (visualTransition != null)
        {
            StopCoroutine(visualTransition);
        }

        visualTransition = null;
        RestoreVisualTransition();
        if (stepAnimation != null)
        {
            StopCoroutine(stepAnimation);
        }

        stepAnimation = null;
        IsStepping = false;
        if (restingLocalScale.sqrMagnitude > 0f)
        {
            transform.localScale = restingLocalScale;
        }

        if (levelGenerator != null)
        {
            levelGenerator.DestroyEnemy(gameObject, defeated);
        }
    }
    private void UpdateHealthUI()
    {
        if (hpbar != null)
        {
            hpbar.fillAmount = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
        }

        combatText?.SetHealth(this, hp);
    }

    private void OnDestroy()
    {
        if (combatText != null)
        {
            combatText.Unregister(this);
        }
    }
}
