using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelProgression : MonoBehaviour
{
    public const int CampaignWavesPerRoom = 5;
    public const int FirstLineMinimumEnemyTotal = 15;
    public const int FirstLineMaximumEnemyTotal = 21;
    public const int SecondLineMinimumEnemyTotal = 14;
    public const int SecondLineMaximumEnemyTotal = 18;
    public const int ThirdLineMinimumEnemyTotal = 12;
    public const int ThirdLineMaximumEnemyTotal = 16;
    public const int BossReinforcementEnemyTotal = 4;
    public const int FourthLineOrdinaryEnemyTotal = 5;

    public enum BossKind
    {
        None,
        Lich,
        BoneDragonMini,
        DrownedMini,
        Mirage,
        BoneDragon,
        GhostlySorrow,
        PoisonousGloom,
        HiveHollow,
        GreenJudge,
        BellRinger,
        KnotBinder,
        VolleyEater,
        ThreeSealIdol,
        Impostor
    }

    [Serializable]
    public class ZombieSpawnRule
    {
        [Range(1, MaxLevel)] public int firstLevel = 2;
        [Range(1, MaxLevel)] public int lastLevel = MaxLevel;
        [Range(0, 4), Tooltip("0 — любая линия.")]
        public int defenseLine;
        [Range(0, 7), Tooltip("0 — любая комната выбранной линии.")]
        public int roomIndex;
        [Min(0), Tooltip("Сколько обычных врагов в каждой волне заменить зомби.")]
        public int zombiesPerWave = 1;

        public bool Matches(int level, int line, int room)
        {
            int from = Mathf.Clamp(Mathf.Min(firstLevel, lastLevel), 1, MaxLevel);
            int to = Mathf.Clamp(Mathf.Max(firstLevel, lastLevel), 1, MaxLevel);
            return level >= from && level <= to
                && (defenseLine == 0 || defenseLine == line)
                && (roomIndex == 0 || roomIndex == room);
        }

        public int Specificity => (defenseLine == 0 ? 0 : 1) + (roomIndex == 0 ? 0 : 2);
    }

    [Serializable]
    public class ArmoredSkeletonSpawnRule
    {
        [Range(1, MaxLevel)] public int firstLevel = LevelsPerLocation + 1;
        [Range(1, MaxLevel)] public int lastLevel = MaxLevel;
        [Range(0, 4), Tooltip("0 — любая линия.")]
        public int defenseLine;
        [Range(0, 7), Tooltip("0 — любая комната выбранной линии.")]
        public int roomIndex;
        [Min(0), Tooltip("Сколько обычных врагов в каждой волне заменить бронированными скелетами.")]
        public int armoredSkeletonsPerWave = 1;

        public bool Matches(int level, int line, int room)
        {
            int from = Mathf.Clamp(Mathf.Min(firstLevel, lastLevel), 1, MaxLevel);
            int to = Mathf.Clamp(Mathf.Max(firstLevel, lastLevel), 1, MaxLevel);
            return level >= from && level <= to
                && (defenseLine == 0 || defenseLine == line)
                && (roomIndex == 0 || roomIndex == room);
        }

        public int Specificity => (defenseLine == 0 ? 0 : 1) + (roomIndex == 0 ? 0 : 2);
    }

    [Serializable]
    public struct DefenseLineBalance
    {
        [Min(0)] public int enemiesPerWave;
        [Min(0)] public int additionalWaves;

        public DefenseLineBalance(int enemies, int waves)
        {
            enemiesPerWave = enemies;
            additionalWaves = waves;
        }
    }

    [Serializable]
    public class GameLevelBalance
    {
        [Range(1, MaxLevel)] public int levelNumber;
        public List<DefenseLineBalance> defenseLines;
        [Tooltip("Номера активных комнат четвёртой линии: от 1 до 7.")]
        public List<int> activeFourthLineRooms;
        [Tooltip("Legacy Mirage room data. Actual bosses are assigned by the per-level Bosses list.")]
        public List<int> mirageRoomIndices;
        [Range(1, 7), Tooltip("Четвёртая линия: босс всегда в комнате 4. На второй и третьей линиях — в комнатах 2 и 3.")]
        public int bossRoomIndex;
        [Tooltip("Three different bosses, in line 2/3/4 order. Dragons are exclusive to their own location finale.")]
        public List<BossKind> bosses;
        [Tooltip("Specialists available on this level, using location roster slots 1 through 4.")]
        public List<int> specialistSlots;

        public GameLevelBalance(int number, int[] fourthLineRooms, int bossRoom, params DefenseLineBalance[] lines)
        {
            levelNumber = number;
            defenseLines = new List<DefenseLineBalance>(lines);
            activeFourthLineRooms = new List<int>(fourthLineRooms);
            bossRoomIndex = bossRoom;
        }

        public DefenseLineBalance GetLine(int lineNumber)
        {
            if (defenseLines == null || defenseLines.Count == 0)
            {
                return new DefenseLineBalance(1, 0);
            }

            int index = Mathf.Clamp(lineNumber - 1, 0, defenseLines.Count - 1);
            return defenseLines[index];
        }
    }

    public static LevelProgression Instance { get; private set; }

    private const string LegacyCurrentLevelKey = "ForestAnimalWar.CurrentLevel";
    private const string HighestUnlockedLevelKey = "ForestAnimalWar.HighestUnlockedLevel";
    private const string MenuSelectedLevelKey = "ForestAnimalWar.MenuSelectedLevel";
    private const string RequestedLevelKey = "ForestAnimalWar.RequestedLevel";
    public const string TwilightThicketName = "Сумеречная чаща";
    public const string GloomyHollowName = "Мрачная лощина";
    public const string DrownedBogName = "Топь утопленников";
    public const string ForgottenBayName = "Забытый залив";
    public const string SnowyHazeName = "Снежная мгла";
    public const string FieryGehennaName = "Геенна огненная";
    public const int LocationCount = 6;
    public const int LevelsPerLocation = 5;
    public const int MaxLevel = LocationCount * LevelsPerLocation;

    [SerializeField, Range(1, MaxLevel)] private int currentLevel = 1;
    [SerializeField] private List<GameLevelBalance> levels = CreateDefaultLevels();
    public const float DefaultEnemyHealthLevelSpan = 8f;
    public const float DefaultEnemyHealthLevelExponent = 1.9f;

    [SerializeField, Min(1f), Tooltip("Enemy HP multiplier = (1 + (level - 1) / span) ^ exponent.")]
    private float enemyHealthLevelSpan = DefaultEnemyHealthLevelSpan;
    [SerializeField, Min(0.01f), Tooltip("Power curve with a decreasing percentage gain per level.")]
    private float enemyHealthLevelExponent = DefaultEnemyHealthLevelExponent;
    [SerializeField, Tooltip("Boss HP relative to ordinary base HP on lines 2, 3 and 4.")]
    private Vector3 bossHealthMultipliers = new Vector3(20f, 30f, 40f);
    public const int DefaultBossEscortMinimum = 3;
    public const int DefaultBossEscortMaximum = 4;
    public const int DefaultBossRoomEnemyCap = 6;
    [Header("Boss escorts")]
    [SerializeField, Range(0, 8), Tooltip("Minimum ordinary enemies a boss summons on each of its turns.")]
    private int bossEscortMinimum = DefaultBossEscortMinimum;
    [SerializeField, Range(0, 8), Tooltip("Maximum ordinary enemies a boss summons on each of its turns.")]
    private int bossEscortMaximum = DefaultBossEscortMaximum;
    [SerializeField, Range(0, 15), Tooltip("A boss stops summoning while its room holds this many enemies besides the boss.")]
    private int bossRoomEnemyCap = DefaultBossRoomEnemyCap;
    [Header("Zombie encounters")]
    [SerializeField, Tooltip("Later, more specific rules override broad rules above them. "
        + "Line 0 and room 0 mean every active room.")]
    private List<ZombieSpawnRule> zombieSpawnRules = CreateDefaultZombieRules();
    [Header("Armored skeleton encounters")]
    [SerializeField, Tooltip("Later, more specific rules override broad rules above them. "
        + "By default one armored skeleton replaces a base enemy in every Gloomy Hollow wave.")]
    private List<ArmoredSkeletonSpawnRule> armoredSkeletonSpawnRules =
        CreateDefaultArmoredSkeletonRules();
    private readonly Dictionary<int, Dictionary<int, int>> campaignWavePlans =
        new Dictionary<int, Dictionary<int, int>>();

    public int CurrentLevel => currentLevel;
    public int CurrentLocationIndex => LocationIndexForLevel(currentLevel);
    public int CurrentLocalLevel => LocalLevelFor(currentLevel);
    public bool IsGloomyHollow => CurrentLocationIndex == 2;
    public bool HasNextLevel => currentLevel < MaxLevel;
    public float EnemyHealthMultiplier => GetEnemyHealthMultiplier(currentLevel);

    public float GetEnemyHealthMultiplier(int levelNumber) => Mathf.Pow(
        1f + (Mathf.Clamp(levelNumber, 1, MaxLevel) - 1) / Mathf.Max(1f, enemyHealthLevelSpan),
        Mathf.Max(0.01f, enemyHealthLevelExponent));

    public float GetBossHealthMultiplier(int defenseLine)
    {
        switch (defenseLine)
        {
            case 2: return Mathf.Max(1f, bossHealthMultipliers.x);
            case 3: return Mathf.Max(1f, bossHealthMultipliers.y);
            case 4: return Mathf.Max(1f, bossHealthMultipliers.z);
            default: return 1f;
        }
    }

    public static float GetCampaignWaveHealthMultiplier(int waveNumber)
    {
        return 1f + 0.25f * (Mathf.Clamp(waveNumber, 1, CampaignWavesPerRoom) - 1);
    }
    public static int HighestUnlockedLevel
    {
        get
        {
            int legacyLevel = PlayerPrefs.GetInt(LegacyCurrentLevelKey, 1);
            int unlockedLevel = PlayerPrefs.GetInt(HighestUnlockedLevelKey, legacyLevel);
            return Mathf.Clamp(Mathf.Max(legacyLevel, unlockedLevel), 1, MaxLevel);
        }
    }
    public static int SelectedMenuLevel => Mathf.Clamp(
        PlayerPrefs.GetInt(MenuSelectedLevelKey, 1),
        1,
        GameRunMode.DeveloperModeEnabled ? MaxLevel : HighestUnlockedLevel);
    public static int SelectedMenuLocation => LocationIndexForLevel(SelectedMenuLevel);
    public static int RequestedLocationIndex => LocationIndexForLevel(PlayerPrefs.GetInt(RequestedLevelKey, 1));
    public static int SelectedMenuLocalLevel => LocalLevelFor(SelectedMenuLevel);
    public static string CurrentLocationName => LocationNameForLevel(
        Instance != null ? Instance.CurrentLevel : SelectedMenuLevel);
    public static int SavedLevel => SelectedMenuLevel;

    public static int LocationIndexForLevel(int globalLevel)
    {
        return Mathf.Clamp((Mathf.Clamp(globalLevel, 1, MaxLevel) - 1) / LevelsPerLocation + 1,
            1, LocationCount);
    }

    public static int LocalLevelFor(int globalLevel)
    {
        return (Mathf.Clamp(globalLevel, 1, MaxLevel) - 1) % LevelsPerLocation + 1;
    }

    public static int StartingUpgradeChoicesForLevel(int globalLevel)
    {
        // Campaign levels no longer grant free upgrades just for their position
        // within a location. Kills and room clears still grant upgrades normally.
        return 0;
    }

    public static int GlobalLevelFor(int locationIndex, int localLevel)
    {
        int safeLocation = Mathf.Clamp(locationIndex, 1, LocationCount);
        int safeLocalLevel = Mathf.Clamp(localLevel, 1, LevelsPerLocation);
        return (safeLocation - 1) * LevelsPerLocation + safeLocalLevel;
    }

    public static string LocationNameForIndex(int locationIndex)
    {
        switch (Mathf.Clamp(locationIndex, 1, LocationCount))
        {
            case 2: return GloomyHollowName;
            case 3: return DrownedBogName;
            case 4: return ForgottenBayName;
            case 5: return SnowyHazeName;
            case 6: return FieryGehennaName;
            default: return TwilightThicketName;
        }
    }

    public static string LocationNameForLevel(int globalLevel)
    {
        return LocationNameForIndex(LocationIndexForLevel(globalLevel));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        currentLevel = ConsumeRequestedLevel();

        NormalizeLevelData();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public DefenseLineBalance GetBalance(int defenseLine)
    {
        return GetBalance(currentLevel, defenseLine);
    }

    public int GetZombieCountPerWave(int levelNumber, int defenseLine, int roomIndex)
    {
        if (zombieSpawnRules == null) return 0;
        int result = 0;
        int bestSpecificity = -1;
        for (int i = 0; i < zombieSpawnRules.Count; i++)
        {
            ZombieSpawnRule rule = zombieSpawnRules[i];
            if (rule == null || !rule.Matches(levelNumber, defenseLine, roomIndex)) continue;
            int specificity = rule.Specificity;
            if (specificity < bestSpecificity) continue;
            bestSpecificity = specificity;
            result = Mathf.Max(0, rule.zombiesPerWave);
        }
        return result;
    }

    public int GetZombieCountPerWave(int defenseLine, int roomIndex)
    {
        return GetZombieCountPerWave(currentLevel, defenseLine, roomIndex);
    }

    public int GetArmoredSkeletonCountPerWave(int levelNumber, int defenseLine, int roomIndex)
    {
        if (armoredSkeletonSpawnRules == null) return 0;
        int result = 0;
        int bestSpecificity = -1;
        for (int i = 0; i < armoredSkeletonSpawnRules.Count; i++)
        {
            ArmoredSkeletonSpawnRule rule = armoredSkeletonSpawnRules[i];
            if (rule == null || !rule.Matches(levelNumber, defenseLine, roomIndex)) continue;
            int specificity = rule.Specificity;
            if (specificity < bestSpecificity) continue;
            bestSpecificity = specificity;
            result = Mathf.Max(0, rule.armoredSkeletonsPerWave);
        }
        return result;
    }

    public int GetArmoredSkeletonCountPerWave(int defenseLine, int roomIndex)
    {
        return GetArmoredSkeletonCountPerWave(currentLevel, defenseLine, roomIndex);
    }

    public DefenseLineBalance GetBalance(int levelNumber, int defenseLine)
    {
        GameLevelBalance level = FindLevel(levelNumber);
        return level != null
            ? level.GetLine(defenseLine)
            : new DefenseLineBalance(1, 0);
    }

    public int GetCampaignWaveEnemyCount(int defenseLine, int roomIndex, int waveNumber)
    {
        return GetCampaignWaveEnemyCount(currentLevel, defenseLine, roomIndex, waveNumber);
    }

    public int GetCampaignWaveEnemyCount(int levelNumber, int defenseLine, int roomIndex,
        int waveNumber)
    {
        int safeLevel = Mathf.Clamp(levelNumber, 1, MaxLevel);
        if (!campaignWavePlans.TryGetValue(safeLevel, out Dictionary<int, int> plan))
        {
            plan = BuildCampaignWavePlan(safeLevel);
            campaignWavePlans.Add(safeLevel, plan);
        }
        int key = CampaignWaveKey(defenseLine, roomIndex, waveNumber);
        return plan.TryGetValue(key, out int count) ? count : 0;
    }

    public int GetCampaignPlannedEnemyTotal(int levelNumber)
    {
        int safeLevel = Mathf.Clamp(levelNumber, 1, MaxLevel);
        if (!campaignWavePlans.TryGetValue(safeLevel, out Dictionary<int, int> plan))
        {
            plan = BuildCampaignWavePlan(safeLevel);
            campaignWavePlans.Add(safeLevel, plan);
        }
        int total = 0;
        foreach (int count in plan.Values) total += count;
        return total + GetScriptedEnemyReserve(levelNumber);
    }

    public int GetCampaignRoomEnemyTotal(int levelNumber, int defenseLine, int roomIndex = 1)
    {
        int total = 0;
        for (int wave = 1; wave <= CampaignWavesPerRoom; wave++)
            total += GetCampaignWaveEnemyCount(levelNumber, defenseLine, roomIndex, wave);
        return total;
    }

    public int GetMaximumEnemyTypesForWave(int defenseLine, int waveNumber)
    {
        int safeWave = Mathf.Clamp(waveNumber, 1, CampaignWavesPerRoom);
        return safeWave;
    }

    public static int GetCampaignWaveEnemyLimit(int defenseLine)
    {
        switch (Mathf.Clamp(defenseLine, 1, 4))
        {
            case 1: return 7;
            default: return LevelGenerator.BossPressureStartingEnemies;
        }
    }

    private Dictionary<int, int> BuildCampaignWavePlan(int levelNumber)
    {
        var result = new Dictionary<int, int>(128);
        for (int line = 1; line <= 4; line++)
        {
            for (int room = 1; room <= line * 2 - 1; room++)
            {
                if (!IsCampaignRoomActive(levelNumber, line, room)) continue;
                if (line >= 2)
                {
                    // Project all five finite waves without kills. Actual waves
                    // wait for space and recalculate from live room occupancy.
                    int projectedEnemies = LevelGenerator.BossPressureStartingEnemies;
                    for (int wave = 2; wave <= CampaignWavesPerRoom; wave++)
                    {
                        int count = LevelGenerator.GetBossPressureEnemyCount(projectedEnemies);
                        result[CampaignWaveKey(line, room, wave)] = count;
                        projectedEnemies += count;
                    }
                    result[CampaignWaveKey(line, room, 1)] = LevelGenerator.BossPressureStartingEnemies;
                    continue;
                }

                int roomTotal = RandomRoomEnemyTotal(line);
                int[] waves = DistributeRoomTotal(roomTotal, GetCampaignWaveEnemyLimit(line));
                for (int wave = 1; wave <= CampaignWavesPerRoom; wave++)
                    result[CampaignWaveKey(line, room, wave)] = waves[wave - 1];
            }
        }
        return result;
    }

    private static int[] DistributeRoomTotal(int roomTotal, int perWaveLimit)
    {
        int safeTotal = Mathf.Max(0, roomTotal);
        int safeLimit = Mathf.Max(1, perWaveLimit);
        if (safeTotal > CampaignWavesPerRoom * safeLimit)
            throw new InvalidOperationException("Campaign room budget " + safeTotal
                + " exceeds five-wave capacity " + (CampaignWavesPerRoom * safeLimit) + ".");
        float[] weights = { 0.20f, 0.20f, 0.20f, 0.20f, 0.20f };
        var counts = new int[CampaignWavesPerRoom];
        int remaining = safeTotal;
        for (int wave = 0; wave < counts.Length; wave++)
        {
            counts[wave] = Mathf.FloorToInt(safeTotal * weights[wave]);
            remaining -= counts[wave];
        }
        while (remaining > 0)
        {
            int bestWave = -1;
            float bestRemainder = float.NegativeInfinity;
            for (int wave = 0; wave < counts.Length; wave++)
            {
                if (counts[wave] >= safeLimit) continue;
                float remainder = safeTotal * weights[wave] - counts[wave];
                if (remainder > bestRemainder)
                {
                    bestWave = wave;
                    bestRemainder = remainder;
                }
            }
            if (bestWave < 0)
                throw new InvalidOperationException("Campaign waves have no room for the enemy budget.");
            counts[bestWave]++;
            remaining--;
        }
        return counts;
    }

    private static int RandomRoomEnemyTotal(int defenseLine)
    {
        switch (Mathf.Clamp(defenseLine, 1, 3))
        {
            case 1:
                return UnityEngine.Random.Range(FirstLineMinimumEnemyTotal,
                    FirstLineMaximumEnemyTotal + 1);
            case 2:
                return UnityEngine.Random.Range(SecondLineMinimumEnemyTotal,
                    SecondLineMaximumEnemyTotal + 1);
            default:
                return UnityEngine.Random.Range(ThirdLineMinimumEnemyTotal,
                    ThirdLineMaximumEnemyTotal + 1);
        }
    }

    private bool IsCampaignRoomActive(int levelNumber, int defenseLine, int roomIndex)
    {
        return IsRoomActive(levelNumber, defenseLine, roomIndex);
    }

    private static int CampaignWaveKey(int defenseLine, int roomIndex, int waveNumber)
    {
        return Mathf.Clamp(defenseLine, 1, 4) * 100
            + Mathf.Clamp(roomIndex, 1, 7) * 10
            + Mathf.Clamp(waveNumber, 1, CampaignWavesPerRoom);
    }

    private static int GetScriptedEnemyReserve(int levelNumber)
    {
        // The mandatory first-level hedgehog lesson adds a real six-enemy
        // cluster on top of the normal per-room wave budget.
        return Mathf.Clamp(levelNumber, 1, MaxLevel) == 1 ? 6 : 0;
    }

    public void ApplyCampaignWaveRules()
    {
        NormalizeLevelData();
    }

    public bool IsRoomActive(int defenseLine, int roomIndex)
    {
        return IsRoomActive(currentLevel, defenseLine, roomIndex);
    }

    public bool IsRoomActive(int levelNumber, int defenseLine, int roomIndex)
    {
        if (defenseLine < 1 || defenseLine > 4 || roomIndex < 1
            || roomIndex > defenseLine * 2 - 1) return false;
        return defenseLine <= FinalCampaignLineForLevel(levelNumber);
    }

    // Levels 1-2 of a location use 4 rooms (lines 1-2), levels 3-4 use 9
    // rooms (lines 1-3), and only the location finale uses all 16 rooms.
    public static int FinalCampaignLineForLevel(int levelNumber)
    {
        int local = LocalLevelFor(levelNumber);
        if (local <= 2) return 2;
        if (local <= 4) return 3;
        return 4;
    }

    public int FinalCampaignLine => FinalCampaignLineForLevel(currentLevel);
    public int BossEscortMinimum => Mathf.Max(0, bossEscortMinimum);
    public int BossEscortMaximum => Mathf.Max(BossEscortMinimum, bossEscortMaximum);
    public int BossRoomEnemyCap => Mathf.Max(0, bossRoomEnemyCap);

    public bool IsBossRoom(int levelNumber, int defenseLine, int roomIndex)
    {
        return IsBossRoomPosition(defenseLine, roomIndex);
    }

    public static bool IsBossRoomPosition(int defenseLine, int roomIndex)
        => defenseLine >= 2 && defenseLine <= 4 && roomIndex == defenseLine;

    public bool IsMirageRoom(int levelNumber, int defenseLine, int roomIndex)
    {
        return GetBossKind(levelNumber, defenseLine, roomIndex) == BossKind.Mirage;
    }

    public BossKind GetBossKind(int levelNumber, int defenseLine, int roomIndex)
    {
        if (!IsBossRoomPosition(defenseLine, roomIndex)) return BossKind.None;
        levelNumber = Mathf.Clamp(levelNumber, 1, MaxLevel);
        GameLevelBalance level = FindLevel(levelNumber);
        List<BossKind> bosses = level != null && HasValidBosses(level)
            ? level.bosses : DefaultBosses(levelNumber);
        return bosses[defenseLine - 2];
    }

    public IReadOnlyList<int> GetSpecialistSlots(int levelNumber)
    {
        GameLevelBalance level = FindLevel(Mathf.Clamp(levelNumber, 1, MaxLevel));
        return level != null && level.specialistSlots != null && level.specialistSlots.Count > 0
            ? level.specialistSlots : DefaultSpecialistSlots(levelNumber);
    }

    private static List<int> DefaultSpecialistSlots(int levelNumber)
    {
        switch (LocalLevelFor(levelNumber))
        {
            case 1: return new List<int> { 1, 2 };
            case 2: return new List<int> { 2, 3 };
            case 3: return new List<int> { 1, 3, 4 };
            case 4: return new List<int> { 2, 4, 1 };
            default: return new List<int> { 1, 2, 3, 4 };
        }
    }

    private static List<BossKind> DefaultBosses(int levelNumber)
    {
        int location = LocationIndexForLevel(levelNumber);
        int local = LocalLevelFor(levelNumber);
        if (location == 1)
        {
            switch (local)
            {
                case 1: return new List<BossKind> { BossKind.GreenJudge, BossKind.HiveHollow, BossKind.Lich };
                case 2: return new List<BossKind> { BossKind.HiveHollow, BossKind.VolleyEater, BossKind.BellRinger };
                case 3: return new List<BossKind> { BossKind.Lich, BossKind.Impostor, BossKind.HiveHollow };
                case 4: return new List<BossKind> { BossKind.BellRinger, BossKind.ThreeSealIdol, BossKind.GreenJudge };
                default: return new List<BossKind> { BossKind.KnotBinder, BossKind.VolleyEater, BossKind.BoneDragon };
            }
        }
        if (location == 2)
        {
            switch (local)
            {
                case 1: return new List<BossKind> { BossKind.Lich, BossKind.ThreeSealIdol, BossKind.Mirage };
                case 2: return new List<BossKind> { BossKind.BellRinger, BossKind.VolleyEater, BossKind.ThreeSealIdol };
                case 3: return new List<BossKind> { BossKind.Lich, BossKind.Mirage, BossKind.Impostor };
                case 4: return new List<BossKind> { BossKind.ThreeSealIdol, BossKind.Impostor, BossKind.KnotBinder };
                default: return new List<BossKind> { BossKind.Mirage, BossKind.KnotBinder, BossKind.GhostlySorrow };
            }
        }
        if (location == 3)
        {
            switch (local)
            {
                case 1: return new List<BossKind> { BossKind.BellRinger, BossKind.ThreeSealIdol, BossKind.DrownedMini };
                case 2: return new List<BossKind> { BossKind.KnotBinder, BossKind.VolleyEater, BossKind.Impostor };
                case 3: return new List<BossKind> { BossKind.BellRinger, BossKind.Impostor, BossKind.DrownedMini };
                case 4: return new List<BossKind> { BossKind.ThreeSealIdol, BossKind.VolleyEater, BossKind.KnotBinder };
                default: return new List<BossKind> { BossKind.DrownedMini, BossKind.Impostor, BossKind.PoisonousGloom };
            }
        }
        // The other locations use existing universal bosses until their own dragons are authored.
        BossKind[] universal = { BossKind.BellRinger, BossKind.KnotBinder,
            BossKind.VolleyEater, BossKind.ThreeSealIdol, BossKind.Impostor };
        int rotation = local + location - 5;
        return new List<BossKind> { universal[rotation % 5], universal[(rotation + 2) % 5], universal[(rotation + 4) % 5] };
    }

    private static bool HasValidBosses(GameLevelBalance level)
    {
        if (level.bosses == null || level.bosses.Count != 3) return false;
        for (int i = 0; i < 3; i++)
        {
            BossKind kind = level.bosses[i];
            if (kind == BossKind.None || kind == BossKind.BoneDragonMini
                || level.bosses.IndexOf(kind) != i) return false;
            int dragonLocation = kind == BossKind.BoneDragon ? 1 : kind == BossKind.GhostlySorrow ? 2
                : kind == BossKind.PoisonousGloom ? 3 : 0;
            if (dragonLocation > 0 && (LocationIndexForLevel(level.levelNumber) != dragonLocation
                || LocalLevelFor(level.levelNumber) != LevelsPerLocation || i != 2)) return false;
        }
        return true;
    }

    public bool AdvanceLevel()
    {
        // Victory is normally recorded by GameManager as soon as it happens.
        // Keep this call as a safe fallback for old scenes and saved button bindings.
        if (!GameRunMode.IsDeveloperRun) RecordLevelCompletion(currentLevel);

        if (!HasNextLevel)
        {
            return false;
        }

        currentLevel++;
        if (!GameRunMode.IsDeveloperRun) UnlockLevel(currentLevel);
        PlayerPrefs.SetInt(MenuSelectedLevelKey, currentLevel);
        PlayerPrefs.SetInt(RequestedLevelKey, currentLevel);
        if (!GameRunMode.IsDeveloperRun)
            PlayerPrefs.SetInt(LegacyCurrentLevelKey, currentLevel);
        PlayerPrefs.Save();
        return true;
    }

    public static void RecordLevelCompletion(int completedLevel)
    {
        int safeCompletedLevel = Mathf.Clamp(completedLevel, 1, MaxLevel);
        int newlyUnlockedLevel = Mathf.Min(safeCompletedLevel + 1, MaxLevel);

        UnlockLevel(newlyUnlockedLevel);

        // Preserve the legacy value for old builds/saves that still used it as
        // the highest reached level instead of HighestUnlockedLevel.
        int legacyLevel = PlayerPrefs.GetInt(LegacyCurrentLevelKey, 1);
        PlayerPrefs.SetInt(
            LegacyCurrentLevelKey,
            Mathf.Clamp(Mathf.Max(legacyLevel, newlyUnlockedLevel), 1, MaxLevel));
        PlayerPrefs.Save();
    }

    public void ResetCampaign()
    {
        currentLevel = 1;
        PlayerPrefs.SetInt(MenuSelectedLevelKey, currentLevel);
        PlayerPrefs.DeleteKey(RequestedLevelKey);
        PlayerPrefs.Save();
    }

    public static bool SelectMenuLevel(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > (GameRunMode.DeveloperModeEnabled
            ? MaxLevel : HighestUnlockedLevel))
        {
            return false;
        }

        PlayerPrefs.SetInt(MenuSelectedLevelKey, levelNumber);
        PlayerPrefs.Save();
        return true;
    }

    public static void PrepareSelectedLevel()
    {
        PrepareLevel(SelectedMenuLevel);
    }

    public static void PrepareLevel(int levelNumber)
    {
        PlayerPrefs.SetInt(RequestedLevelKey, Mathf.Clamp(levelNumber, 1, MaxLevel));
        PlayerPrefs.Save();
    }

    private static int ConsumeRequestedLevel()
    {
        int requestedLevel = PlayerPrefs.GetInt(RequestedLevelKey, 1);
        PlayerPrefs.DeleteKey(RequestedLevelKey);
        PlayerPrefs.Save();
        return Mathf.Clamp(requestedLevel, 1, MaxLevel);
    }

    private static void UnlockLevel(int levelNumber)
    {
        int highestLevel = Mathf.Max(HighestUnlockedLevel, levelNumber);
        PlayerPrefs.SetInt(HighestUnlockedLevelKey, Mathf.Clamp(highestLevel, 1, MaxLevel));
    }

    private GameLevelBalance FindLevel(int levelNumber)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] != null && levels[i].levelNumber == levelNumber)
            {
                return levels[i];
            }
        }

        int fallback = Mathf.Clamp(levelNumber - 1, 0, levels.Count - 1);
        return levels.Count > 0 ? levels[fallback] : null;
    }

    private void NormalizeLevelData()
    {
        campaignWavePlans.Clear();
        List<GameLevelBalance> defaults = CreateDefaultLevels();
        if (levels == null) levels = new List<GameLevelBalance>();
        levels.RemoveAll(level => level == null || level.levelNumber < 1 || level.levelNumber > MaxLevel);
        for (int i = 0; i < defaults.Count; i++)
        {
            GameLevelBalance fallback = defaults[i];
            GameLevelBalance level = levels.Find(candidate => candidate.levelNumber == fallback.levelNumber);
            if (level == null)
            {
                levels.Add(fallback);
                continue;
            }
            if (level.defenseLines == null || level.defenseLines.Count == 0)
                level.defenseLines = fallback.defenseLines;
            for (int line = 0; line < level.defenseLines.Count; line++)
            {
                DefenseLineBalance balance = level.defenseLines[line];
                balance.additionalWaves = CampaignWavesPerRoom - 1;
                level.defenseLines[line] = balance;
            }
            level.activeFourthLineRooms = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
            level.bossRoomIndex = 4;
            if (!HasValidBosses(level)) level.bosses = fallback.bosses;
            if (level.specialistSlots == null) level.specialistSlots = new List<int>();
            level.specialistSlots.RemoveAll(slot => slot < 1 || slot > 4);
            if (level.specialistSlots.Count == 0) level.specialistSlots = fallback.specialistSlots;
            // Migrate old side-room Mirage encounters to the only outer boss room.
            if (level.mirageRoomIndices != null && level.mirageRoomIndices.Count > 0)
                level.mirageRoomIndices = new List<int> { 4 };
        }
        levels.Sort((left, right) => left.levelNumber.CompareTo(right.levelNumber));
        if (zombieSpawnRules == null) zombieSpawnRules = CreateDefaultZombieRules();
        if (armoredSkeletonSpawnRules == null || armoredSkeletonSpawnRules.Count == 0)
            armoredSkeletonSpawnRules = CreateDefaultArmoredSkeletonRules();
    }

    private static List<ZombieSpawnRule> CreateDefaultZombieRules()
    {
        return new List<ZombieSpawnRule>
        {
            new ZombieSpawnRule {
                firstLevel = 2,
                lastLevel = MaxLevel,
                defenseLine = 0,
                roomIndex = 0,
                zombiesPerWave = 1
            }
        };
    }

    private static List<ArmoredSkeletonSpawnRule> CreateDefaultArmoredSkeletonRules()
    {
        return new List<ArmoredSkeletonSpawnRule>
        {
            new ArmoredSkeletonSpawnRule {
                firstLevel = LevelsPerLocation + 1,
                lastLevel = MaxLevel,
                defenseLine = 0,
                roomIndex = 0,
                armoredSkeletonsPerWave = 1
            }
        };
    }

    private static List<GameLevelBalance> CreateDefaultLevels()
    {
        return new List<GameLevelBalance>
        {
            Level(1, 4, 2, 3, 2, 2, 2, 2, 2),
            Level(2, 5, 2, 4, 2, 3, 2, 2, 2),
            Level(3, 5, 2, 4, 2, 3, 2, 3, 2),
            Level(4, 6, 2, 5, 2, 4, 2, 3, 2),
            Level(5, 6, 2, 5, 2, 4, 2, 4, 2),

            // Мрачная лощина повторяет понятную схему открытия комнат, но
            // начинает чуть плотнее. Лич здесь входит в обычный состав волны.
            WithMirageRooms(Level(6, 5, 2, 4, 2, 3, 2, 3, 2), 4),
            Level(7, 6, 2, 5, 2, 4, 2, 3, 2),
            Level(8, 6, 2, 5, 2, 4, 2, 4, 2),
            Level(9, 7, 2, 6, 2, 5, 2, 4, 2),
            Level(10, 7, 2, 6, 2, 5, 2, 5, 2),

            // The Drowned Bog keeps its own ordinary wave roster.
            // Drowned mini-bosses share this location with universal bosses.
            Level(11, 5, 2, 4, 2, 3, 2, 3, 2),
            Level(12, 6, 2, 5, 2, 4, 2, 3, 2),
            Level(13, 6, 2, 5, 2, 4, 2, 4, 2),
            Level(14, 7, 2, 6, 2, 5, 2, 4, 2),
            Level(15, 7, 2, 6, 2, 5, 2, 5, 2),

            // Forgotten Bay uses the established room progression for now.
            // Its finale uses universal bosses until its own dragon is authored.
            Level(16, 5, 2, 4, 2, 3, 2, 3, 2),
            Level(17, 6, 2, 5, 2, 4, 2, 3, 2),
            Level(18, 6, 2, 5, 2, 4, 2, 4, 2),
            Level(19, 7, 2, 6, 2, 5, 2, 4, 2),
            Level(20, 7, 2, 6, 2, 5, 2, 5, 2),

            // Snowy Haze mirrors the existing five-level branch layout.
            // Its finale uses universal bosses until its own dragon is authored.
            Level(21, 5, 2, 4, 2, 3, 2, 3, 2),
            Level(22, 6, 2, 5, 2, 4, 2, 3, 2),
            Level(23, 6, 2, 5, 2, 4, 2, 4, 2),
            Level(24, 7, 2, 6, 2, 5, 2, 4, 2),
            Level(25, 7, 2, 6, 2, 5, 2, 5, 2),

            // Fiery Gehenna uses its own ordinary roster and universal bosses.
            Level(26, 5, 2, 4, 2, 3, 2, 3, 2),
            Level(27, 6, 2, 5, 2, 4, 2, 3, 2),
            Level(28, 6, 2, 5, 2, 4, 2, 4, 2),
            Level(29, 7, 2, 6, 2, 5, 2, 4, 2),
            Level(30, 7, 2, 6, 2, 5, 2, 5, 2)
        };
    }

    private static GameLevelBalance Level(
        int levelNumber,
        int line1Enemies, int line1Waves,
        int line2Enemies, int line2Waves,
        int line3Enemies, int line3Waves,
        int line4Enemies, int line4Waves)
    {
        GameLevelBalance level = new GameLevelBalance(
            levelNumber,
            new[] { 1, 2, 3, 4, 5, 6, 7 },
            4,
            new DefenseLineBalance(line1Enemies, line1Waves),
            new DefenseLineBalance(line2Enemies, line2Waves),
            new DefenseLineBalance(line3Enemies, line3Waves),
            new DefenseLineBalance(line4Enemies, line4Waves));
        level.bosses = DefaultBosses(levelNumber);
        level.specialistSlots = DefaultSpecialistSlots(levelNumber);
        for (int i = 0; i < level.defenseLines.Count; i++)
        {
            DefenseLineBalance balance = level.defenseLines[i];
            balance.additionalWaves = CampaignWavesPerRoom - 1;
            level.defenseLines[i] = balance;
        }
        return level;
    }

    private static GameLevelBalance WithMirageRooms(GameLevelBalance level, params int[] roomIndices)
    {
        level.mirageRoomIndices = new List<int>(roomIndices);
        return level;
    }
}
