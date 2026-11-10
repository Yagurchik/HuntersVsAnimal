using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class AnimalProjectileQueue : MonoBehaviour
{
    public enum UpgradeKind
    {
        Ammo = 0,
        RepeatFire = 1,
        Burn = 2,
        HedgehogRadius = 3,
        HedgehogQuillDamage = 4,
        HedgehogUltimate = 5,
        HoneyBadgerRadius = 6,
        HoneyBadgerChain = 7,
        HoneyBadgerUltimate = 8,
        BoarBounces = 9,
        BoarFirstHit = 10,
        BoarUltimate = 11,
        GlobalDamage = 12, // Reserved legacy ID; no longer offered or accepted.
        FireflyUltimate = 13,
        RabbitPackStrength = 14,
        RabbitBreeding = 15,
        RabbitUltimate = 16,
        BeaverDefenseHealth = 17,
        BeaverDamage = 18,
        BeaverUltimate = 19,
        FrogRadius = 20,
        FrogChorus = 21,
        FrogUltimate = 22,
        SquirrelDetonation = 23,
        SquirrelBinding = 24,
        SquirrelUltimate = 25,
        RabbitFirstBreeding = 26
    }

    public sealed class UpgradeOption
    {
        public int AnimalIndex { get; }
        public UpgradeKind Kind { get; }
        public string Title { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public Color IconColor { get; }

        public UpgradeOption(int animalIndex, UpgradeKind kind, string title,
            string description, Sprite icon, Color iconColor)
        {
            AnimalIndex = animalIndex;
            Kind = kind;
            Title = title;
            Description = description;
            Icon = icon;
            IconColor = iconColor;
        }
    }

    [Serializable]
    public class AnimalProjectileEntry
    {
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;
        [SerializeField] private bool unlocked = true;
        [SerializeField, Min(1)] private int ammoPerTurn = 1;
        [SerializeField] private bool areaExplosion;
        [SerializeField, Min(0.1f)] private float explosionRadius = 1.2f;
        [SerializeField, Min(0.05f)] private float explosionDuration = 0.18f;
        [SerializeField, Range(0f, 1f), Tooltip("Area damage as a part of the projectile's direct damage.")]
        private float explosionDamageMultiplier = 0.5f;
        [SerializeField] private bool replicatesOnEnemyHits;
        [SerializeField, Min(1)] private int enemyHitsToReplicate = 5;

        [NonSerialized] private int remainingAmmo;
        [NonSerialized] private int remainingVolleys;
        [NonSerialized] private int upgradeLevel;
        [NonSerialized] private int ammoUpgradeLevel;
        [NonSerialized] private int repeatFireUpgradeLevel;
        [NonSerialized] private int burnUpgradeLevel;
        [NonSerialized] private bool fireflyUltimate;
        [NonSerialized] private int rabbitPackStrengthUpgradeLevel;
        [NonSerialized] private int rabbitBreedingUpgradeLevel;
        [NonSerialized] private int rabbitFirstBreedingUpgradeLevel;
        [NonSerialized] private bool rabbitUltimate;
        [NonSerialized] private int hedgehogRadiusUpgradeLevel;
        [NonSerialized] private int hedgehogQuillDamageUpgradeLevel;
        [NonSerialized] private bool hedgehogUltimate;
        [NonSerialized] private int honeyBadgerRadiusUpgradeLevel;
        [NonSerialized] private int honeyBadgerChainUpgradeLevel;
        [NonSerialized] private bool honeyBadgerUltimate;
        [NonSerialized] private int boarBouncesUpgradeLevel;
        [NonSerialized] private int boarFirstHitUpgradeLevel;
        [NonSerialized] private bool boarUltimate;
        [NonSerialized] private int beaverDefenseHealthUpgradeLevel;
        [NonSerialized] private int beaverDamageUpgradeLevel;
        [NonSerialized] private bool beaverUltimate;
        [NonSerialized] private int frogRadiusUpgradeLevel;
        [NonSerialized] private int frogChorusUpgradeLevel;
        [NonSerialized] private bool frogUltimate;
        [NonSerialized] private int squirrelDetonationUpgradeLevel;
        [NonSerialized] private int squirrelBindingUpgradeLevel;
        [NonSerialized] private bool squirrelUltimate;
        [NonSerialized] private bool runtimeInitialized;
        [NonSerialized] private int startingAmmoOverride;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) && prefab != null
            ? prefab.name
            : displayName;

        public GameObject Prefab => prefab;
        public bool IsConfigured => prefab != null;
        public bool IsUnlocked => unlocked;
        public bool IsAvailable => unlocked && prefab != null;
        public bool HasCustomIcon => icon != null;
        public int AmmoPerTurn
        {
            get
            {
                EnsureRuntimeInitialized();
                return Mathf.Max(1, BaseAmmoPerTurn + Mathf.Max(0, ammoUpgradeLevel - 1));
            }
        }
        public int BaseAmmoPerTurn => startingAmmoOverride > 0 ? startingAmmoOverride : Mathf.Max(1, ammoPerTurn);
        public int RemainingAmmo => remainingAmmo;
        public int RemainingVolleys => remainingVolleys;
        public int UpgradeLevel => upgradeLevel;
        public int AmmoUpgradeLevel
        {
            get { EnsureRuntimeInitialized(); return ammoUpgradeLevel; }
        }
        public int RepeatFireUpgradeLevel => repeatFireUpgradeLevel;
        public int BurnUpgradeLevel => burnUpgradeLevel;
        public bool HasFireflyUltimate => fireflyUltimate;
        public int RabbitPackStrengthUpgradeLevel => rabbitPackStrengthUpgradeLevel;
        public int RabbitBreedingUpgradeLevel => rabbitBreedingUpgradeLevel;
        public int RabbitFirstBreedingUpgradeLevel => rabbitFirstBreedingUpgradeLevel;
        public bool HasRabbitUltimate => rabbitUltimate;
        public int HedgehogRadiusUpgradeLevel => hedgehogRadiusUpgradeLevel;
        public int HedgehogQuillDamageUpgradeLevel => hedgehogQuillDamageUpgradeLevel;
        public bool HasHedgehogUltimate => hedgehogUltimate;
        public int HoneyBadgerRadiusUpgradeLevel => honeyBadgerRadiusUpgradeLevel;
        public int HoneyBadgerChainUpgradeLevel => honeyBadgerChainUpgradeLevel;
        public bool HasHoneyBadgerUltimate => honeyBadgerUltimate;
        public int BoarBouncesUpgradeLevel => boarBouncesUpgradeLevel;
        public int BoarFirstHitUpgradeLevel => boarFirstHitUpgradeLevel;
        public bool HasBoarUltimate => boarUltimate;
        public int BeaverDefenseHealthUpgradeLevel => beaverDefenseHealthUpgradeLevel;
        public int BeaverDamageUpgradeLevel => beaverDamageUpgradeLevel;
        public bool HasBeaverUltimate => beaverUltimate;
        public int FrogRadiusUpgradeLevel => frogRadiusUpgradeLevel;
        public int FrogChorusUpgradeLevel => frogChorusUpgradeLevel;
        public bool HasFrogUltimate => frogUltimate;
        public int SquirrelDetonationUpgradeLevel => squirrelDetonationUpgradeLevel;
        public int SquirrelBindingUpgradeLevel => squirrelBindingUpgradeLevel;
        public bool HasSquirrelUltimate => squirrelUltimate;
        public bool HasAmmo => IsAvailable && remainingAmmo > 0;
        public bool IsAreaExplosion => areaExplosion;
        public float ExplosionRadius => Mathf.Max(0.1f, explosionRadius);
        public float ExplosionDuration => Mathf.Max(0.05f, explosionDuration);
        public float ExplosionDamageMultiplier => Mathf.Clamp01(explosionDamageMultiplier);
        public bool ReplicatesOnEnemyHits => replicatesOnEnemyHits;
        public int EnemyHitsToReplicate => Mathf.Max(1, enemyHitsToReplicate);

        public Sprite GetIcon()
        {
            Sprite skin = ProjectileSkins.EquippedArtwork(prefab);
            if (skin != null) return skin;
            if (icon != null)
            {
                return icon;
            }

            SpriteRenderer spriteRenderer = prefab != null
                ? prefab.GetComponentInChildren<SpriteRenderer>(true)
                : null;

            return spriteRenderer != null ? spriteRenderer.sprite : null;
        }

        public Color GetIconColor()
        {
            if (ProjectileSkins.EquippedArtwork(prefab) != null) return Color.white;
            // Dedicated HUD artwork already contains the intended palette. Prefab
            // tinting is only useful for the fallback combat sprite.
            if (icon != null)
            {
                return Color.white;
            }

            SpriteRenderer spriteRenderer = prefab != null
                ? prefab.GetComponentInChildren<SpriteRenderer>(true)
                : null;

            if (spriteRenderer == null)
            {
                return Color.white;
            }

            Color color = spriteRenderer.color;
            color.a = 1f;
            return color;
        }

        public void SetIcon(Sprite value)
        {
            icon = value;
        }

        public void SetStartingAmmo(int amount) => startingAmmoOverride = Mathf.Max(0, amount);

        public void Unlock()
        {
            EnsureRuntimeInitialized();
            unlocked = true;
            ammoUpgradeLevel = Mathf.Max(1, ammoUpgradeLevel);
        }

        public void LockForRun()
        {
            unlocked = false;
            // A newly configured run must not inherit upgrades from an earlier loadout.
            runtimeInitialized = false;
            EnsureRuntimeInitialized();
            startingAmmoOverride = 0;
            ClearAllAmmo();
        }

        public void Reload(int volleysPerTurn = 1)
        {
            remainingVolleys = IsAvailable ? Mathf.Max(1, volleysPerTurn) : 0;
            remainingAmmo = remainingVolleys > 0 ? AmmoPerTurn : 0;
        }

        public bool TryConsume()
        {
            if (!HasAmmo)
            {
                return false;
            }

            remainingAmmo--;
            return true;
        }

        public bool CompleteVolley()
        {
            if (remainingVolleys <= 0)
            {
                remainingAmmo = 0;
                return false;
            }

            remainingVolleys--;
            remainingAmmo = remainingVolleys > 0 ? AmmoPerTurn : 0;
            return remainingVolleys > 0;
        }

        public void ClearCurrentVolleyAmmo()
        {
            remainingAmmo = 0;
        }

        public void ClearAllAmmo()
        {
            remainingAmmo = 0;
            remainingVolleys = 0;
        }

        public bool UpgradeAmmo(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsConfigured || ammoUpgradeLevel >= maximumLevel) return false;

            if (!unlocked) unlocked = true;
            ammoUpgradeLevel = Mathf.Max(1, ammoUpgradeLevel + 1);
            upgradeLevel++;
            return true;
        }

        public bool UpgradeRepeatFire(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || repeatFireUpgradeLevel >= maximumLevel) return false;
            repeatFireUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeBurn(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || burnUpgradeLevel >= maximumLevel) return false;
            burnUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeFireflyUltimate()
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || fireflyUltimate) return false;
            fireflyUltimate = true;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeRabbitPackStrength(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || rabbitPackStrengthUpgradeLevel >= maximumLevel) return false;
            rabbitPackStrengthUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeRabbitBreeding(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || rabbitBreedingUpgradeLevel >= maximumLevel) return false;
            rabbitBreedingUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeRabbitFirstBreeding(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || rabbitFirstBreedingUpgradeLevel >= maximumLevel) return false;
            rabbitFirstBreedingUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeRabbitUltimate()
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || rabbitUltimate) return false;
            rabbitUltimate = true;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeHoneyBadgerRadius(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || honeyBadgerRadiusUpgradeLevel >= maximumLevel) return false;
            honeyBadgerRadiusUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeHedgehogRadius(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || hedgehogRadiusUpgradeLevel >= maximumLevel) return false;
            hedgehogRadiusUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeHedgehogQuillDamage(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || hedgehogQuillDamageUpgradeLevel >= maximumLevel) return false;
            hedgehogQuillDamageUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeHedgehogUltimate()
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || hedgehogUltimate) return false;
            hedgehogUltimate = true;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeHoneyBadgerChain(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || honeyBadgerChainUpgradeLevel >= maximumLevel) return false;
            honeyBadgerChainUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeHoneyBadgerUltimate()
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || honeyBadgerUltimate) return false;
            honeyBadgerUltimate = true;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeBoarBounces(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || boarBouncesUpgradeLevel >= maximumLevel) return false;
            boarBouncesUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeBoarFirstHit(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || boarFirstHitUpgradeLevel >= maximumLevel) return false;
            boarFirstHitUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeBoarUltimate()
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || boarUltimate) return false;
            boarUltimate = true;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeBeaverDefenseHealth(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || beaverDefenseHealthUpgradeLevel >= maximumLevel) return false;
            beaverDefenseHealthUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeBeaverDamage(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || beaverDamageUpgradeLevel >= maximumLevel) return false;
            beaverDamageUpgradeLevel++;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeBeaverUltimate()
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || beaverUltimate) return false;
            beaverUltimate = true;
            upgradeLevel++;
            return true;
        }

        public bool UpgradeFrogRadius(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || frogRadiusUpgradeLevel >= maximumLevel) return false;
            frogRadiusUpgradeLevel++; upgradeLevel++; return true;
        }

        public bool UpgradeFrogChorus(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || frogChorusUpgradeLevel >= maximumLevel) return false;
            frogChorusUpgradeLevel++; upgradeLevel++; return true;
        }

        public bool UpgradeFrogUltimate()
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || frogUltimate) return false;
            frogUltimate = true; upgradeLevel++; return true;
        }

        public bool UpgradeSquirrelDetonation(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || squirrelDetonationUpgradeLevel >= maximumLevel) return false;
            squirrelDetonationUpgradeLevel++; upgradeLevel++; return true;
        }

        public bool UpgradeSquirrelBinding(int maximumLevel)
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || squirrelBindingUpgradeLevel >= maximumLevel) return false;
            squirrelBindingUpgradeLevel++; upgradeLevel++; return true;
        }

        public bool UpgradeSquirrelUltimate()
        {
            EnsureRuntimeInitialized();
            if (!IsAvailable || squirrelUltimate) return false;
            squirrelUltimate = true; upgradeLevel++; return true;
        }

        private void EnsureRuntimeInitialized()
        {
            if (runtimeInitialized) return;
            runtimeInitialized = true;
            ammoUpgradeLevel = unlocked ? 1 : 0;
            repeatFireUpgradeLevel = 0;
            burnUpgradeLevel = 0;
            fireflyUltimate = false;
            rabbitPackStrengthUpgradeLevel = 0;
            rabbitBreedingUpgradeLevel = 0;
            rabbitFirstBreedingUpgradeLevel = 0;
            rabbitUltimate = false;
            hedgehogRadiusUpgradeLevel = 0;
            hedgehogQuillDamageUpgradeLevel = 0;
            hedgehogUltimate = false;
            honeyBadgerRadiusUpgradeLevel = 0;
            honeyBadgerChainUpgradeLevel = 0;
            honeyBadgerUltimate = false;
            boarBouncesUpgradeLevel = 0;
            boarFirstHitUpgradeLevel = 0;
            boarUltimate = false;
            beaverDefenseHealthUpgradeLevel = 0;
            beaverDamageUpgradeLevel = 0;
            beaverUltimate = false;
            frogRadiusUpgradeLevel = 0;
            frogChorusUpgradeLevel = 0;
            frogUltimate = false;
            squirrelDetonationUpgradeLevel = 0;
            squirrelBindingUpgradeLevel = 0;
            squirrelUltimate = false;
            upgradeLevel = 0;
        }
    }

    public const int MaximumBranchLevel = 3;
    public const int FireflyIndex = 0;
    public const int HedgehogIndex = 1;
    public const int RabbitIndex = 2;
    public const int HoneyBadgerIndex = 3;
    public const int BoarIndex = 4;
    public const int BeaverIndex = 5;
    public const int FrogIndex = 6;
    public const int SquirrelIndex = 7;
    public const int AnimalTypeCount = SquirrelIndex + 1;

    public static AnimalProjectileQueue Instance { get; private set; }

    [Header("Animals")]
    [SerializeField] private List<AnimalProjectileEntry> animals = new List<AnimalProjectileEntry>();
    [SerializeField] private int selectedIndex;

    [Header("Run upgrades")]
    [FormerlySerializedAs("fireflyRepeatShotsLevel1")]
    [SerializeField, Range(1, 5), Tooltip("Manual firefly volleys available during one player turn.")]
    private int fireflyManualVolleysLevel1 = 2;
    [FormerlySerializedAs("fireflyRepeatShotsLevel2")]
    [SerializeField, Range(1, 5), Tooltip("Manual firefly volleys available during one player turn.")]
    private int fireflyManualVolleysLevel2 = 3;
    [FormerlySerializedAs("fireflyRepeatShotsLevel3")]
    [SerializeField, Range(1, 5), Tooltip("Manual firefly volleys available during one player turn.")]
    private int fireflyManualVolleysLevel3 = 3;
    [SerializeField, Range(0f, 1f)] private float fireflyRepeatDamageLevel1 = 0.5f;
    [SerializeField, Range(0f, 1f)] private float fireflyRepeatDamageLevel2 = 0.33f;
    [SerializeField, Range(0f, 1f)] private float fireflyRepeatDamageLevel3 = 0.5f;
    [SerializeField, Min(0f), Tooltip("Additional flat damage dealt by each firefly burn upgrade.")]
    private float fireflyBurnDamagePerUpgrade = 25f;

    [Header("Wheel")]
    [SerializeField] private bool createWheelOnStart = true;
    [SerializeField] private float wheelRadius = 135f;
    [SerializeField] private float wheelAngleStep = 48f;
    [SerializeField] private float wheelVerticalOffset = 180f;

    public event Action<int> SelectionChanged;
    public event Action AmmoChanged;
    public event Action UpgradesChanged;
    public event Action<bool> AllVolleyModeChanged;

    private AnimalSelectionWheel selectionWheel;
    private int selectedUpgradeCount;
    private int maximumAllowedAnimalIndex = int.MaxValue;
    private bool[] selectedForRun;
    private static int[] preparedRunLoadout;
    private static int preparedStartingAnimal = -1;
    private bool hasForcedUpgradeOption;
    private int forcedUpgradeAnimalIndex = -1;
    private UpgradeKind forcedUpgradeKind;
    private bool allVolleyEnabled;

    public IReadOnlyList<AnimalProjectileEntry> Animals => animals;
    public int SelectedIndex => selectedIndex;
    public GameObject SelectedProjectile => HasAmmo(selectedIndex) ? animals[selectedIndex].Prefab : null;
    public bool HasRemainingAmmo => TotalRemainingAmmo > 0;
    public int TotalRemainingAmmo
    {
        get
        {
            int total = 0;
            for (int i = 0; i < animals.Count; i++)
            {
                if (IsAnimalAllowedForRun(i)) total += animals[i].RemainingAmmo;
            }

            return total;
        }
    }
    public float WheelRadius => wheelRadius;
    public float WheelAngleStep => wheelAngleStep;
    public float WheelVerticalOffset => wheelVerticalOffset;
    public int SelectedUpgradeCount => selectedUpgradeCount;
    public int MaximumAllowedAnimalIndex => animals.Count == 0
        ? -1 : Mathf.Min(maximumAllowedAnimalIndex, animals.Count - 1);
    public bool HasForcedUpgradeOption => hasForcedUpgradeOption;
    public bool AllVolleyEnabled => allVolleyEnabled;
    public bool HasRunLoadout => selectedForRun != null;
    public int StartingAnimalIndex { get; private set; } = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        int location = CurrentStartingLocation;
        if (preparedRunLoadout != null)
        {
            int starter = StartingAnimalForLocation(location);
            if (Array.IndexOf(preparedRunLoadout, starter) < 0)
            {
                int replace = Array.IndexOf(preparedRunLoadout, preparedStartingAnimal);
                if (replace < 0 || preparedRunLoadout[replace] == FireflyIndex) replace = preparedRunLoadout.Length - 1;
                preparedRunLoadout[replace] = starter;
            }
            preparedStartingAnimal = starter;
        }
        if (preparedRunLoadout == null || !ConfigureRunLoadout(preparedRunLoadout, location))
        {
            ConfigureStartingProjectiles(location);
            ReloadAll();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPreparedRunLoadout() { preparedRunLoadout = null; preparedStartingAnimal = -1; }

    private static int CurrentStartingLocation => LevelProgression.Instance != null
        ? LevelProgression.Instance.CurrentLocationIndex : LevelProgression.RequestedLocationIndex;

    public static int StartingAnimalForLocation(int locationIndex)
    {
        switch (locationIndex)
        {
            case 2: return RabbitIndex;
            case 3: return BeaverIndex;
            case 4: return FrogIndex;
            case 5: return SquirrelIndex;
            case 6: return BoarIndex;
            case 7: return HoneyBadgerIndex;
            default: return HedgehogIndex;
        }
    }

    public static int StartingAmmoForAnimal(int animalIndex)
    {
        if (animalIndex == FireflyIndex) return 5;
        if (animalIndex == RabbitIndex) return 2;
        return MaximumBranchLevel;
    }

    public static bool PrepareRunLoadout(IReadOnlyList<int> selected, int locationIndex = 0)
    {
        if (selected == null || selected.Count != 5) return false;
        bool[] chosen = new bool[AnimalTypeCount];
        int[] prepared = new int[selected.Count];
        for (int i = 0; i < selected.Count; i++)
        {
            int index = selected[i];
            if (index < 0 || index >= chosen.Length || chosen[index]) return false;
            chosen[index] = true;
            prepared[i] = index;
        }
        int starter = StartingAnimalForLocation(locationIndex > 0 ? locationIndex : LevelProgression.SelectedMenuLocation);
        if (!chosen[FireflyIndex] || !chosen[starter]) return false;
        preparedRunLoadout = prepared;
        preparedStartingAnimal = starter;
        return true;
    }

    private void Start()
    {
        if (createWheelOnStart)
        {
            selectionWheel = AnimalSelectionWheel.Create(this);
        }
    }

    private void LateUpdate()
    {
        if (createWheelOnStart && selectionWheel == null)
        {
            selectionWheel = AnimalSelectionWheel.Create(this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        wheelRadius = Mathf.Max(60f, wheelRadius);
        wheelAngleStep = Mathf.Clamp(wheelAngleStep, 20f, 80f);
        wheelVerticalOffset = Mathf.Max(wheelRadius + 20f, wheelVerticalOffset);
        fireflyManualVolleysLevel1 = Mathf.Clamp(fireflyManualVolleysLevel1, 1, 5);
        fireflyManualVolleysLevel2 = Mathf.Clamp(fireflyManualVolleysLevel2, 1, 5);
        fireflyManualVolleysLevel3 = Mathf.Clamp(fireflyManualVolleysLevel3, 1, 5);
        fireflyRepeatDamageLevel1 = Mathf.Clamp01(fireflyRepeatDamageLevel1);
        fireflyRepeatDamageLevel2 = Mathf.Clamp01(fireflyRepeatDamageLevel2);
        fireflyRepeatDamageLevel3 = Mathf.Clamp01(fireflyRepeatDamageLevel3);
        fireflyBurnDamagePerUpgrade = Mathf.Max(0f, fireflyBurnDamagePerUpgrade);
        NormalizeSelectionByAvailability();
    }

    public bool Select(int index)
    {
        if (!HasAmmo(index))
        {
            return false;
        }

        if (selectedIndex == index)
        {
            return true;
        }

        selectedIndex = index;
        SelectionChanged?.Invoke(selectedIndex);
        return true;
    }

    public bool MoveSelection(int direction)
    {
        if (animals.Count == 0 || direction == 0)
        {
            return false;
        }

        int step = direction > 0 ? 1 : -1;
        int candidate = selectedIndex;

        for (int i = 0; i < animals.Count; i++)
        {
            candidate = (candidate + step + animals.Count) % animals.Count;
            if (HasAmmo(candidate))
            {
                return Select(candidate);
            }
        }

        return false;
    }

    public void ToggleAllVolleyMode()
    {
        SetAllVolleyMode(!allVolleyEnabled);
    }

    public void SetAllVolleyMode(bool enabled)
    {
        if (allVolleyEnabled == enabled) return;
        allVolleyEnabled = enabled;
        AllVolleyModeChanged?.Invoke(allVolleyEnabled);
    }

    public static int GetAllVolleyAnimalIndex(int orderPosition)
    {
        switch (orderPosition)
        {
            case 0: return FrogIndex;
            case 1: return FireflyIndex;
            case 2: return BoarIndex;
            case 3: return RabbitIndex;
            case 4: return HedgehogIndex;
            case 5: return HoneyBadgerIndex;
            case 6: return BeaverIndex;
            case 7: return SquirrelIndex;
            default: return -1;
        }
    }

    public bool Unlock(int index)
    {
        if (!IsAnimalAllowedForRun(index) || animals[index].Prefab == null)
        {
            return false;
        }

        animals[index].Unlock();
        animals[index].Reload(GetManualVolleyCount(index));

        if (!HasAmmo(selectedIndex))
        {
            selectedIndex = index;
        }

        SelectionChanged?.Invoke(selectedIndex);
        AmmoChanged?.Invoke();
        return true;
    }

    public bool IsAvailable(int index)
    {
        return IsAnimalAllowedForRun(index) && animals[index].IsAvailable;
    }

    public bool IsAnimalAllowedForRun(int index)
    {
        return index >= 0 && index < animals.Count && index <= maximumAllowedAnimalIndex
            && (selectedForRun == null || (index < selectedForRun.Length && selectedForRun[index]));
    }

    public bool ConfigureRunLoadout(IReadOnlyList<int> selected, int locationIndex = 0)
    {
        if (selected == null || selected.Count != 5 || animals.Count <= FireflyIndex) return false;
        bool[] chosen = new bool[animals.Count];
        for (int i = 0; i < selected.Count; i++)
        {
            int index = selected[i];
            if (index < 0 || index >= animals.Count || chosen[index]
                || !animals[index].IsConfigured) return false;
            chosen[index] = true;
        }
        int location = locationIndex > 0 ? locationIndex : CurrentStartingLocation;
        int starter = StartingAnimalForLocation(location);
        if (starter >= chosen.Length || !chosen[FireflyIndex] || !chosen[starter]) return false;
        selectedForRun = chosen;
        ConfigureStartingProjectiles(location);
        selectedIndex = FireflyIndex;
        ReloadAll();
        UpgradesChanged?.Invoke();
        return true;
    }

    private void ConfigureStartingProjectiles(int locationIndex)
    {
        selectedUpgradeCount = 0;
        StartingAnimalIndex = StartingAnimalForLocation(locationIndex);
        for (int i = 0; i < animals.Count; i++)
        {
            animals[i].LockForRun();
            int baseAmmo = i == FireflyIndex ? StartingAmmoForAnimal(i) : i == RabbitIndex ? 2 : 1;
            animals[i].SetStartingAmmo(i == FireflyIndex || i == StartingAnimalIndex
                ? baseAmmo : 0);
            if (i == FireflyIndex || i == StartingAnimalIndex)
            {
                animals[i].Unlock();
                if (i != FireflyIndex && i != RabbitIndex)
                    while (animals[i].AmmoUpgradeLevel < MaximumBranchLevel)
                        if (!animals[i].UpgradeAmmo(MaximumBranchLevel)) break;
            }
        }
        if (StartingAnimalIndex == RabbitIndex)
        {
            for (int rank = 0; rank < MaximumBranchLevel; rank++)
                animals[RabbitIndex].UpgradeRabbitBreeding(MaximumBranchLevel);
        }
        selectedIndex = FireflyIndex;
    }

    public void SetMaximumAllowedAnimalIndex(int maximumIndex)
    {
        int normalized = animals.Count == 0 ? -1 : Mathf.Clamp(maximumIndex, -1, animals.Count - 1);
        if (maximumAllowedAnimalIndex == normalized) return;
        maximumAllowedAnimalIndex = normalized;
        for (int i = 0; i < animals.Count; i++)
            if (!IsAnimalAllowedForRun(i)) animals[i].ClearAllAmmo();
        if (!HasAmmo(selectedIndex)) selectedIndex = FindFirstWithAmmo();
        SelectionChanged?.Invoke(selectedIndex);
        AmmoChanged?.Invoke();
        UpgradesChanged?.Invoke();
    }

    public bool ForceNextUpgradeOption(int animalIndex, UpgradeKind kind)
    {
        if (!IsAnimalAllowedForRun(animalIndex) || !animals[animalIndex].IsConfigured)
            return false;
        hasForcedUpgradeOption = true;
        forcedUpgradeAnimalIndex = animalIndex;
        forcedUpgradeKind = kind;
        UpgradesChanged?.Invoke();
        return true;
    }

    public void ClearForcedUpgradeOption()
    {
        if (!hasForcedUpgradeOption) return;
        hasForcedUpgradeOption = false;
        forcedUpgradeAnimalIndex = -1;
        UpgradesChanged?.Invoke();
    }

    public bool CanChooseUpgrade(int index)
    {
        if (index < 0 || index >= animals.Count || !animals[index].IsConfigured) return false;
        List<UpgradeOption> options = GetAvailableUpgradeOptions();
        return options.Exists(option => option.AnimalIndex == index);
    }

    public bool HasAmmo(int index)
    {
        return IsAnimalAllowedForRun(index) && animals[index].HasAmmo;
    }

    public int GetRemainingAmmo(int index)
    {
        return IsAnimalAllowedForRun(index) ? animals[index].RemainingAmmo : 0;
    }

    public int GetRemainingVolleys(int index)
    {
        return IsAnimalAllowedForRun(index) ? animals[index].RemainingVolleys : 0;
    }

    public int GetUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count ? animals[index].UpgradeLevel : 0;
    }

    public int GetAmmoUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count ? animals[index].AmmoUpgradeLevel : 0;
    }

    public int GetRepeatFireUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count ? animals[index].RepeatFireUpgradeLevel : 0;
    }

    public int GetBurnUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count ? animals[index].BurnUpgradeLevel : 0;
    }

    public bool HasFireflyUltimate(int index)
    {
        return index == FireflyIndex && index >= 0 && index < animals.Count
            && animals[index].HasFireflyUltimate;
    }

    public int GetRabbitPackStrengthUpgradeLevel(int index)
    {
        return index == RabbitIndex && index >= 0 && index < animals.Count
            ? animals[index].RabbitPackStrengthUpgradeLevel : 0;
    }

    public float GetRabbitPackDamagePerRabbit(int index)
    {
        if (index != RabbitIndex || index < 0 || index >= animals.Count) return 0f;
        return Mathf.Clamp(animals[index].RabbitPackStrengthUpgradeLevel, 0,
            MaximumBranchLevel);
    }

    public int GetRabbitBreedingUpgradeLevel(int index)
    {
        return index == RabbitIndex && index >= 0 && index < animals.Count
            ? animals[index].RabbitBreedingUpgradeLevel : 0;
    }

    public int GetRabbitMaximumReplicationHits(int index)
    {
        return 10 - Mathf.Clamp(GetRabbitBreedingUpgradeLevel(index), 0, MaximumBranchLevel);
    }

    public int GetRabbitFirstBreedingUpgradeLevel(int index)
    {
        return index == RabbitIndex && index >= 0 && index < animals.Count
            ? animals[index].RabbitFirstBreedingUpgradeLevel : 0;
    }

    public int GetRabbitInitialReplicationHits(int index)
    {
        if (index != RabbitIndex || index < 0 || index >= animals.Count) return 5;
        int reduction = Mathf.Clamp(GetRabbitFirstBreedingUpgradeLevel(index), 0, MaximumBranchLevel);
        return Mathf.Clamp(animals[index].EnemyHitsToReplicate - reduction, 1,
            GetRabbitMaximumReplicationHits(index));
    }

    public bool HasRabbitUltimate(int index)
    {
        return index == RabbitIndex && index >= 0 && index < animals.Count
            && animals[index].HasRabbitUltimate;
    }

    public int GetHedgehogRadiusUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count
            ? animals[index].HedgehogRadiusUpgradeLevel : 0;
    }

    public int GetHedgehogQuillDamageUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count
            ? animals[index].HedgehogQuillDamageUpgradeLevel : 0;
    }

    public float GetHedgehogQuillDamageGrowthPerHit(int index)
    {
        return index == HedgehogIndex && index >= 0 && index < animals.Count
            ? Mathf.Clamp(animals[index].HedgehogQuillDamageUpgradeLevel, 0, MaximumBranchLevel)
            : 0f;
    }

    public bool HasHedgehogUltimate(int index)
    {
        return index >= 0 && index < animals.Count && animals[index].HasHedgehogUltimate;
    }

    public int GetHoneyBadgerRadiusUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count
            ? animals[index].HoneyBadgerRadiusUpgradeLevel : 0;
    }

    public int GetHoneyBadgerChainUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count
            ? animals[index].HoneyBadgerChainUpgradeLevel : 0;
    }

    public bool HasHoneyBadgerUltimate(int index)
    {
        return index >= 0 && index < animals.Count && animals[index].HasHoneyBadgerUltimate;
    }

    public float GetHoneyBadgerAttackRadius(int index)
    {
        if (index != HoneyBadgerIndex || index < 0 || index >= animals.Count) return 1.2f;
        int rank = animals[index].HoneyBadgerRadiusUpgradeLevel;
        return 2f + rank;
    }

    public int GetHoneyBadgerMaximumAttacks(int index)
    {
        if (index != HoneyBadgerIndex || index < 0 || index >= animals.Count) return 5;
        switch (animals[index].HoneyBadgerChainUpgradeLevel)
        {
            case 1: return 7;
            case 2: return 9;
            case 3: return 11;
            default: return 5;
        }
    }

    public int GetHoneyBadgerConsecutiveAttacks(int index)
    {
        if (index != HoneyBadgerIndex || index < 0 || index >= animals.Count) return 1;
        return Mathf.Clamp(animals[index].HoneyBadgerChainUpgradeLevel + 1, 1, 4);
    }

    public int GetBoarBouncesUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count
            ? animals[index].BoarBouncesUpgradeLevel : 0;
    }

    public int GetBoarFirstHitUpgradeLevel(int index)
    {
        return index >= 0 && index < animals.Count
            ? animals[index].BoarFirstHitUpgradeLevel : 0;
    }

    public int GetBoarMaximumBounces(int index)
    {
        if (index != BoarIndex || index < 0 || index >= animals.Count) return 7;
        switch (animals[index].BoarBouncesUpgradeLevel)
        {
            case 1: return 10;
            case 2: return 13;
            case 3: return 16;
            default: return 7;
        }
    }

    public float GetBoarFirstHitDamageMultiplier(int index)
    {
        if (index != BoarIndex || index < 0 || index >= animals.Count) return 1f;
        return Mathf.Clamp(animals[index].BoarFirstHitUpgradeLevel + 1, 1, 4);
    }

    public bool HasBoarUltimate(int index)
    {
        return index >= 0 && index < animals.Count && animals[index].HasBoarUltimate;
    }

    public int GetBeaverDefenseHealthUpgradeLevel(int index)
    {
        return index == BeaverIndex && index >= 0 && index < animals.Count
            ? Mathf.Clamp(animals[index].BeaverDefenseHealthUpgradeLevel, 0, 3) : 0;
    }

    public float GetBeaverBonusDamage(int index)
    {
        return index == BeaverIndex && index >= 0 && index < animals.Count
            ? Mathf.Clamp(animals[index].BeaverDamageUpgradeLevel, 0, 3) * 5f : 0f;
    }

    public bool HasBeaverUltimate(int index)
    {
        return index == BeaverIndex && index >= 0 && index < animals.Count
            && animals[index].HasBeaverUltimate;
    }

    public float GetFrogJumpRadius(int index)
    {
        int rank = index == FrogIndex && index < animals.Count ? animals[index].FrogRadiusUpgradeLevel : 0;
        return 2f + rank;
    }

    public int GetFrogChorusRank(int index) => index == FrogIndex && index < animals.Count
        ? animals[index].FrogChorusUpgradeLevel : 0;

    public bool HasFrogUltimate(int index) => index == FrogIndex && index < animals.Count
        && animals[index].HasFrogUltimate;

    public float GetSquirrelEarlyExplosionChance(int index) => index == SquirrelIndex && index < animals.Count
        ? 0.05f * (1 + animals[index].SquirrelDetonationUpgradeLevel) : 0.05f;

    public int GetSquirrelBindingRank(int index) => index == SquirrelIndex && index < animals.Count
        ? animals[index].SquirrelBindingUpgradeLevel : 0;

    public bool HasSquirrelUltimate(int index) => index == SquirrelIndex && index < animals.Count
        && animals[index].HasSquirrelUltimate;

    public int GetManualVolleyCount(int index)
    {
        if (index != FireflyIndex || index < 0 || index >= animals.Count) return 1;
        switch (animals[index].RepeatFireUpgradeLevel)
        {
            case 1: return fireflyManualVolleysLevel1;
            case 2: return fireflyManualVolleysLevel2;
            case 3: return fireflyManualVolleysLevel3;
            default: return 1;
        }
    }

    public float GetManualVolleyDamageMultiplier(int index)
    {
        if (index != FireflyIndex || index < 0 || index >= animals.Count) return 1f;
        switch (animals[index].RepeatFireUpgradeLevel)
        {
            case 1: return fireflyRepeatDamageLevel1;
            case 2: return fireflyRepeatDamageLevel2;
            case 3: return fireflyRepeatDamageLevel3;
            default: return 1f;
        }
    }

    public float GetBurnDamageMultiplier(int index)
    {
        if (index != FireflyIndex || index < 0 || index >= animals.Count) return 1f;
        float baseDamage = GetFireflyBaseBurnDamage(index);
        if (baseDamage <= 0f) return 1f;
        return GetBurnDamage(index) / baseDamage;
    }

    public float GetBurnDamage(int index)
    {
        if (index != FireflyIndex || index < 0 || index >= animals.Count) return 25f;
        return (GetFireflyBaseBurnDamage(index) + animals[index].BurnUpgradeLevel * fireflyBurnDamagePerUpgrade)
            * MetaProgression.FireflyBurnDamageMultiplier;
    }

    public bool IsAreaExplosion(int index)
    {
        return index >= 0 && index < animals.Count && animals[index].IsAreaExplosion;
    }

    public float GetExplosionRadius(int index)
    {
        if (index < 0 || index >= animals.Count) return 0f;
        if (index == HedgehogIndex && animals[index].HedgehogRadiusUpgradeLevel > 0)
            return animals[index].HedgehogRadiusUpgradeLevel + 1f;
        return animals[index].ExplosionRadius;
    }

    public float GetExplosionDuration(int index)
    {
        return index >= 0 && index < animals.Count ? animals[index].ExplosionDuration : 0f;
    }

    public float GetExplosionDamageMultiplier(int index)
    {
        return index >= 0 && index < animals.Count
            ? animals[index].ExplosionDamageMultiplier
            : 0f;
    }

    public bool ReplicatesOnEnemyHits(int index)
    {
        return index >= 0 && index < animals.Count && animals[index].ReplicatesOnEnemyHits;
    }

    public int GetEnemyHitsToReplicate(int index)
    {
        return index >= 0 && index < animals.Count ? animals[index].EnemyHitsToReplicate : 5;
    }

    public bool Upgrade(int index)
    {
        List<UpgradeOption> options = GetAvailableUpgradeOptions();
        UpgradeOption option = options.Find(candidate => candidate.AnimalIndex == index);
        // Compatibility for scripted/editor callers that used the old animal-only API.
        if (option == null && options.Count > 0) option = options[0];
        return option != null && Upgrade(option.AnimalIndex, option.Kind);
    }

    public bool Upgrade(int index, UpgradeKind kind)
    {
        List<UpgradeOption> options = GetAvailableUpgradeOptions();
        if (!options.Exists(option => option.AnimalIndex == index && option.Kind == kind)) return false;

        if (index < 0 || index >= animals.Count) return false;
        AnimalProjectileEntry entry = animals[index];
        bool wasUnlocked = entry.IsUnlocked;
        bool changed;
        switch (kind)
        {
            case UpgradeKind.Ammo:
                changed = entry.UpgradeAmmo(MaximumBranchLevel);
                break;
            case UpgradeKind.RepeatFire:
                changed = entry.UpgradeRepeatFire(MaximumBranchLevel);
                break;
            case UpgradeKind.Burn:
                changed = entry.UpgradeBurn(MaximumBranchLevel);
                break;
            case UpgradeKind.FireflyUltimate:
                changed = entry.UpgradeFireflyUltimate();
                break;
            case UpgradeKind.RabbitPackStrength:
                changed = entry.UpgradeRabbitPackStrength(MaximumBranchLevel);
                break;
            case UpgradeKind.RabbitBreeding:
                changed = entry.UpgradeRabbitBreeding(MaximumBranchLevel);
                break;
            case UpgradeKind.RabbitFirstBreeding:
                changed = entry.UpgradeRabbitFirstBreeding(MaximumBranchLevel);
                break;
            case UpgradeKind.RabbitUltimate:
                changed = entry.UpgradeRabbitUltimate();
                break;
            case UpgradeKind.HedgehogRadius:
                changed = entry.UpgradeHedgehogRadius(MaximumBranchLevel);
                break;
            case UpgradeKind.HedgehogQuillDamage:
                changed = entry.UpgradeHedgehogQuillDamage(MaximumBranchLevel);
                break;
            case UpgradeKind.HedgehogUltimate:
                changed = entry.UpgradeHedgehogUltimate();
                break;
            case UpgradeKind.HoneyBadgerRadius:
                changed = entry.UpgradeHoneyBadgerRadius(MaximumBranchLevel);
                break;
            case UpgradeKind.HoneyBadgerChain:
                changed = entry.UpgradeHoneyBadgerChain(MaximumBranchLevel);
                break;
            case UpgradeKind.HoneyBadgerUltimate:
                changed = entry.UpgradeHoneyBadgerUltimate();
                break;
            case UpgradeKind.BoarBounces:
                changed = entry.UpgradeBoarBounces(MaximumBranchLevel);
                break;
            case UpgradeKind.BoarFirstHit:
                changed = entry.UpgradeBoarFirstHit(MaximumBranchLevel);
                break;
            case UpgradeKind.BoarUltimate:
                changed = entry.UpgradeBoarUltimate();
                break;
            case UpgradeKind.BeaverDefenseHealth:
                changed = entry.UpgradeBeaverDefenseHealth(MaximumBranchLevel);
                break;
            case UpgradeKind.BeaverDamage:
                changed = entry.UpgradeBeaverDamage(MaximumBranchLevel);
                break;
            case UpgradeKind.BeaverUltimate:
                changed = entry.UpgradeBeaverUltimate();
                break;
            case UpgradeKind.FrogRadius:
                changed = entry.UpgradeFrogRadius(MaximumBranchLevel);
                break;
            case UpgradeKind.FrogChorus:
                changed = entry.UpgradeFrogChorus(MaximumBranchLevel);
                break;
            case UpgradeKind.FrogUltimate:
                changed = entry.UpgradeFrogUltimate();
                break;
            case UpgradeKind.SquirrelDetonation:
                changed = entry.UpgradeSquirrelDetonation(MaximumBranchLevel);
                break;
            case UpgradeKind.SquirrelBinding:
                changed = entry.UpgradeSquirrelBinding(MaximumBranchLevel);
                break;
            case UpgradeKind.SquirrelUltimate:
                changed = entry.UpgradeSquirrelUltimate();
                break;
            default:
                changed = false;
                break;
        }
        if (!changed) return false;
        selectedUpgradeCount++;
        if (index == FrogIndex)
            FrogRider.UpgradeAll(GetFrogJumpRadius(index), GetFrogChorusRank(index), HasFrogUltimate(index));
        if (index == SquirrelIndex)
            SquirrelAcorn.UpgradeAll(GetSquirrelEarlyExplosionChance(index), GetSquirrelBindingRank(index), HasSquirrelUltimate(index));

        if (hasForcedUpgradeOption
            && forcedUpgradeAnimalIndex == index
            && forcedUpgradeKind == kind)
        {
            hasForcedUpgradeOption = false;
            forcedUpgradeAnimalIndex = -1;
        }

        if (!wasUnlocked && entry.IsUnlocked)
        {
            entry.Reload(GetManualVolleyCount(index));
            if (!HasAmmo(selectedIndex))
            {
                selectedIndex = index;
                SelectionChanged?.Invoke(selectedIndex);
            }
        }

        if (kind == UpgradeKind.Ammo) AmmoChanged?.Invoke();
        UpgradesChanged?.Invoke();
        return true;
    }

    public List<UpgradeOption> GetAvailableUpgradeOptions()
    {
        List<UpgradeOption> options = new List<UpgradeOption>();
        for (int i = 0; i < animals.Count; i++)
        {
            if (!IsAnimalAllowedForRun(i)) continue;
            AnimalProjectileEntry entry = animals[i];
            if (!entry.IsConfigured) continue;

            // Firefly has a fixed volley; rabbit can be unlocked here but has no quantity branch.
            if (i != FireflyIndex && entry.AmmoUpgradeLevel < MaximumBranchLevel
                && (i != RabbitIndex || !entry.IsUnlocked))
            {
                string description = entry.IsUnlocked
                    ? "Количество: " + entry.AmmoPerTurn + " -> " + (entry.AmmoPerTurn + 1)
                        + "\nУровень " + entry.AmmoUpgradeLevel + "/" + MaximumBranchLevel
                    : "Открыть новый снаряд\nКоличество: " + entry.BaseAmmoPerTurn;
                options.Add(CreateOption(i, UpgradeKind.Ammo,
                    entry.IsUnlocked ? entry.DisplayName + ": количество" : "Открыть: " + entry.DisplayName,
                    description));
            }

            if (i == FireflyIndex && entry.IsUnlocked
                && entry.RepeatFireUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.RepeatFireUpgradeLevel;
                int nextRank = currentRank + 1;
                options.Add(CreateOption(i, UpgradeKind.RepeatFire, entry.DisplayName + ": повторный залп",
                    RepeatFireDescription(currentRank) + " -> " + RepeatFireDescription(nextRank)
                    + "\nУровень " + currentRank + "/" + MaximumBranchLevel));
            }

            if (i == RabbitIndex && entry.IsUnlocked
                && entry.RabbitPackStrengthUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.RabbitPackStrengthUpgradeLevel;
                options.Add(CreateOption(i, UpgradeKind.RabbitPackStrength,
                    entry.DisplayName + ": сила стаи",
                    "Урон за каждого живого кролика: +" + currentRank
                    + " -> +" + (currentRank + 1) + "\nУровень "
                    + currentRank + "/" + MaximumBranchLevel));
            }
            if (i == RabbitIndex && entry.IsUnlocked
                && entry.RabbitBreedingUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.RabbitBreedingUpgradeLevel;
                int currentMaximum = GetRabbitMaximumReplicationHits(i);
                options.Add(CreateOption(i, UpgradeKind.RabbitBreeding,
                    entry.DisplayName + ": быстрое размножение",
                    "Максимум ударов до размножения: " + currentMaximum
                    + " -> " + (currentMaximum - 1) + "\nПервое размножение: " + GetRabbitInitialReplicationHits(i)
                    + " ударов. Затем порог растёт на 1 до максимума"
                    + "\nУровень "
                    + currentRank + "/" + MaximumBranchLevel));
            }
            if (i == RabbitIndex && entry.IsUnlocked
                && entry.RabbitFirstBreedingUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.RabbitFirstBreedingUpgradeLevel;
                int currentHits = GetRabbitInitialReplicationHits(i);
                options.Add(CreateOption(i, UpgradeKind.RabbitFirstBreeding,
                    entry.DisplayName + ": ранний приплод",
                    "Ударов до первого размножения: " + currentHits + " -> " + Mathf.Max(1, currentHits - 1)
                    + "\nКаждый уровень: первое размножение на 1 удар раньше"
                    + "\nУровень " + currentRank + "/" + MaximumBranchLevel));
            }
            if (i == RabbitIndex && entry.IsUnlocked
                && entry.RabbitPackStrengthUpgradeLevel >= MaximumBranchLevel
                && entry.RabbitBreedingUpgradeLevel >= MaximumBranchLevel
                && entry.RabbitFirstBreedingUpgradeLevel >= MaximumBranchLevel
                && !entry.HasRabbitUltimate)
            {
                options.Add(CreateOption(i, UpgradeKind.RabbitUltimate,
                    entry.DisplayName + ": пуховая буря",
                    "УЛЬТА: каждый 10-й суммарный удар заставляет всех кроликов "
                    + "выпустить облако на 10 урона\nОблака не считаются ударами и не увеличивают комбо"));
            }
            if (i == FireflyIndex && entry.IsUnlocked
                && entry.BurnUpgradeLevel < MaximumBranchLevel)
            {
                float current = GetBurnDamage(i);
                float next = current + fireflyBurnDamagePerUpgrade * MetaProgression.FireflyBurnDamageMultiplier;
                options.Add(CreateOption(i, UpgradeKind.Burn, entry.DisplayName + ": горение",
                    "Урон одного стака за ход: " + FormatDamage(current) + " -> " + FormatDamage(next)
                    + "\nУровень " + entry.BurnUpgradeLevel + "/" + MaximumBranchLevel));
            }
            if (i == FireflyIndex && entry.IsUnlocked
                && entry.RepeatFireUpgradeLevel >= MaximumBranchLevel
                && entry.BurnUpgradeLevel >= MaximumBranchLevel
                && !entry.HasFireflyUltimate)
            {
                options.Add(CreateOption(i, UpgradeKind.FireflyUltimate,
                    entry.DisplayName + ": вечный огонь",
                    "УЛЬТА: стаки горения больше не исчезают\n"
                    + "Количество стаков больше не ограничено"));
            }

            if (i == HedgehogIndex && entry.IsUnlocked
                && entry.HedgehogRadiusUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.HedgehogRadiusUpgradeLevel;
                float currentRadius = currentRank <= 0 ? entry.ExplosionRadius : currentRank + 1f;
                float nextRadius = currentRank + 2f;
                options.Add(CreateOption(i, UpgradeKind.HedgehogRadius,
                    entry.DisplayName + ": радиус взрыва",
                    "Радиус: " + currentRadius.ToString("0.#") + " -> "
                    + nextRadius.ToString("0") + " клетки\nУровень "
                    + currentRank + "/" + MaximumBranchLevel));
            }
            if (i == HedgehogIndex && entry.IsUnlocked
                && entry.HedgehogQuillDamageUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.HedgehogQuillDamageUpgradeLevel;
                options.Add(CreateOption(i, UpgradeKind.HedgehogQuillDamage,
                    entry.DisplayName + ": острые иголки",
                    "Рост урона по врагу за взрыв в этом ходу: +" + currentRank
                    + " -> +" + (currentRank + 1)
                    + "\nУровень "
                    + currentRank + "/" + MaximumBranchLevel));
            }
            if (i == HedgehogIndex && entry.IsUnlocked
                && entry.AmmoUpgradeLevel >= MaximumBranchLevel
                && entry.HedgehogRadiusUpgradeLevel >= MaximumBranchLevel
                && entry.HedgehogQuillDamageUpgradeLevel >= MaximumBranchLevel
                && !entry.HasHedgehogUltimate)
            {
                options.Add(CreateOption(i, UpgradeKind.HedgehogUltimate,
                    entry.DisplayName + ": двойной залп",
                    "УЛЬТА: два взрыва при столкновении\n"
                    + "Второй взрыв происходит через 0,09 секунды"));
            }

            if (i == BoarIndex && entry.IsUnlocked
                && entry.BoarBouncesUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.BoarBouncesUpgradeLevel;
                options.Add(CreateOption(i, UpgradeKind.BoarBounces,
                    entry.DisplayName + ": неудержимый разбег",
                    "Отскоки: " + BoarBounceCount(currentRank) + " -> "
                    + BoarBounceCount(currentRank + 1) + "\nУровень "
                    + currentRank + "/" + MaximumBranchLevel));
            }
            if (i == BoarIndex && entry.IsUnlocked
                && entry.BoarFirstHitUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.BoarFirstHitUpgradeLevel;
                options.Add(CreateOption(i, UpgradeKind.BoarFirstHit,
                    entry.DisplayName + ": первый таран",
                    "Первый удар по каждому врагу: "
                    + FormatPercent(BoarFirstHitPercent(currentRank)) + " -> "
                    + FormatPercent(BoarFirstHitPercent(currentRank + 1)) + "\nУровень "
                    + currentRank + "/" + MaximumBranchLevel));
            }
            if (i == BoarIndex && entry.IsUnlocked
                && entry.AmmoUpgradeLevel >= MaximumBranchLevel
                && entry.BoarBouncesUpgradeLevel >= MaximumBranchLevel
                && entry.BoarFirstHitUpgradeLevel >= MaximumBranchLevel
                && !entry.HasBoarUltimate)
            {
                options.Add(CreateOption(i, UpgradeKind.BoarUltimate,
                    entry.DisplayName + ": тройной прорыв",
                    "УЛЬТА: три кабана летят одновременно\n"
                    + "Боковые траектории отклонены на ±4°"));
            }

            if (i == BeaverIndex && entry.IsUnlocked)
            {
                int defenseRank = entry.BeaverDefenseHealthUpgradeLevel;
                if (defenseRank < MaximumBranchLevel)
                    options.Add(CreateOption(i, UpgradeKind.BeaverDefenseHealth,
                        entry.DisplayName + ": крепкая плотина",
                        "Здоровье плотины/баррикады от нанесённого урона: "
                        + (defenseRank + 1) + "% -> " + (defenseRank + 2) + "%"
                        + "\nПредел плотины: 25% макс. HP баррикады"
                        + "\nУровень " + defenseRank + "/" + MaximumBranchLevel));
                int damageRank = entry.BeaverDamageUpgradeLevel;
                if (damageRank < MaximumBranchLevel)
                    options.Add(CreateOption(i, UpgradeKind.BeaverDamage,
                        entry.DisplayName + ": крепкие зубы",
                        "Урон за укус: +" + (damageRank * 5) + " -> +"
                        + ((damageRank + 1) * 5) + "\nУровень "
                        + damageRank + "/" + MaximumBranchLevel));
                if (entry.AmmoUpgradeLevel >= MaximumBranchLevel
                    && defenseRank >= MaximumBranchLevel
                    && damageRank >= MaximumBranchLevel
                    && !entry.HasBeaverUltimate)
                    options.Add(CreateOption(i, UpgradeKind.BeaverUltimate,
                        entry.DisplayName + ": отскок от тыквы",
                        "УЛЬТА: +1 отскок от поля тыквы"));
            }

            if (i == FrogIndex && entry.IsUnlocked)
            {
                int radiusRank = entry.FrogRadiusUpgradeLevel;
                if (radiusRank < MaximumBranchLevel)
                    options.Add(CreateOption(i, UpgradeKind.FrogRadius,
                        entry.DisplayName + ": дальний прыжок",
                        "Радиус прыжка: " + GetFrogJumpRadius(i).ToString("0.#")
                        + " -> " + (radiusRank + 3) + " клетки\nУровень "
                        + radiusRank + "/" + MaximumBranchLevel));
                int chorusRank = entry.FrogChorusUpgradeLevel;
                if (chorusRank < MaximumBranchLevel)
                    options.Add(CreateOption(i, UpgradeKind.FrogChorus,
                        entry.DisplayName + ": лягушачий хор",
                        "Урон в комнате за каждую лягушку: +" + chorusRank + "% -> +"
                        + (chorusRank + 1) + "%\nУровень " + chorusRank + "/" + MaximumBranchLevel));
                if (entry.AmmoUpgradeLevel >= MaximumBranchLevel
                    && radiusRank >= MaximumBranchLevel && chorusRank >= MaximumBranchLevel
                    && !entry.HasFrogUltimate)
                    options.Add(CreateOption(i, UpgradeKind.FrogUltimate,
                        entry.DisplayName + ": КВА",
                        "УЛЬТА: весь урон лягушки действует на цель и врагов в радиусе 1 клетки"));
            }

            if (i == SquirrelIndex && entry.IsUnlocked)
            {
                int detonationRank = entry.SquirrelDetonationUpgradeLevel;
                if (detonationRank < MaximumBranchLevel)
                    options.Add(CreateOption(i, UpgradeKind.SquirrelDetonation, entry.DisplayName + ": ранний взрыв",
                        "Шанс взрыва при повторном попадании белочки: "
                        + (detonationRank + 1) * 5 + "% -> " + (detonationRank + 2) * 5 + "%"
                        + "\nПри смерти врага жёлудь взрывается всегда"));
                int vinesRank = entry.SquirrelBindingUpgradeLevel;
                if (vinesRank < MaximumBranchLevel)
                    options.Add(CreateOption(i, UpgradeKind.SquirrelBinding, entry.DisplayName + ": лесные путы",
                        "Шанс оцепенения при взрыве: " + vinesRank * 3 + "% -> " + (vinesRank + 1) * 3
                        + "% для каждого врага\nЛозы удерживают врага 1 ход"));
                if (entry.AmmoUpgradeLevel >= MaximumBranchLevel
                    && detonationRank >= MaximumBranchLevel && vinesRank >= MaximumBranchLevel
                    && !entry.HasSquirrelUltimate)
                    options.Add(CreateOption(i, UpgradeKind.SquirrelUltimate,
                        entry.DisplayName + ": цепная реакция",
                        "УЛЬТА: взрыв жёлудя подрывает соседние жёлуди\n"
                        + "Цепь продолжается, пока рядом есть жёлуди"));
            }

            if (i != HoneyBadgerIndex || !entry.IsUnlocked) continue;
            if (entry.HoneyBadgerRadiusUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.HoneyBadgerRadiusUpgradeLevel;
                float currentRadius = currentRank <= 0 ? 1.2f : currentRank + 1f;
                float nextRadius = currentRank + 2f;
                options.Add(CreateOption(i, UpgradeKind.HoneyBadgerRadius,
                    entry.DisplayName + ": радиус атаки",
                    "Радиус: " + currentRadius.ToString("0.#") + " -> " + nextRadius.ToString("0.#")
                    + " клетки\nУровень " + currentRank + "/" + MaximumBranchLevel));
            }
            if (entry.HoneyBadgerChainUpgradeLevel < MaximumBranchLevel)
            {
                int currentRank = entry.HoneyBadgerChainUpgradeLevel;
                options.Add(CreateOption(i, UpgradeKind.HoneyBadgerChain,
                    entry.DisplayName + ": яростная цепь",
                    HoneyBadgerChainDescription(currentRank) + " -> "
                    + HoneyBadgerChainDescription(currentRank + 1)
                    + "\nУровень " + currentRank + "/" + MaximumBranchLevel));
            }
            if (entry.AmmoUpgradeLevel >= MaximumBranchLevel
                && entry.HoneyBadgerRadiusUpgradeLevel >= MaximumBranchLevel
                && entry.HoneyBadgerChainUpgradeLevel >= MaximumBranchLevel
                && !entry.HasHoneyBadgerUltimate)
            {
                options.Add(CreateOption(i, UpgradeKind.HoneyBadgerUltimate,
                    entry.DisplayName + ": абсолютная ярость",
                    "УЛЬТА: каждый следующий удар получает ещё +10 урона\n"
                    + "Удары: +10, +20, +30..."));
            }
        }



        if (hasForcedUpgradeOption)
        {
            UpgradeOption forced = options.Find(option =>
                option.AnimalIndex == forcedUpgradeAnimalIndex
                && option.Kind == forcedUpgradeKind);
            if (forced != null) return new List<UpgradeOption> { forced };
        }

        return options;
    }

    private UpgradeOption CreateOption(int animalIndex, UpgradeKind kind, string title, string description)
    {
        AnimalProjectileEntry entry = animals[animalIndex];
        return new UpgradeOption(animalIndex, kind, title, description,
            entry.GetIcon(), entry.GetIconColor());
    }

    private static float GetFireflyBaseBurnDamage(AnimalProjectileEntry entry)
    {
        FireflyImpactEffect effect = entry != null && entry.Prefab != null
            ? entry.Prefab.GetComponent<FireflyImpactEffect>() : null;
        return effect != null ? effect.BaseBurnDamage : 25f;
    }

    private float GetFireflyBaseBurnDamage(int index)
    {
        if (index < 0 || index >= animals.Count) return 25f;
        return GetFireflyBaseBurnDamage(animals[index]);
    }

    private static string FormatDamage(float value)
    {
        return Mathf.RoundToInt(Mathf.Max(0f, value)).ToString();
    }

    private string RepeatFireDescription(int rank)
    {
        if (rank <= 0) return "1 выстрел со 100%";
        int shots = rank == 1 ? fireflyManualVolleysLevel1
            : rank == 2 ? fireflyManualVolleysLevel2 : fireflyManualVolleysLevel3;
        float damage = rank == 1 ? fireflyRepeatDamageLevel1
            : rank == 2 ? fireflyRepeatDamageLevel2 : fireflyRepeatDamageLevel3;
        return shots + (shots == 1 ? " ручной залп " : shots < 5 ? " ручных залпа " : " ручных залпов ")
            + "по " + FormatPercent(damage * 100f);
    }

    private static string HoneyBadgerChainDescription(int rank)
    {
        switch (rank)
        {
            case 1: return "7 атак, до 2 по одной цели";
            case 2: return "9 атак, до 3 по одной цели";
            case 3: return "11 атак, до 4 по одной цели";
            default: return "5 атак, 1 по одной цели";
        }
    }

    private static int BoarBounceCount(int rank)
    {
        switch (rank)
        {
            case 1: return 10;
            case 2: return 13;
            case 3: return 16;
            default: return 7;
        }
    }

    private static float BoarFirstHitPercent(int rank)
    {
        return Mathf.Clamp(rank + 1, 1, 4) * 100f;
    }

    private static string FormatPercent(float value)
    {
        return Mathf.RoundToInt(value) + "%";
    }

    public bool TryConsume(int index, out GameObject projectilePrefab)
    {
        projectilePrefab = null;
        if (!HasAmmo(index))
        {
            return false;
        }

        projectilePrefab = animals[index].Prefab;
        animals[index].TryConsume();

        if (index == selectedIndex && !HasAmmo(index))
        {
            SelectNextWithAmmo();
        }

        AmmoChanged?.Invoke();
        return true;
    }

    public void ReloadAll()
    {
        for (int i = 0; i < animals.Count; i++)
        {
            if (IsAnimalAllowedForRun(i)) animals[i].Reload(GetManualVolleyCount(i));
            else animals[i].ClearAllAmmo();
        }

        if (!HasAmmo(selectedIndex))
        {
            selectedIndex = FindFirstWithAmmo();
        }

        SelectionChanged?.Invoke(selectedIndex);
        AmmoChanged?.Invoke();
    }

    public void ClearAmmo(int index)
    {
        if (index < 0 || index >= animals.Count) return;
        animals[index].ClearCurrentVolleyAmmo();
        if (index == selectedIndex) SelectNextWithAmmo();
        AmmoChanged?.Invoke();
    }

    public bool CompleteVolley(int index)
    {
        if (index < 0 || index >= animals.Count) return false;
        bool reloaded = animals[index].CompleteVolley();
        if (!HasAmmo(selectedIndex) && HasAmmo(index))
        {
            selectedIndex = index;
            SelectionChanged?.Invoke(selectedIndex);
        }
        AmmoChanged?.Invoke();
        return reloaded;
    }

    public void ClearAllAmmo()
    {
        for (int i = 0; i < animals.Count; i++)
        {
            animals[i].ClearAllAmmo();
        }

        AmmoChanged?.Invoke();
    }

    private void SelectNextWithAmmo()
    {
        if (animals.Count == 0)
        {
            return;
        }

        int candidate = selectedIndex;
        for (int i = 0; i < animals.Count; i++)
        {
            candidate = (candidate + 1 + animals.Count) % animals.Count;
            if (HasAmmo(candidate))
            {
                selectedIndex = candidate;
                SelectionChanged?.Invoke(selectedIndex);
                return;
            }
        }
    }

    private int FindFirstWithAmmo()
    {
        for (int i = 0; i < animals.Count; i++)
        {
            if (HasAmmo(i))
            {
                return i;
            }
        }

        return -1;
    }

    private void NormalizeSelectionByAvailability()
    {
        if (IsAvailable(selectedIndex))
        {
            return;
        }

        selectedIndex = -1;
        for (int i = 0; i < animals.Count; i++)
        {
            if (IsAvailable(i))
            {
                selectedIndex = i;
                return;
            }
        }
    }
}
