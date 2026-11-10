using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    private RoomEnemyIndex enemyIndex;
    public void InvalidateEnemyQueries() => enemyIndex?.Invalidate();
    public void CollectNearbyEnemies(Vector2 center, float radius, bool square, Brick excluded, List<Brick> result)
    {
        if (enemyIndex == null) enemyIndex = new RoomEnemyIndex(this);
        enemyIndex.Collect(center, radius, square, excluded, result);
    }
    public const int MaxColumns = 8;
    public const int BossPressureStartingEnemies = 6;
    public const int BossPressureEnemyLimit = 15;
    [SerializeField, HideInInspector] private Vector2Int size = new Vector2Int(MaxColumns, Stage.MaxBattlefieldHeight);
    [SerializeField, Range(1, Stage.MaxBattlefieldHeight)] private int spawnRows = 3;
    public GameObject brickPrefab;
    [SerializeField, Tooltip("Optional override. Uses Resources/Enemies/Zombie when empty.")]
    private GameObject zombiePrefab;
    [SerializeField, Tooltip("Optional override for the Lich in central boss rooms 2-2, 3-3 and 4-4.")]
    private GameObject lichPrefab;
    [SerializeField, Tooltip("Optional override. In Gloomy Hollow, one ordinary 1x1 Lich replaces a base enemy in each wave.")]
    private GameObject regularLichPrefab;
    [SerializeField, Tooltip("Optional override. Uses Resources/Enemies/ArmoredSkeleton when empty.")]
    private GameObject armoredSkeletonPrefab;
    [SerializeField, Tooltip("Optional override. Uses Resources/Enemies/Ghost when empty.")]
    private GameObject ghostPrefab;
    [SerializeField, Tooltip("Optional override. Drowned Bog waves use Resources/Enemies/SwampDemon when empty.")]
    private GameObject swampDemonPrefab;
    [SerializeField, Tooltip("Optional override. Drowned Bog waves use Resources/Enemies/BogPustulant when empty.")]
    private GameObject bogPustulantPrefab;
    [SerializeField, Tooltip("Optional override for the Twilight Thicket final dragon, only on level 1-5.")]
    private GameObject boneDragonBossPrefab;
    [SerializeField, Tooltip("Optional override for the Gloomy Hollow final dragon, only on level 2-5.")]
    private GameObject ghostlySorrowBossPrefab;
    [SerializeField, Tooltip("Optional override for the Drowned Bog final dragon, only on level 3-5.")]
    private GameObject poisonousGloomBossPrefab;
    [SerializeField, Tooltip("Optional override for Drowned Bog central boss rooms.")]
    private GameObject drownedMiniBossPrefab;
    [SerializeField, Tooltip("Optional override for Gloomy Hollow central boss rooms.")]
    private GameObject boneDragonMiniBossPrefab;
    [SerializeField, Tooltip("Optional override for Mirage rooms in Gloomy Hollow.")]
    private GameObject mirageBossPrefab;
    [SerializeField] private Settings m_Settings;
    [SerializeField] public List<GameObject> EnemyCollection = new List<GameObject>();
    private readonly List<Brick> stepSnapshot = new List<Brick>();
    private readonly HashSet<Brick> stunnedThisTurn = new HashSet<Brick>();
    private readonly List<Vector3> freeCells = new List<Vector3>();
    private readonly List<GameObject> waveCandidates = new List<GameObject>(6);
    private readonly List<GameObject> waveComposition = new List<GameObject>(16);
    // Each row is an authored 1x1 ordinary enemy followed by four specialists.
    // Location one keeps the scene's editable brickPrefab as its ordinary type.
    private static readonly string[,] LocationEnemyPaths = {
        { null, "ForestBarkGuard", "Zombie", "ForestThornling", "ForestLeafEater" },
        { "GloomSkull", "Ghost", "ArmoredSkeleton", "LichRegular", "ShadowWeaver" },
        { "SwampDemon", "BogPustulant", "BogLeech", "BogMossHealer", "BogTinnik" },
        { "BayDeepOne", "BayShellback", "BayTideHerald", "BayPuffer", "BaySaltbearer" },
        { "SnowSkull", "SnowIceface", "SnowWisp", "SnowRunesmith", "SnowDoppel" },
        { "FireCoalMask", "FireSlag", "FireEmber", "FireAshCollector", "FireBrander" }
    };
    private bool initialized;
    private int spawnedWaveCount;
    private int defenseLine = 1;
    private int roomIndex = 1;
    private RoomDarkness darkness;
    private bool campaignBossRoom;
    private Brick campaignBoss;
    private bool breachRequested;
    private readonly List<Brick> breachAttackers = new List<Brick>();

    public RoomDarkness Darkness => darkness != null ? darkness : darkness = GetComponentInParent<RoomDarkness>(true);

    public bool HasEnemies => EnemyCollection.Count > 0;
    public bool HasLivingEnemies
    {
        get
        {
            for (int i = 0; i < EnemyCollection.Count; i++)
            {
                GameObject enemy = EnemyCollection[i];
                if (enemy != null && enemy.TryGetComponent(out Brick brick) && !brick.IsDefeated)
                    return true;
            }
            return false;
        }
    }
    public int LivingEnemyCount
    {
        get
        {
            int count = 0;
            foreach (GameObject enemy in EnemyCollection)
                if (enemy != null && enemy.TryGetComponent(out Brick brick)
                    && !brick.IsDefeated && brick.levelGenerator == this) count++;
            return count;
        }
    }
    public int DefenseLine => defenseLine;
    public int RoomIndex => roomIndex;
    public int RoomId => DefenseFront.IdFor(defenseLine, roomIndex);
    public int SpawnedWaveCount => spawnedWaveCount;
    public bool IsInitialized => initialized;
    public bool IsLost { get; private set; }
    private static ClockSiegeDirector ClockDirector => GameManager.Instance != null
        ? GameManager.Instance.Clock : null;
    // In the clock siege only the fronts the director keeps open are endless.
    public bool IsEndlessSurvivalSource => initialized && GameManager.Instance != null
        && (ClockDirector != null
            ? ClockDirector.IsFrontSpawning(RoomId)
            : defenseLine == 4 && GameManager.Instance.IsSurvivalFinalLineActive);
    public bool IsCompleted => initialized && !IsLost && !HasEnemies && RemainingWaves == 0;
    public bool IsCampaignBossRoom => campaignBossRoom;
    public bool HasLivingCampaignBoss => campaignBossRoom && campaignBoss != null
        && !campaignBoss.IsDefeated;
    public int RemainingWaves
    {
        get
        {
            if (IsEndlessSurvivalSource) return 1;
            return m_Settings != null ? Mathf.Max(0, m_Settings._enemyAddWaves) : 0;
        }
    }
    public int Columns => Mathf.Clamp(size.x, 1, MaxColumns);
    public float OrdinaryEnemyBaseHealth => brickPrefab != null
        && brickPrefab.TryGetComponent(out Brick template) ? Mathf.Max(1f, template.hp) : 100f;
    public int Rows => Mathf.Clamp(size.y, 1, Stage.MaxBattlefieldHeight);
    public int SpawnRows => Mathf.Clamp(spawnRows, 1, Rows);
    public int PressureRows => defenseLine <= 3
        ? Mathf.Clamp(8 - defenseLine, 1, Rows)
        : SpawnRows;
    public int SafeReceivingRows => Mathf.Max(1, Rows - 1);
    public int OccupiedEnemyCells
    {
        get
        {
            int occupied = 0;
            for (int i = 0; i < EnemyCollection.Count; i++)
            {
                GameObject enemy = EnemyCollection[i];
                if (enemy == null || !enemy.TryGetComponent(out Brick brick)
                    || !brick.OccupiesGrid) continue;
                occupied += brick.OccupiedCellCount;
            }
            return occupied;
        }
    }
    // The warning includes large enemies and bosses now that they can breach.
    public int ClosestBreachInTurns
    {
        get
        {
            if (!initialized || IsLost) return int.MaxValue;
            int closest = int.MaxValue;
            foreach (GameObject enemy in EnemyCollection)
            {
                if (enemy == null || !enemy.TryGetComponent(out Brick brick)
                    || brick.IsDefeated
                    || (brick.TryGetComponent(out ZombieResurrection grave)
                        && grave.IsAwaitingResurrection)
                    || brick.levelGenerator != this || brick.IsStationary) continue;
                int cells = GetTurnsUntilBreach(brick);
                int steps = brick.TryGetComponent(out BossEnemyTurnController controller)
                    ? controller.MovementCellsPerTurn : 1;
                closest = Mathf.Min(closest, Mathf.CeilToInt((float)cells / steps));
            }
            return closest;
        }
    }
    public bool IsTransferOccupancyLimitReached => OccupiedEnemyCells * 3 >= Columns * Rows * 2;
    // Enemies now breach when they enter the final playable row. Projectile
    // return/destruction zones remain below the physical battlefield edge.
    public float DefenseBoundaryY => transform.position.y - Rows + 0.5f + Stage.BreachBoundaryInset;

    public void ConfigureRoomIdentity(int line, int index)
    {
        defenseLine = Mathf.Clamp(line, 1, 4);
        roomIndex = Mathf.Clamp(index, 1, defenseLine * 2 - 1);
        ConfigureBattlefieldSize(Stage.SizeForLine(defenseLine));
    }

    public void ConfigureBattlefieldSize(Vector2Int battlefieldSize)
    {
        size = new Vector2Int(Mathf.Clamp(battlefieldSize.x, 1, MaxColumns),
            Mathf.Clamp(battlefieldSize.y, 1, Stage.MaxBattlefieldHeight));
    }

    // Activation and turns belong to GameManager, not OnEnable/Update on each room.
    public void InitializeRoom()
    {
        if (initialized) return;
        initialized = true;
        if (m_Settings == null) m_Settings = GetComponent<Settings>();
        if (m_Settings == null) m_Settings = gameObject.AddComponent<Settings>();
        ClockSiegeDirector clock = ClockDirector;
        if (clock != null)
        {
            // Clock fronts have no finite wave budget: the director decides
            // when an open front sends its next wave.
            m_Settings.Configure(clock.BaseEnemiesPerWave, 0);
        }
        else if (LevelProgression.Instance != null)
        {
            LevelProgression.DefenseLineBalance balance = LevelProgression.Instance.GetBalance(defenseLine);
            m_Settings.Configure(balance.enemiesPerWave,
                GameManager.Instance != null && GameManager.Instance.IsSurvivalMode
                    ? balance.additionalWaves
                    : LevelProgression.CampaignWavesPerRoom - 1);
        }
        if (GameManager.Instance != null && !GameManager.Instance.rooms.Contains(this))
            GameManager.Instance.rooms.Add(this);
        bool preLiberatedSurvivalRoom = GameManager.Instance != null
            && GameManager.Instance.IsSurvivalMode
            && (clock != null ? defenseLine == 1 || !clock.IsFrontSpawning(RoomId) : defenseLine < 4);
        if (!preLiberatedSurvivalRoom)
        {
            bool campaignBossPressure = defenseLine >= 2
                && (GameManager.Instance == null || !GameManager.Instance.IsSurvivalMode);
            int firstWaveCount = campaignBossPressure
                ? BossPressureStartingEnemies : GetEnemyCountForWave(1);
            bool bossReplacedFirstWave = SpawnStartingBoss();
            if (bossReplacedFirstWave && campaignBossPressure)
                CreateCell(firstWaveCount - 1, 1, true);
            else if (!bossReplacedFirstWave) CreateCell(firstWaveCount, 1);
            spawnedWaveCount = 1;
        }
    }

    private bool SpawnStartingBoss()
    {
        // Clock bosses arrive only through the boss raid event.
        if (ClockDirector != null) return false;
        if (!LevelProgression.IsBossRoomPosition(defenseLine, roomIndex)) return false;
        LevelProgression progression = LevelProgression.Instance;
        bool survival = GameManager.Instance != null && GameManager.Instance.IsSurvivalMode;
        LevelProgression.BossKind kind = progression != null
            ? progression.GetBossKind(progression.CurrentLevel, defenseLine, roomIndex)
            : LevelProgression.BossKind.Lich;
        return SpawnBoss(ResolveBossPrefab(kind), !survival);
    }

    // Clock siege boss raid: any boss kind, in any open front, not tracked as
    // a campaign boss. Escorts follow the common boss escort rule.
    public Brick SpawnRaidBoss(LevelProgression.BossKind kind)
    {
        if (!initialized || IsLost) return null;
        int before = EnemyCollection.Count;
        if (!SpawnBoss(ResolveBossPrefab(kind), false) || EnemyCollection.Count <= before) return null;
        GameObject spawned = EnemyCollection[EnemyCollection.Count - 1];
        GameManager.Instance?.Front?.RefreshRoom(this);
        return spawned != null ? spawned.GetComponent<Brick>() : null;
    }

    // Clock siege events: one ordinary enemy in a free entry cell, with more
    // health and a tint. A stationary target never walks toward the breach.
    public Brick SpawnEventEnemy(float healthMultiplier, Color tint, bool stationary)
    {
        if (!initialized || IsLost) return null;
        GameObject prefab = ResolveOrdinaryEnemyPrefab();
        if (prefab == null) return null;
        FindFreeEntryCells();
        if (freeCells.Count == 0) return null;
        Brick enemy = SpawnEnemy(prefab, freeCells[Random.Range(0, freeCells.Count)],
            Mathf.Max(1, spawnedWaveCount));
        if (enemy == null) return null;
        enemy.ApplyClockEventVariant(healthMultiplier, tint);
        enemy.IsStationary = stationary;
        GameManager.Instance?.Front?.RefreshRoom(this);
        return enemy;
    }

    // Clock siege: a front that starts attacking sends its first wave at once.
    public bool SpawnClockWave()
    {
        if (!initialized || IsLost) return false;
        int waveNumber = spawnedWaveCount + 1;
        if (!CreateCell(GetEnemyCountForWave(waveNumber), waveNumber)) return false;
        spawnedWaveCount++;
        GameManager.Instance?.Front?.RefreshRoom(this);
        return true;
    }

    public int SummonGuardsAround(Brick center, int count)
    {
        GameObject prefab = ResolveOrdinaryEnemyPrefab();
        int summonedCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TrySummonNear(center, prefab, out Brick guard) || guard == null) break;
            summonedCount++;
        }
        if (summonedCount > 0) GameManager.Instance?.Front?.RefreshRoom(this);
        return summonedCount;
    }

    private GameObject ResolveBossPrefab(LevelProgression.BossKind kind)
    {
        GameObject prefab;
        switch (kind)
        {
            case LevelProgression.BossKind.BoneDragon:
                prefab = boneDragonBossPrefab != null ? boneDragonBossPrefab
                    : Resources.Load<GameObject>("Enemies/BoneDragonBoss");
                break;
            case LevelProgression.BossKind.GhostlySorrow:
                prefab = ghostlySorrowBossPrefab != null ? ghostlySorrowBossPrefab
                    : Resources.Load<GameObject>("Enemies/GhostlySorrowBoss");
                break;
            case LevelProgression.BossKind.PoisonousGloom:
                prefab = poisonousGloomBossPrefab != null ? poisonousGloomBossPrefab
                    : Resources.Load<GameObject>("Enemies/PoisonousGloomBoss");
                break;
            case LevelProgression.BossKind.Mirage:
                prefab = mirageBossPrefab != null ? mirageBossPrefab
                    : Resources.Load<GameObject>("Enemies/MirageBoss");
                break;
            case LevelProgression.BossKind.DrownedMini:
                prefab = drownedMiniBossPrefab != null ? drownedMiniBossPrefab
                    : Resources.Load<GameObject>("Enemies/DrownedMiniBoss");
                break;
            case LevelProgression.BossKind.BoneDragonMini:
                prefab = boneDragonMiniBossPrefab != null ? boneDragonMiniBossPrefab
                    : Resources.Load<GameObject>("Enemies/BoneDragonMiniBoss");
                break;
            case LevelProgression.BossKind.HiveHollow:
                prefab = Resources.Load<GameObject>("Enemies/HiveHollowMiniBoss");
                break;
            case LevelProgression.BossKind.GreenJudge:
                prefab = Resources.Load<GameObject>("Enemies/GreenJudgeMiniBoss");
                break;
            case LevelProgression.BossKind.BellRinger:
                prefab = Resources.Load<GameObject>("Enemies/BellRingerBoss");
                break;
            case LevelProgression.BossKind.KnotBinder:
                prefab = Resources.Load<GameObject>("Enemies/KnotBinderBoss");
                break;
            case LevelProgression.BossKind.VolleyEater:
                prefab = Resources.Load<GameObject>("Enemies/VolleyEaterBoss");
                break;
            case LevelProgression.BossKind.ThreeSealIdol:
                prefab = Resources.Load<GameObject>("Enemies/ThreeSealIdolBoss");
                break;
            case LevelProgression.BossKind.Impostor:
                prefab = Resources.Load<GameObject>("Enemies/ImpostorBoss");
                break;
            default:
                prefab = lichPrefab != null ? lichPrefab
                    : Resources.Load<GameObject>("Enemies/LichBoss");
                break;
        }
        return prefab;
    }

    private bool SpawnBoss(GameObject prefab, bool trackCampaignBoss)
    {
        if (prefab == null || !prefab.TryGetComponent(out Brick boss)) return false;
        var grid = new EnemyRoomGrid(this, Mathf.Max(2, SpawnRows));
        List<Vector2Int> cells = grid.FreeAnchors(boss);
        if (cells.Count == 0) return false;
        Vector2Int chosen = cells[0];
        float center = (Columns - boss.Footprint.x) * 0.5f;
        for (int i = 1; i < cells.Count; i++)
            if (Mathf.Abs(cells[i].x - center) < Mathf.Abs(chosen.x - center)) chosen = cells[i];
        Brick spawnedBoss = SpawnEnemy(prefab, grid.Center(chosen, boss.Footprint));
        if (trackCampaignBoss)
        {
            campaignBossRoom = true;
            campaignBoss = spawnedBoss;
        }
        return true;
    }

    private Brick SpawnEnemy(GameObject prefab, Vector3 position, int waveNumber = 1)
    {
        GameObject enemy = Instantiate(prefab, position, Quaternion.identity, transform);
        Brick brick = enemy.GetComponent<Brick>();
        int healthWave = defenseLine >= 2 && GameManager.Instance != null
            && !GameManager.Instance.IsSurvivalMode
            ? Mathf.Min(waveNumber, LevelProgression.CampaignWavesPerRoom) : waveNumber;
        brick.ApplyCampaignWaveHealth(
            LevelProgression.GetCampaignWaveHealthMultiplier(healthWave));
        brick.levelGenerator = this;
        EnemyCollection.Add(enemy);
            InvalidateEnemyQueries(); FrogRider.InvalidateRoomBonuses();
        brick.PlayArrivalAnimation();
        return brick;
    }

    // Called before the king is removed from EnemyCollection. Its defeated
    // footprint is already free, and all three heirs are registered before
    // room-completion checks can run.
    public int SpawnDrownedHeirs(Brick king, GameObject heirPrefab)
    {
        if (!initialized || IsLost || king == null || king.levelGenerator != this
            || heirPrefab == null || !heirPrefab.TryGetComponent(out Brick heir)) return 0;
        int heirCount = defenseLine >= 2
            ? Mathf.Min(3, BossPressureEnemyLimit - LivingEnemyCount) : 3;
        if (heirCount <= 0) return 0;
        var grid = new EnemyRoomGrid(this, SafeReceivingRows);
        List<Vector2Int> candidates = grid.FreeAnchors(heir);
        Vector2Int kingAnchor = grid.Anchor(king.transform.position, king.Footprint);
        Vector2 kingCenter = (Vector2)kingAnchor + (Vector2)king.Footprint * 0.5f;
        candidates.Sort((left, right) =>
        {
            float leftDistance = ((Vector2)left + (Vector2)heir.Footprint * 0.5f - kingCenter).sqrMagnitude;
            float rightDistance = ((Vector2)right + (Vector2)heir.Footprint * 0.5f - kingCenter).sqrMagnitude;
            int distance = leftDistance.CompareTo(rightDistance);
            if (distance != 0) return distance;
            int row = left.y.CompareTo(right.y);
            return row != 0 ? row : left.x.CompareTo(right.x);
        });
        var chosen = new List<Vector2Int>(heirCount);
        if (!TryPlanDrownedHeirs(grid, heir, candidates, 0, heirCount, chosen))
        {
            Debug.LogWarning("The Drowned King has no room for its heirs.", this);
            return 0;
        }
        foreach (Vector2Int cell in chosen)
            SpawnEnemy(heirPrefab, grid.Center(cell, heir.Footprint)).InheritBossHealth(king);
        GameManager.Instance?.Front?.RefreshRoom(this);
        return chosen.Count;
    }

    private static bool TryPlanDrownedHeirs(EnemyRoomGrid grid, Brick heir,
        List<Vector2Int> candidates, int first, int targetCount, List<Vector2Int> chosen)
    {
        if (chosen.Count == targetCount) return true;
        for (int i = first; i < candidates.Count; i++)
        {
            Vector2Int cell = candidates[i];
            if (!grid.CanPlace(cell, heir)) continue;
            grid.Reserve(cell, heir);
            chosen.Add(cell);
            if (TryPlanDrownedHeirs(grid, heir, candidates, i + 1, targetCount, chosen)) return true;
            chosen.RemoveAt(chosen.Count - 1);
            grid.Reserve(cell, heir, false);
        }
        return false;
    }

    public int SpawnTutorialCluster(int requestedCount = 6)
    {
        GameObject ordinaryPrefab = ResolveOrdinaryEnemyPrefab();
        if (!initialized || IsLost || ordinaryPrefab == null || requestedCount <= 0) return 0;
        int placementRows = Mathf.Clamp(Mathf.Max(3, SpawnRows), 1, Rows);
        var grid = new EnemyRoomGrid(this, placementRows);
        List<Vector2Int> candidates = grid.FreeAnchors(Vector2Int.one);
        // Nearest cells to this point form a compact 3x2 group around the
        // upper-middle of the board, ideal for demonstrating hedgehog quills.
        Vector2 target = new Vector2((Columns - 1) * 0.5f,
            Mathf.Min(1f, placementRows - 1));
        candidates.Sort((left, right) =>
        {
            float leftDistance = ((Vector2)left - target).sqrMagnitude;
            float rightDistance = ((Vector2)right - target).sqrMagnitude;
            int distanceOrder = leftDistance.CompareTo(rightDistance);
            if (distanceOrder != 0) return distanceOrder;
            int rowOrder = left.y.CompareTo(right.y);
            return rowOrder != 0 ? rowOrder : left.x.CompareTo(right.x);
        });

        int count = Mathf.Min(requestedCount, candidates.Count);
        for (int i = 0; i < count; i++)
            SpawnEnemy(ordinaryPrefab, grid.Center(candidates[i], Vector2Int.one),
                Mathf.Max(1, spawnedWaveCount));
        GameManager.Instance?.Front?.RefreshRoom(this);
        return count;
    }

    private GameObject ResolveZombiePrefab()
    {
        if (zombiePrefab == null) zombiePrefab = Resources.Load<GameObject>("Enemies/Zombie");
        return zombiePrefab;
    }

    private GameObject ResolveRegularLichPrefab()
    {
        if (regularLichPrefab == null)
            regularLichPrefab = Resources.Load<GameObject>("Enemies/LichRegular");
        return regularLichPrefab;
    }

    private GameObject ResolveArmoredSkeletonPrefab()
    {
        if (armoredSkeletonPrefab == null)
            armoredSkeletonPrefab = Resources.Load<GameObject>("Enemies/ArmoredSkeleton");
        return armoredSkeletonPrefab;
    }

    private GameObject ResolveGhostPrefab()
    {
        if (ghostPrefab == null) ghostPrefab = Resources.Load<GameObject>("Enemies/Ghost");
        return ghostPrefab;
    }

    private GameObject ResolveSwampDemonPrefab()
    {
        if (swampDemonPrefab == null)
            swampDemonPrefab = Resources.Load<GameObject>("Enemies/SwampDemon");
        return swampDemonPrefab;
    }

    private GameObject ResolveBogPustulantPrefab()
    {
        if (bogPustulantPrefab == null)
            bogPustulantPrefab = Resources.Load<GameObject>("Enemies/BogPustulant");
        return bogPustulantPrefab;
    }

    private GameObject ResolveOrdinaryEnemyPrefab()
    {
        int location = LevelProgression.Instance != null
            ? LevelProgression.Instance.CurrentLocationIndex : 1;
        if (location == 1) return brickPrefab;
        if (location == 3 && swampDemonPrefab != null) return swampDemonPrefab;
        GameObject ordinary = LoadLocationEnemy(location, 0);
        return ordinary != null ? ordinary : brickPrefab;
    }

    private static GameObject LoadLocationEnemy(int location, int slot)
    {
        string name = GetLocationEnemyResourceName(location, slot);
        return string.IsNullOrEmpty(name) ? null
            : Resources.Load<GameObject>("Enemies/" + name);
    }

    public static string GetLocationEnemyResourceName(int location, int slot)
    {
        if (location < 1 || location > LocationEnemyPaths.GetLength(0)
            || slot < 0 || slot >= LocationEnemyPaths.GetLength(1)) return null;
        return LocationEnemyPaths[location - 1, slot];
    }

    private GameObject ResolveLocationSpecialist(int location, int slot)
    {
        if (location == 1 && slot == 2) return ResolveZombiePrefab();
        if (location == 2)
        {
            if (slot == 1) return ResolveGhostPrefab();
            if (slot == 2) return ResolveArmoredSkeletonPrefab();
            if (slot == 3) return ResolveRegularLichPrefab();
        }
        if (location == 3 && slot == 1) return ResolveBogPustulantPrefab();
        return LoadLocationEnemy(location, slot);
    }

    private bool HasRegularLichForWave()
    {
        return LevelProgression.Instance != null
            && LevelProgression.Instance.IsGloomyHollow
            && ResolveRegularLichPrefab() != null;
    }

    private int ZombieCountForWave(int totalEnemies)
    {
        if (totalEnemies <= 0 || LevelProgression.Instance == null || ResolveZombiePrefab() == null) return 0;
        return Mathf.Clamp(LevelProgression.Instance.GetZombieCountPerWave(defenseLine, roomIndex),
            0, totalEnemies);
    }

    private int ArmoredSkeletonCountForWave(int totalEnemies)
    {
        if (totalEnemies <= 0 || LevelProgression.Instance == null
            || ResolveArmoredSkeletonPrefab() == null) return 0;
        return Mathf.Clamp(LevelProgression.Instance.GetArmoredSkeletonCountPerWave(
            defenseLine, roomIndex), 0, totalEnemies);
    }

    public int MaximumEnemyTypesForWave(int waveNumber)
    {
        if (LevelProgression.Instance != null)
            return LevelProgression.Instance.GetMaximumEnemyTypesForWave(defenseLine, waveNumber);
        int safeWave = Mathf.Clamp(waveNumber, 1, LevelProgression.CampaignWavesPerRoom);
        return safeWave;
    }

    private int GetEnemyCountForWave(int waveNumber)
    {
        if (ClockDirector != null) return ClockDirector.WaveEnemyCount;
        if (GameManager.Instance != null && GameManager.Instance.IsSurvivalMode)
            return waveNumber <= 1
                ? Mathf.Max(1, m_Settings._enemySet)
                : GameManager.Instance.GetSurvivalWaveEnemyCount(m_Settings._enemySet);
        return LevelProgression.Instance != null
            ? LevelProgression.Instance.GetCampaignWaveEnemyCount(defenseLine, roomIndex,
                Mathf.Clamp(waveNumber, 1, LevelProgression.CampaignWavesPerRoom))
            : Mathf.Max(1, m_Settings._enemySet);
    }

    private void BuildWaveComposition(int totalEnemies, int waveNumber)
    {
        waveComposition.Clear();
        GameObject ordinaryPrefab = ResolveOrdinaryEnemyPrefab();
        if (totalEnemies <= 0 || ordinaryPrefab == null) return;
        int location = LevelProgression.Instance != null
            ? LevelProgression.Instance.CurrentLocationIndex : 1;
        waveCandidates.Clear();
        if (waveNumber >= 2 && waveNumber <= LevelProgression.CampaignWavesPerRoom
            && (totalEnemies == 1 || MaximumEnemyTypesForWave(waveNumber) >= 2))
        {
            LevelProgression progression = LevelProgression.Instance;
            IReadOnlyList<int> slots = progression != null
                ? progression.GetSpecialistSlots(progression.CurrentLevel) : new[] { 1, 2, 3, 4 };
            int rotation = (roomIndex + defenseLine + waveNumber - 4) % slots.Count;
            int typeLimit = totalEnemies == 1 ? 1 : MaximumEnemyTypesForWave(waveNumber) - 1;
            for (int i = 0; i < slots.Count && waveCandidates.Count < typeLimit; i++)
                AddWaveCandidate(ResolveWaveSpecialist(location, slots[(rotation + i) % slots.Count]));
            int local = progression != null ? progression.CurrentLocalLevel : 1;
            float share = 0.2f + local * 0.05f + (waveNumber - 2) * 0.1f;
            int specialistCount = totalEnemies == 1 ? 1
                : Mathf.Clamp(Mathf.RoundToInt(totalEnemies * share), 1, totalEnemies - 1);
            for (int i = 0; i < specialistCount && waveCandidates.Count > 0; i++)
                waveComposition.Add(waveCandidates[i % waveCandidates.Count]);
        }
        while (waveComposition.Count < totalEnemies) waveComposition.Add(ordinaryPrefab);
    }

    private GameObject ResolveWaveSpecialist(int location, int slot)
    {
        GameObject specialist = ResolveLocationSpecialist(location, slot);
        if (specialist != null && specialist.GetComponent<LichSummoner>() != null)
            foreach (GameObject present in EnemyCollection)
                if (present != null && present.TryGetComponent(out Brick existing)
                    && !existing.IsDefeated && present.GetComponent<LichSummoner>() != null)
                {
                    specialist = null;
                    break;
                }
        if (specialist != null && specialist.TryGetComponent(out Brick specialistBrick)
            && specialistBrick.Footprint == Vector2Int.one)
            return specialist;
        return null;
    }

    private void AddWaveCandidate(GameObject prefab)
    {
        if (prefab != null && !waveCandidates.Contains(prefab)) waveCandidates.Add(prefab);
    }

    public bool TrySummonNear(Brick summoner, GameObject prefab)
    {
        return TrySummonNear(summoner, prefab, out _);
    }

    public bool TrySummonNear(Brick summoner, GameObject prefab, out Brick summoned)
    {
        summoned = null;
        if (!initialized || IsLost || summoner == null || summoner.levelGenerator != this
            || prefab == null || !prefab.TryGetComponent(out Brick template)
            || template.Footprint != Vector2Int.one
            || (defenseLine >= 2 && LivingEnemyCount >= BossPressureEnemyLimit)) return false;
        // Never summon directly on or beyond the breach row.
        int safeRows = Mathf.Clamp(Mathf.CeilToInt(transform.position.y - DefenseBoundaryY), 1, Rows);
        var grid = new EnemyRoomGrid(this, safeRows);
        if (!grid.TryFindNearest(summoner, out Vector3 position)) return false;
        summoned = SpawnEnemy(prefab, position, Mathf.Max(1, spawnedWaveCount));
        return summoned != null;
    }

    // Summons 3-4 ordinary enemies per boss turn while the room holds fewer
    // than six enemies besides the boss. Escorts grant no XP. Unique boss
    // summons (Lich skeletons, hive bees, decoys) keep their own rules.
    public int SummonBossEscort(Brick boss)
    {
        if (!initialized || IsLost || boss == null || boss.IsDefeated
            || !boss.UsesBossHealth || boss.levelGenerator != this) return 0;
        GameObject prefab = ResolveOrdinaryEnemyPrefab();
        if (prefab == null) return 0;
        LevelProgression progression = LevelProgression.Instance;
        int minimum = progression != null ? progression.BossEscortMinimum : LevelProgression.DefaultBossEscortMinimum;
        int maximum = progression != null ? progression.BossEscortMaximum : LevelProgression.DefaultBossEscortMaximum;
        int cap = progression != null ? progression.BossRoomEnemyCap : LevelProgression.DefaultBossRoomEnemyCap;
        int others = LivingEnemyCount - 1;
        int requested = Mathf.Min(Random.Range(minimum, Mathf.Max(minimum, maximum) + 1), cap - others);
        int summonedCount = 0;
        for (int i = 0; i < requested; i++)
        {
            if (!TrySummonNear(boss, prefab, out Brick escort) || escort == null) break;
            escort.ConfigureSummonedVariant(0, Color.white);
            summonedCount++;
        }
        if (summonedCount > 0) GameManager.Instance?.Front?.RefreshRoom(this);
        return summonedCount;
    }

    public int SummonSkeletonLineAhead(Brick summoner, GameObject prefab, Color tint)
    {
        if (!initialized || IsLost || summoner == null || summoner.levelGenerator != this
            || prefab == null || !prefab.TryGetComponent(out Brick template)
            || template.Footprint != Vector2Int.one
            || (defenseLine >= 2 && LivingEnemyCount >= BossPressureEnemyLimit)) return 0;
        // The breach row is never a spawn point. If the row ahead is already
        // outside the safe field, use the nearest free row beside/behind the boss.
        int safeRows = Mathf.Clamp(Mathf.CeilToInt(transform.position.y - DefenseBoundaryY), 1, Rows);
        var grid = new EnemyRoomGrid(this, safeRows);
        Vector2Int anchor = grid.Anchor(summoner.transform.position, summoner.Footprint);
        int forwardRow = anchor.y + summoner.LowestOccupiedRowOffset + 1;
        int row = -1;
        for (int candidate = Mathf.Min(forwardRow, safeRows - 1); candidate >= 0; candidate--)
        {
            for (int column = 0; column < Columns; column++)
            {
                if (!grid.CanPlace(new Vector2Int(column, candidate), Vector2Int.one)) continue;
                row = candidate;
                break;
            }
            if (row >= 0) break;
        }
        if (row < 0) return 0;
        int count = 0;
        for (int column = 0; column < Columns; column++)
        {
            if (defenseLine >= 2 && LivingEnemyCount >= BossPressureEnemyLimit) break;
            Vector2Int cell = new Vector2Int(column, row);
            if (!grid.CanPlace(cell, Vector2Int.one)) continue;
            Brick summoned = SpawnEnemy(prefab, grid.Center(cell, Vector2Int.one),
                Mathf.Max(1, spawnedWaveCount));
            if (summoned == null) continue;
            summoned.ConfigureSummonedVariant(0, tint);
            summoned.SetSummonedHealth(1f);
            grid.Reserve(cell, Vector2Int.one);
            count++;
        }
        GameManager.Instance?.Front?.RefreshRoom(this);
        return count;
    }

    private bool CreateCell(int requestedEnemyCount, int waveNumber, bool ordinaryEnemiesOnly = false)
    {
        GameObject ordinaryPrefab = ResolveOrdinaryEnemyPrefab();
        if (ordinaryPrefab == null) return false;
        FindFreeEntryCells();
        int safeRequestedCount = Mathf.Max(0, requestedEnemyCount);
        if (freeCells.Count == 0 && safeRequestedCount > 0) return false;
        bool survival = GameManager.Instance != null && GameManager.Instance.IsSurvivalMode;
        // Campaign budgets are exact. If the entry area is crowded, keep the
        // complete wave pending until every enemy in it can be placed.
        if (!survival && freeCells.Count < safeRequestedCount) return false;
        bool clock = ClockDirector != null;
        // Clock waves already grow on their own schedule; only free cells limit them.
        float densityMultiplier = survival && !clock ? EnemyHard() : 1f;
        int count = Mathf.Min(freeCells.Count,
            Mathf.RoundToInt(safeRequestedCount * densityMultiplier));
        if (safeRequestedCount > 0 && count == 0 && freeCells.Count > 0) count = 1;
        // Endless clock waves keep cycling through the specialist mixes of
        // waves 2-5 instead of falling back to ordinary enemies only.
        int compositionWave = clock && waveNumber > LevelProgression.CampaignWavesPerRoom
            ? 2 + (waveNumber - 2) % (LevelProgression.CampaignWavesPerRoom - 1)
            : waveNumber;
        if (!ordinaryEnemiesOnly) BuildWaveComposition(count, compositionWave);
        for (int i = 0; i < count; i++)
        {
            int cell = Random.Range(0, freeCells.Count);
            SpawnEnemy(ordinaryEnemiesOnly ? ordinaryPrefab : waveComposition[i],
                freeCells[cell], waveNumber);
            freeCells.RemoveAt(cell);
        }
        return true;
    }

    public static int GetBossPressureEnemyCount(int livingEnemies)
    {
        int vacancies = BossPressureEnemyLimit
            - Mathf.Clamp(livingEnemies, 0, BossPressureEnemyLimit);
        return Mathf.Min(vacancies, Mathf.CeilToInt(
            BossPressureStartingEnemies * vacancies / (float)BossPressureEnemyLimit));
    }

    private bool SpawnBossPressureWave(int waveNumber)
    {
        // Travelling enemies enter after spawning. Leave capacity for their
        // existing cohort instead of refilling the room before it can arrive.
        if (GameManager.Instance?.Front?.HasPendingPressureForRoom(RoomId) == true)
            return false;
        int requested = GetBossPressureEnemyCount(LivingEnemyCount);
        if (requested == 0) return false;
        GameObject ordinaryPrefab = ResolveOrdinaryEnemyPrefab();
        if (ordinaryPrefab == null) return false;
        FindFreeEntryCells();
        int count = Mathf.Min(requested, freeCells.Count);
        if (count == 0) return false;
        BuildWaveComposition(count, waveNumber);
        for (int i = 0; i < count; i++)
        {
            int cell = Random.Range(0, freeCells.Count);
            SpawnEnemy(waveComposition[i],
                freeCells[cell], waveNumber);
            freeCells.RemoveAt(cell);
        }
        return true;
    }

    public void StepEnemy()
    {
        if (!initialized || IsLost) return;
        stepSnapshot.Clear();
        stunnedThisTurn.Clear();
        foreach (GameObject enemy in EnemyCollection)
            if (enemy != null && enemy.TryGetComponent(out Brick brick)) stepSnapshot.Add(brick);
        // Resolve the front of every column first. A moving enemy then vacates its
        // old cell for the follower, while stunned or otherwise stationary enemies
        // continue to reserve their complete footprint.
        stepSnapshot.Sort((left, right) =>
        {
            if (left == null) return right == null ? 0 : 1;
            if (right == null) return -1;
            int byFront = left.LowestCellY.CompareTo(right.LowestCellY);
            return byFront != 0 ? byFront : left.GetInstanceID().CompareTo(right.GetInstanceID());
        });
        foreach (Brick brick in stepSnapshot)
        {
            if (IsLost || GameManager.Instance == null || GameManager.Instance.GameEnded) break;
            if (brick == null) continue;
            EnemyTurnStun stun = brick.GetComponent<EnemyTurnStun>();
            if (stun != null && stun.IsStunned) stunnedThisTurn.Add(brick);
            brick.Step();
        }
    }

    public void SpawnNextWave()
    {
        if (!initialized || (!IsEndlessSurvivalSource && RemainingWaves == 0)) return;
        // An open clock front sends a wave only on its own interval and not
        // during a calm.
        if (ClockDirector != null && !ClockDirector.ShouldSpawnWave(RoomId)) return;
        int waveNumber = spawnedWaveCount + 1;
        GameManager manager = GameManager.Instance;
        if (defenseLine >= 2 && !IsLost && manager != null
            && !manager.IsSurvivalMode)
        {
            if (SpawnBossPressureWave(waveNumber))
            {
                spawnedWaveCount++;
                m_Settings._enemyAddWaves--;
            }
            return;
        }
        int waveEnemyCount = GetEnemyCountForWave(waveNumber);
        bool ordinaryBossReinforcement = campaignBossRoom && HasLivingCampaignBoss;
        if (IsLost)
        {
            // Remaining waves from a lost direction also have to be defeated.
            GameObject ordinaryPrefab = ResolveOrdinaryEnemyPrefab();
            if (ordinaryPrefab == null) return;
            if (!ordinaryBossReinforcement) BuildWaveComposition(waveEnemyCount, waveNumber);
            int cohort = GameManager.Instance.Front.CreatePressureCohort();
            for (int i = 0; i < waveEnemyCount; i++)
            {
                GameObject prefab = ordinaryBossReinforcement ? ordinaryPrefab : waveComposition[i];
                Brick brick = Instantiate(prefab, transform).GetComponent<Brick>();
                brick.ApplyCampaignWaveHealth(
                    LevelProgression.GetCampaignWaveHealthMultiplier(waveNumber));
                GameManager.Instance.Front.SendToInnerRoom(this, brick, 0, cohort);
            }
            spawnedWaveCount++;
            if (!IsEndlessSurvivalSource) m_Settings._enemyAddWaves--;
        }
        else if (CreateCell(waveEnemyCount, waveNumber, ordinaryBossReinforcement))
        {
            spawnedWaveCount++;
            if (!IsEndlessSurvivalSource) m_Settings._enemyAddWaves--;
        }
    }

    public void CompleteEnemyTurn(int turnNumber)
    {
        // Use the pre-movement snapshot, including bosses that just transferred
        // to an inner room. New arrivals/summons do not take an additional turn.
        foreach (Brick brick in stepSnapshot)
            if (brick != null)
            {
                if (brick.TryGetComponent(out WinterEnemyForm winter))
                    winter.CompleteEnemyTurn();
                if (stunnedThisTurn.Contains(brick)) continue;
                if (brick.TryGetComponent(out LichSummoner summoner))
                    summoner.CompleteEnemyTurn(turnNumber);
                if (brick.TryGetComponent(out BossEnemyTurnController boss))
                    boss.CompleteEnemyTurn(turnNumber);
                if (brick.TryGetComponent(out EnemySpecialistTrait specialist))
                    specialist.CompleteEnemyTurn(turnNumber);
                if (brick.TryGetComponent(out ForestMiniBossAbility forestMiniBoss))
                    forestMiniBoss.CompleteEnemyTurn(turnNumber);
                // Every boss also calls ordinary escorts in whichever room it
                // now stands, giving projectiles more targets to ricochet from.
                if (brick.UsesBossHealth && !brick.IsDefeated && brick.levelGenerator != null)
                    brick.levelGenerator.SummonBossEscort(brick);
            }
        stepSnapshot.Clear();
        stunnedThisTurn.Clear();
    }

    // A breach no longer abandons the room. Only the enemy that crossed the
    // final row leaves; residents and pending waves stay where they are.
    public bool ReleaseBreachingEnemy(Brick brick)
    {
        if (brick == null || brick.IsDefeated || brick.levelGenerator != this
            || !EnemyCollection.Remove(brick.gameObject)) return false;
        breachAttackers.Remove(brick);
        InvalidateEnemyQueries(); FrogRider.InvalidateRoomBonuses();
        return true;
    }

    public int GetTurnsUntilBreach(Brick brick)
    {
        if (brick == null) return 0;
        float cells = Mathf.Max(0f, brick.LowestCellY - DefenseBoundaryY);
        return Mathf.Max(0, Mathf.CeilToInt(cells - 0.001f));
    }

    public void RequestBreach(Brick attacker = null)
    {
        if (!initialized || IsLost) return;
        breachRequested = true;
        if (attacker != null && attacker.levelGenerator == this
            && !breachAttackers.Contains(attacker))
            breachAttackers.Add(attacker);
    }

    public void AnimateRequestedBreach()
    {
        if (!breachRequested || IsLost) return;
        for (int i = 0; i < EnemyCollection.Count; i++)
        {
            GameObject enemy = EnemyCollection[i];
            if (enemy == null || !enemy.TryGetComponent(out Brick brick)
                || brick.IsDefeated || brick.levelGenerator != this) continue;
            bool isAttacker = breachAttackers.Contains(brick)
                || brick.LowestCellY <= DefenseBoundaryY + 0.0001f;
            // Rooms are never abandoned, so only the attackers leave the board.
            if (!isAttacker) continue;
            brick.PlayBreachAnimation(true);
        }
    }

    public void ResolveRequestedBreach()
    {
        if (!breachRequested || IsLost) return;
        breachRequested = false;
        Brick[] attackers = breachAttackers.ToArray();
        breachAttackers.Clear();
        DefenseLineRuntime front = GameManager.Instance?.Front;
        if (defenseLine == 1) front?.ResolveMainRoomBreach(this, attackers);
        else front?.BreachRoom(this, attackers);
    }

    public bool TryReturnBreachingEnemy(Brick brick, out Vector3 destination)
    {
        destination = brick != null ? brick.transform.position : transform.position;
        if (!initialized || IsLost || brick == null || brick.IsDefeated
            || brick.levelGenerator != this) return false;

        int returnRows = Mathf.Clamp(3, 1, Rows);
        var grid = new EnemyRoomGrid(this, returnRows);
        List<Vector2Int> candidates = grid.FreeAnchors(brick.Footprint);
        if (candidates.Count > 0)
        {
            Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
            destination = grid.Center(chosen, brick.Footprint);
            return true;
        }

        if (brick.UsesBossHealth)
        {
            // A boss returning from the barricade follows the same no-boss-
            // overlap rule as a boss arriving from another room. Prefer the
            // upper three rows, then the rest of the safe field if necessary.
            if (!TryFindBossLanding(brick, returnRows, out destination, out List<Brick> victims)
                && !TryFindBossLanding(brick, SafeReceivingRows, out destination, out victims))
                return false;
            float stolen = AbsorbVictims(victims, out List<Vector3> soulOrigins);
            brick.AbsorbHealth(stolen);
            foreach (Vector3 source in soulOrigins)
                SoulAbsorptionEffect.Play(source, brick.transform);
            return true;
        }

        // This is only expected when all top rows are completely packed. Keep
        // the rule deterministic and visible instead of leaving the attacker on
        // the barricade to deal damage again without travelling across the room.
        int maxX = Mathf.Max(0, Columns - brick.Footprint.x);
        int maxY = Mathf.Max(0, returnRows - brick.Footprint.y);
        Vector2Int fallback = new Vector2Int(Random.Range(0, maxX + 1),
            Random.Range(0, maxY + 1));
        destination = grid.Center(fallback, brick.Footprint);
        Debug.LogWarning("Верхние три строки переполнены: враг возвращён в случайную "
            + "клетку с возможным пересечением.", this);
        return true;
    }

    public bool RestoreAfterLineClear()
    {
        if (!initialized || !IsLost || HasEnemies || RemainingWaves > 0) return false;
        IsLost = false;
        breachAttackers.Clear();
        return true;
    }

    public bool TryReceiveEnemy(Brick brick)
    {
        if (brick == null) return false;
        return TryReceivePressureGroup(new[] { brick });
    }

    public bool TryReceivePressureGroup(IReadOnlyList<Brick> enemies)
    {
        return TryReceivePressureGroup(enemies, false);
    }

    public bool TryReceivePressureGroup(IReadOnlyList<Brick> enemies, bool useAllSafeRowsIfNeeded)
    {
        if (!initialized || IsLost || enemies == null) return false;
        if (defenseLine >= 2 && LivingEnemyCount + enemies.Count > BossPressureEnemyLimit)
            return false;
        var grid = new EnemyRoomGrid(this, PressureRows);
        bool fits = grid.TryPlan(enemies, out Dictionary<Brick, Vector3> placements);
        if (useAllSafeRowsIfNeeded && !fits)
        {
            // Overflow may use the whole safe part of the room, but never the
            // final breach row. Arrival itself must not count as crossing a line.
            grid = new EnemyRoomGrid(this, SafeReceivingRows);
            fits = grid.TryPlan(enemies, out placements);
        }
        if (!fits) return false;

        foreach (KeyValuePair<Brick, Vector3> placement in placements)
        {
            Brick brick = placement.Key;
            brick.transform.SetParent(transform, true);
            brick.transform.position = placement.Value;
            brick.levelGenerator = this;
            if (!EnemyCollection.Contains(brick.gameObject)) EnemyCollection.Add(brick.gameObject);
            InvalidateEnemyQueries(); FrogRider.InvalidateRoomBonuses();
            brick.gameObject.SetActive(true);
            brick.PlayArrivalAnimation();
        }
        return true;
    }

    // Called only for a boss whose ordinary cohort could not fit. Its whole
    // rectangular territory must avoid every resident boss; lesser enemies in
    // that territory are consumed rather than left overlapping the arrival.
    public bool TryReceiveBossByAbsorption(Brick boss)
    {
        if (!initialized || IsLost || boss == null || !boss.UsesBossHealth
            || boss.levelGenerator != null) return false;
        if (!TryFindBossLanding(boss, SafeReceivingRows, out Vector3 bestPosition,
            out List<Brick> bestVictims)) return false;
        if (defenseLine >= 2 && LivingEnemyCount - bestVictims.Count + 1
            > BossPressureEnemyLimit) return false;

        float stolenHealth = AbsorbVictims(bestVictims, out List<Vector3> soulOrigins);
        boss.transform.SetParent(transform, true);
        boss.transform.position = bestPosition;
        boss.levelGenerator = this;
        if (!EnemyCollection.Contains(boss.gameObject)) EnemyCollection.Add(boss.gameObject);
        InvalidateEnemyQueries(); FrogRider.InvalidateRoomBonuses();
        boss.gameObject.SetActive(true);
        boss.PlayArrivalAnimation();
        boss.AbsorbHealth(stolenHealth);
        foreach (Vector3 source in soulOrigins)
            SoulAbsorptionEffect.Play(source, boss.transform);
        GameManager.Instance?.Front?.RefreshRoom(this);
        return true;
    }

    private bool TryFindBossLanding(Brick boss, int rowLimit, out Vector3 position,
        out List<Brick> bestVictims)
    {
        position = default;
        bestVictims = null;
        if (boss == null || boss.Footprint.x > Columns || boss.Footprint.y > rowLimit)
            return false;
        var grid = new EnemyRoomGrid(this, rowLimit);
        int fewestVictims = int.MaxValue;
        for (int row = 0; row <= rowLimit - boss.Footprint.y; row++)
        for (int column = 0; column <= Columns - boss.Footprint.x; column++)
        {
            Vector2Int landing = new Vector2Int(column, row);
            var victims = new List<Brick>();
            bool blockedByBoss = false;
            foreach (GameObject occupantObject in EnemyCollection)
            {
                if (occupantObject == null || !occupantObject.TryGetComponent(out Brick occupant)
                    || occupant == boss || !occupant.OccupiesGrid) continue;
                Vector2Int occupied = grid.Anchor(occupant.transform.position, occupant.Footprint);
                if (!RectanglesOverlap(landing, boss.Footprint, occupied, occupant.Footprint)) continue;
                if (occupant.UsesBossHealth) { blockedByBoss = true; break; }
                victims.Add(occupant);
            }
            if (blockedByBoss || victims.Count >= fewestVictims) continue;
            bestVictims = victims;
            position = grid.Center(landing, boss.Footprint);
            fewestVictims = victims.Count;
        }
        return bestVictims != null;
    }

    private static float AbsorbVictims(List<Brick> victims, out List<Vector3> soulOrigins)
    {
        soulOrigins = new List<Vector3>(victims.Count);
        float stolenHealth = 0f;
        foreach (Brick victim in victims)
        {
            soulOrigins.Add(victim.transform.position);
            stolenHealth += victim.VanishForBossAbsorption();
        }
        return stolenHealth;
    }

    private void FindFreeEntryCells()
    {
        FindFreeCells(SpawnRows);
    }

    private void FindFreeCells(int rowCount)
    {
        freeCells.Clear();
        var grid = new EnemyRoomGrid(this, rowCount);
        foreach (Vector2Int cell in grid.FreeAnchors(Vector2Int.one))
            freeCells.Add(grid.Center(cell, Vector2Int.one));
    }

    public void DestroyEnemy(GameObject enemy, bool defeated)
    {
        if (!EnemyCollection.Remove(enemy)) return;
        InvalidateEnemyQueries(); FrogRider.InvalidateRoomBonuses();
        if (defeated)
        {
            GameManager.Instance?.Front?.RefreshRoom(this);
            GameManager.Instance?.TryCompletePlayerTurn();
        }
    }

    public void NotifyEnemyStateChanged()
    {
        InvalidateEnemyQueries(); FrogRider.InvalidateRoomBonuses();
        GameManager.Instance?.Front?.RefreshRoom(this);
    }

    public bool IsMovementBlockedByZombieGrave(Brick movingEnemy, Vector3 destination)
    {
        if (movingEnemy == null) return false;
        Vector2Int movingSize = movingEnemy.Footprint;
        Vector2Int movingCell = CellAnchor(destination, movingSize);
        for (int i = 0; i < EnemyCollection.Count; i++)
        {
            GameObject candidate = EnemyCollection[i];
            if (candidate == null || candidate == movingEnemy.gameObject
                || !candidate.TryGetComponent(out Brick graveEnemy)
                || !graveEnemy.TryGetComponent(out ZombieResurrection zombie)
                || !zombie.BlocksCell) continue;
            Vector2Int graveSize = graveEnemy.Footprint;
            Vector2Int graveCell = CellAnchor(graveEnemy.transform.position, graveSize);
            if (RectanglesOverlap(movingCell, movingSize, graveCell, graveSize)) return true;
        }
        return false;
    }

    public bool IsMovementBlockedByEnemy(Brick movingEnemy, Vector3 destination,
        bool includeSteppingEnemies = false)
    {
        if (movingEnemy == null) return false;
        Vector2Int movingCell = CellAnchor(destination, movingEnemy.Footprint);
        for (int i = 0; i < EnemyCollection.Count; i++)
        {
            GameObject candidate = EnemyCollection[i];
            if (candidate == null || candidate == movingEnemy.gameObject
                || !candidate.TryGetComponent(out Brick blocker)
                || !blocker.OccupiesGrid
                || (!includeSteppingEnemies && blocker.IsStepping)) continue;

            // A two-cell boss cannot treat a moving enemy's still-occupied cell
            // as free just because that enemy began its animation earlier.
            Vector2Int blockerCell = CellAnchor(blocker.transform.position, blocker.Footprint);
            if (FootprintsOverlap(movingEnemy, movingCell, blocker, blockerCell)) return true;
            if (!includeSteppingEnemies || !blocker.IsStepping) continue;
            Vector2Int startCell = CellAnchor(blocker.StepStartPosition, blocker.Footprint);
            Vector2Int targetCell = CellAnchor(blocker.StepTargetPosition, blocker.Footprint);
            if (FootprintsOverlap(movingEnemy, movingCell, blocker, startCell)
                || FootprintsOverlap(movingEnemy, movingCell, blocker, targetCell)) return true;
        }
        return false;
    }

    private static bool FootprintsOverlap(Brick first, Vector2Int firstCell,
        Brick second, Vector2Int secondCell)
    {
        // Boss silhouettes fill nearly the whole cell rectangle even when an
        // octagonal collider omits the tiny corners. Never stack two bosses.
        if (first.UsesBossHealth || second.UsesBossHealth)
            return RectanglesOverlap(firstCell, first.Footprint, secondCell, second.Footprint);
        Vector2Int firstSize = first.Footprint;
        for (int y = 0; y < firstSize.y; y++)
        for (int x = 0; x < firstSize.x; x++)
        {
            Vector2Int offset = new Vector2Int(x, y);
            if (!first.OccupiesFootprintOffset(offset)) continue;
            Vector2Int secondOffset = firstCell + offset - secondCell;
            if (second.OccupiesFootprintOffset(secondOffset)) return true;
        }
        return false;
    }

    private Vector2Int CellAnchor(Vector3 center, Vector2Int footprint)
    {
        return new Vector2Int(
            Mathf.RoundToInt(center.x - transform.position.x - (footprint.x - 1) * 0.5f),
            Mathf.RoundToInt(transform.position.y - center.y - (footprint.y - 1) * 0.5f));
    }

    private static bool RectanglesOverlap(Vector2Int a, Vector2Int aSize,
        Vector2Int b, Vector2Int bSize)
    {
        return a.x < b.x + bSize.x && a.x + aSize.x > b.x
            && a.y < b.y + bSize.y && a.y + aSize.y > b.y;
    }

    public float EnemyHard()
    {
        float ratio = (float)EnemyCollection.Count / (Columns * SpawnRows);
        if (ratio == 0) return 1f;
        if (ratio < 0.3f) return 0.7f;
        if (ratio < 0.6f) return 0.4f;
        if (ratio < 0.9f) return 0.1f;
        return 0f;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.rooms.Remove(this);
    }
}
