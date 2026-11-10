using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public List<Animal> animals;
    public bool stepPlayer = false;
    public bool stepGame = false;
    public bool isPaused = false;
    public int stage = 1;
    [Range(1, 4)] public int maxStage = 4;
    [SerializeField] private GameObject secondStage;
    [SerializeField] private GameObject secondStageButton;
    [SerializeField, Min(1), Tooltip("Completed enemy turns between campaign line openings, regardless of remaining enemies.")]
    private int campaignLineIntervalTurns = LevelProgression.CampaignWavesPerRoom;
    public float losePos = -4;
    public List<LevelGenerator> rooms = new List<LevelGenerator>();

    [Header("Experience")]
    [SerializeField] private ExperienceEconomy experienceEconomy;

    [Header("Projectile fast-forward")]
    [SerializeField, Min(0.1f), Tooltip("Real flight time before the projectile phase accelerates.")]
    private float projectileFastForwardDelay = 5f;
    [SerializeField, Min(1f), Tooltip("Game speed while a projectile phase has exceeded the delay.")]
    private float projectileFastForwardScale = 2f;

    [Header("Survival mode")]
    [SerializeField, Range(0f, 0.5f), Tooltip("Enemy health gained after every turn on line four.")]
    private float survivalHealthGrowthPerTurn = 0.06f;
    [SerializeField, Range(0f, 0.5f), Tooltip("Enemy contact damage gained after every turn on line four.")]
    private float survivalDamageGrowthPerTurn = 0.04f;
    [SerializeField, Min(1), Tooltip("How often endless waves gain one additional enemy.")]
    private int survivalExtraEnemyEveryTurns = 3;
    [SerializeField, Range(1, 4), Tooltip("Hard cap for enemies added by one endless wave in one room.")]
    private int survivalMaximumEnemiesPerWave = 4;
    [SerializeField, Range(1, 20), Tooltip("Player upgrade level granted before the first survival turn.")]
    private int survivalStartingPlayerLevel = 7;

    private int pendingProjectileUpgrades;
    private readonly HashSet<int> rewardedLines = new HashSet<int>();
    private float timeScaleBeforeUpgrade = 1f;
    private bool enemyTurnPendingAfterUpgrade;
    private readonly List<Button> roomSwitchButtons = new List<Button>();
    private readonly List<LevelGenerator> turnRooms = new List<LevelGenerator>(16);
    private readonly List<Brick> bellPulseTargets = new List<Brick>(16);
    private bool resolvingTurn;
    private bool battleStarted;
    private bool? roomSwitchInteractable;
    private bool victoryHandled;
    private bool gameEnded;
    private int defeatedPlannedEnemies;
    private int survivedSurvivalWaves;
    private float projectileFlightElapsed;
    private float timeScaleBeforeProjectileFastForward = 1f;
    private bool projectileFlightWasActive;
    private bool projectileFastForwardActive;
    private bool projectileFastForwardRequested;
    private bool tutorialFlowActive;
    private LevelTransition levelTransition;
    private DefenseLineRuntime defenseLineRuntime;
    public DefenseLineRuntime Front => defenseLineRuntime;
    private ClockSiegeDirector clockDirector;
    // Present only in the clock siege mode.
    public ClockSiegeDirector Clock => clockDirector;
    public bool IsClockMode => GameRunMode.IsClock;
    public bool HasLivingCampaignBoss
    {
        get
        {
            if (IsSurvivalMode) return false;
            foreach (LevelGenerator room in rooms)
                if (room != null && room.HasLivingCampaignBoss) return true;
            return false;
        }
    }
    public bool HasLivingCampaignBossOnLine(int line)
    {
        if (IsSurvivalMode) return false;
        foreach (LevelGenerator room in rooms)
            if (room != null && room.DefenseLine == line && room.HasLivingCampaignBoss)
                return true;
        return false;
    }
    public bool HasStartedCampaignBossEncounter
    {
        get
        {
            if (IsSurvivalMode) return false;
            foreach (LevelGenerator room in rooms)
                if (room != null && room.IsCampaignBossRoom) return true;
            return false;
        }
    }
    public bool HasStartedCampaignBossEncounterOnLine(int line)
    {
        if (IsSurvivalMode) return false;
        foreach (LevelGenerator room in rooms)
            if (room != null && room.DefenseLine == line && room.IsCampaignBossRoom)
                return true;
        return false;
    }
    public bool GameEnded => gameEnded;
    public bool IsResolvingEnemyTurn => resolvingTurn;
    public int TurnNumber { get; private set; }
    public int CampaignLineIntervalTurns => Mathf.Max(1, campaignLineIntervalTurns);
    public static int CampaignLineForTurn(int completedTurns, int interval) => Mathf.Clamp(
        1 + Mathf.Max(0, completedTurns) / Mathf.Max(1, interval), 1, 4);
    public int SurvivedSurvivalWaves => survivedSurvivalWaves;
    public bool IsSurvivalMode => GameRunMode.IsSurvival;
    public bool IsSurvivalFinalLineActive => IsSurvivalMode && stage >= maxStage;
    private bool CampaignVictoryReady => ShouldFinishCampaignWithoutUpgrade(
        IsSurvivalMode, stage, maxStage,
        defenseLineRuntime != null && defenseLineRuntime.IsReady,
        defenseLineRuntime != null && defenseLineRuntime.IsQuiet);
    public static bool ShouldFinishCampaignWithoutUpgrade(bool survivalMode,
        int currentStage, int finalStage, bool frontReady, bool frontQuiet)
    {
        return !survivalMode && currentStage >= finalStage && frontReady && frontQuiet;
    }
    public int SurvivalPressureTurns { get; private set; }
    // The clock siege grows enemy health linearly and never grows contact damage.
    public float CurrentSurvivalHealthMultiplier => clockDirector != null
        ? clockDirector.HealthMultiplierForTurn(SurvivalPressureTurns)
        : IsSurvivalMode
        ? Mathf.Pow(1f + Mathf.Max(0f, survivalHealthGrowthPerTurn), SurvivalPressureTurns)
        : 1f;
    public float CurrentSurvivalDamageMultiplier => clockDirector != null ? 1f
        : IsSurvivalMode
        ? Mathf.Pow(1f + Mathf.Max(0f, survivalDamageGrowthPerTurn), SurvivalPressureTurns)
        : 1f;
    public bool IsFrontMapOpen => defenseLineRuntime != null && defenseLineRuntime.Interface != null
        && defenseLineRuntime.Interface.IsMapOpen;
    public bool IsProjectileFastForwardActive => projectileFastForwardActive;
    public float ProjectileFlightElapsed => projectileFlightElapsed;
    public float ProjectileFastForwardDelay => Mathf.Max(0.1f, projectileFastForwardDelay);
    public float ProjectileFastForwardScale => Mathf.Max(1f, projectileFastForwardScale);
    public bool IsTutorialFlowActive => tutorialFlowActive;
    public event Action ExperienceChanged;

    public int CurrentExperience => experienceEconomy != null ? experienceEconomy.CurrentExperience : 0;
    public int ExperienceForNextUpgrade => experienceEconomy != null
        ? experienceEconomy.ExperienceForNextUpgrade
        : ExperienceEconomy.DefaultFirstUpgradeExperience;
    // Kept as an alias for older editor validations and saved integrations.
    public int EnemiesPerUpgrade => ExperienceForNextUpgrade;
    public int PendingProjectileUpgrades => pendingProjectileUpgrades;
    public int PlayerUpgradeLevel => 1 + (experienceEconomy != null
        ? experienceEconomy.EarnedUpgrades
        : 0);
    public bool IsChoosingUpgrade { get; private set; }
    public bool CanSwitchRooms => !gameEnded && !resolvingTurn && !Player.HasActiveProjectiles
        && !IsChoosingUpgrade && !stepGame && !isPaused && !tutorialFlowActive
        && (defenseLineRuntime == null || !defenseLineRuntime.IsCameraTransitioning);
    public bool HasActiveEnemies
    {
        get
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] != null && !rooms[i].IsLost && rooms[i].HasEnemies)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void TryCompletePlayerTurn()
    {
        if (!battleStarted || gameEnded || resolvingTurn || stepPlayer || tutorialFlowActive) return;
        AnimalProjectileQueue projectileQueue = AnimalProjectileQueue.Instance;
        bool allAmmoSpent = projectileQueue == null || !projectileQueue.HasRemainingAmmo;
        bool allProjectilesResolved = !Player.HasActiveProjectiles;
        if (allProjectilesResolved && !isPaused && !IsChoosingUpgrade && !stepGame
            && TryStartBellRingerPulse()) return;
        // Delayed breach cohorts remain a campaign threat, but they are not
        // shootable targets. An empty battlefield advances without consuming
        // ammunition until the next cohort is ready to enter.
        bool noLivingTargets = true;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] != null && !rooms[i].IsLost && rooms[i].HasLivingEnemies)
            {
                noLivingTargets = false;
                break;
            }
        }

        // A zombie grave remains a target. The player can spend the next firing
        // phase breaking it before the zombie rises.
        bool playerTurnFinished = allAmmoSpent || noLivingTargets;
        if (!playerTurnFinished
            || !allProjectilesResolved
            || stepGame
            || isPaused
            || IsChoosingUpgrade)
        {
            if (!allProjectilesResolved || isPaused || IsChoosingUpgrade || stepGame) return;
            defenseLineRuntime?.EnsureCombatFocus();
            ShowClockEmptyRoomHint();
            return;
        }

        if (noLivingTargets && projectileQueue != null)
        {
            projectileQueue.ClearAllAmmo();
        }

        // The last clear can grant XP before its room is removed from the
        // battlefield. Once the front is genuinely quiet, victory takes
        // precedence over any unspent in-run upgrade choices.
        if (CampaignVictoryReady)
        {
            WinGame();
            return;
        }

        if (pendingProjectileUpgrades > 0)
        {
            enemyTurnPendingAfterUpgrade = true;
            if (OpenUpgradeChoice())
            {
                return;
            }

            enemyTurnPendingAfterUpgrade = false;
        }

        stepGame = true;
    }

    // Clock siege: the camera never moves to enemies, and shooting is allowed only
    // where enemies are. After the player moves to another room, say once if
    // there is nobody to shoot there. Staying put never hides turn announcements.
    private int clockHintRoomId = -1;
    private bool clockHintPending;
    private void TrackClockRoomForHint()
    {
        if (clockDirector == null || defenseLineRuntime == null) return;
        int roomId = defenseLineRuntime.CurrentRoomId;
        if (roomId == clockHintRoomId) return;
        clockHintPending = clockHintRoomId >= 0;
        clockHintRoomId = roomId;
    }

    private void ShowClockEmptyRoomHint()
    {
        if (!clockHintPending || clockDirector == null || defenseLineRuntime == null || IsFrontMapOpen) return;
        LevelGenerator current = defenseLineRuntime.GetGenerator(defenseLineRuntime.CurrentRoomId);
        if (current == null || current.HasLivingEnemies) return;
        clockHintPending = false;
        GameAnnouncementUI.Show("Здесь врагов нет. Ищите их через карту", 2f);
    }

    private bool TryStartBellRingerPulse()
    {
        bellPulseTargets.Clear();
        int completedVolley = Player.LastCombatVolleyId;
        if (completedVolley <= 0) return false;
        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            LevelGenerator room = rooms[roomIndex];
            if (room == null || room.IsLost) continue;
            for (int enemyIndex = 0; enemyIndex < room.EnemyCollection.Count; enemyIndex++)
            {
                GameObject occupant = room.EnemyCollection[enemyIndex];
                if (occupant != null && occupant.TryGetComponent(out UniversalBossAbility bell))
                    bell.ConsumeBellPulse(completedVolley, bellPulseTargets);
            }
        }
        if (bellPulseTargets.Count == 0) return false;
        StartCoroutine(ResolveBellRingerPulseRoutine());
        return true;
    }

    private IEnumerator ResolveBellRingerPulseRoutine()
    {
        resolvingTurn = true;
        turnRooms.Clear();
        turnRooms.AddRange(rooms);
        bellPulseTargets.Sort((left, right) =>
        {
            if (left == null) return right == null ? 0 : 1;
            if (right == null) return -1;
            int byFront = left.LowestCellY.CompareTo(right.LowestCellY);
            return byFront != 0 ? byFront : left.GetInstanceID().CompareTo(right.GetInstanceID());
        });
        try
        {
            foreach (Brick target in bellPulseTargets)
                if (!gameEnded && target != null && !target.IsDefeated
                    && target.levelGenerator != null && !target.levelGenerator.IsLost)
                    target.Step();
            while (!gameEnded && HasEnemyStepAnimations()) yield return null;
            if (gameEnded) yield break;
            foreach (LevelGenerator room in turnRooms)
                if (room != null) room.AnimateRequestedBreach();
            while (!gameEnded && HasEnemyVisualTransitions()) yield return null;
            if (gameEnded) yield break;
            foreach (LevelGenerator room in turnRooms)
                if (room != null) room.ResolveRequestedBreach();
            defenseLineRuntime?.DeliverPressure();
            defenseLineRuntime?.RefreshAll();
            defenseLineRuntime?.NotifyApproachingBreaches();
            defenseLineRuntime?.FocusPendingBreachRoom();
        }
        finally
        {
            bellPulseTargets.Clear();
            resolvingTurn = false;
        }
        if (!gameEnded) TryCompletePlayerTurn();
    }

    public void LoseGame()
    {
        if (gameEnded)
        {
            return;
        }

        gameEnded = true;
        defenseLineRuntime?.Interface?.CloseMap();
        enemyTurnPendingAfterUpgrade = false;
        CloseUpgradeChoice(false);
        stepGame = false;
        stepPlayer = false;
        if (IsSurvivalMode)
        {
            int best = GameRunMode.RecordSurvivalScore(TurnNumber);
            GameAnnouncementUI.Show("Выживание завершено · "
                + TurnNumber + " ходов · рекорд " + best, 3f);
        }
        levelTransition.LosePressed();
    }
    public void WinGame()
    {
        if (IsSurvivalMode || gameEnded || victoryHandled)
        {
            return;
        }

        defenseLineRuntime?.LiberateCompletedLines(maxStage);
        gameEnded = true;
        victoryHandled = true;
        defenseLineRuntime?.Interface?.CloseMap();
        pendingProjectileUpgrades = 0;
        ExperienceChanged?.Invoke();
        enemyTurnPendingAfterUpgrade = false;
        CloseUpgradeChoice(false);
        stepGame = false;
        stepPlayer = false;
        int levelNumber = LevelProgression.Instance != null
            ? LevelProgression.Instance.CurrentLevel
            : 1;
        if (GameRunMode.IsDeveloperRun)
            GameAnnouncementUI.Show("Тестовая победа · прогресс и награды не сохранены", 3f);
        else
        {
            LevelProgression.RecordLevelCompletion(levelNumber);
            int reward = MetaProgression.AwardLevelCompletion(levelNumber);
            GameAnnouncementUI.Show("Победа! Получено желудей: " + reward, 3f);
        }
        levelTransition.WinPressed();
    }
    private void Awake()
    {
        MetaProgression.NormalizeEquipmentCapacity();
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        GameRunMode.BeginGameplay();
        ProjectileDamageStats.BeginLevel();
        levelTransition = FindObjectOfType<LevelTransition>();
        maxStage = 4;
        // The director must exist before the rooms are prepared: clock fronts
        // use one shared battlefield size and breach straight into the centre.
        if (IsClockMode) clockDirector = ClockSiegeDirector.Ensure(this);
        defenseLineRuntime = DefenseLineRuntime.Ensure(this, secondStage, secondStageButton);

        if (experienceEconomy == null)
        {
            experienceEconomy = GetComponent<ExperienceEconomy>();
        }
        if (experienceEconomy == null)
        {
            experienceEconomy = gameObject.AddComponent<ExperienceEconomy>();
        }

        experienceEconomy.Changed += OnExperienceChanged;
    }
    private void Start()
    {
        Time.timeScale = 1.0f;
        ResetProjectileFastForward(false);
        stepPlayer = true;
        ExperienceAndUpgradeUI.Ensure(this);
        EnemyFirstEncounterUI.Ensure();
        FindRoomSwitchButtons();
        // Campaign levels end on their own final line: 2 for local levels
        // 1-2, 3 for levels 3-4 and 4 only for the location finale.
        maxStage = IsSurvivalMode || GameRunMode.IsDeveloperRun || LevelProgression.Instance == null
            ? 4 : LevelProgression.Instance.FinalCampaignLine;
        // Survival starts from a fully deployed front. The inner lines are
        // already secured, while the endless attack enters through line four.
        if (IsSurvivalMode || GameRunMode.IsDeveloperRun)
        {
            stage = maxStage;
        }
        defenseLineRuntime?.BindInterface();
        battleStarted = true;

        if (IsSurvivalMode)
        {
            GameAnnouncementUI.Show(IsClockMode
                ? "РЕЖИМ ВЫЖИВАНИЯ · враги где-то на 12 фронтах"
                : "РЕЖИМ ВЫЖИВАНИЯ · " + LevelProgression.CurrentLocationName, 2f);
            // The clock siege starts without bonus upgrade choices.
            if (!IsClockMode) GrantSurvivalStartingUpgrades();
        }
        else
        {
            int levelNumber = LevelProgression.Instance != null
                ? LevelProgression.Instance.CurrentLocalLevel
                : 1;
            GameAnnouncementUI.Show((GameRunMode.IsDeveloperRun ? "ТЕСТ · " : "")
                + LevelProgression.CurrentLocationName + " · уровень " + levelNumber, 1.5f);
            GrantCampaignStartingUpgrades();
        }
    }

    private void Update()
    {
        TrackClockRoomForHint();
        UpdateProjectileFastForward();
        if (battleStarted && stepGame && !resolvingTurn && !isPaused && !gameEnded
            && !IsChoosingUpgrade && !Player.HasActiveProjectiles)
            ResolveEnemyTurn();
        RefreshRoomSwitchButtons();
    }

    private void UpdateProjectileFastForward()
    {
        bool hasProjectile = Player.HasProjectileBodies;
        if (!hasProjectile)
        {
            ResetProjectileFastForward(true);
            return;
        }

        if (!projectileFlightWasActive)
        {
            projectileFlightWasActive = true;
            projectileFlightElapsed = 0f;
        }

        // Pausing suspends the five-second clock. Upgrade and result screens
        // must retain their own time-scale control.
        if (isPaused || gameEnded || IsChoosingUpgrade || Time.timeScale <= 0f) return;

        if (!projectileFastForwardActive)
        {
            projectileFlightElapsed += Time.unscaledDeltaTime;
            if (projectileFastForwardRequested || projectileFlightElapsed >= ProjectileFastForwardDelay)
                ActivateProjectileFastForward();
        }

        if (projectileFastForwardActive)
            Time.timeScale = ProjectileFastForwardScale;
    }

    public void RequestProjectileFastForward()
    {
        if (!Player.HasProjectileBodies || gameEnded) return;
        projectileFastForwardRequested = true;
        // Keep modal screens paused; a pending request resumes with the volley.
        if (isPaused || IsChoosingUpgrade || Time.timeScale <= 0f) return;
        ActivateProjectileFastForward();
    }

    private void ActivateProjectileFastForward()
    {
        if (!projectileFastForwardActive)
        {
            timeScaleBeforeProjectileFastForward = Mathf.Max(0.01f, Time.timeScale);
            projectileFastForwardActive = true;
        }
        Time.timeScale = ProjectileFastForwardScale;
    }

    public void NotifyProjectileCollectionChanged()
    {
        if (!Player.HasProjectileBodies)
            ResetProjectileFastForward(true);
    }

    public void SetTutorialFlowActive(bool active)
    {
        tutorialFlowActive = active;
    }

    private void ResetProjectileFastForward(bool restoreTimeScale)
    {
        bool wasFastForwarding = projectileFastForwardActive;
        projectileFlightElapsed = 0f;
        projectileFlightWasActive = false;
        projectileFastForwardActive = false;
        projectileFastForwardRequested = false;

        if (restoreTimeScale && wasFastForwarding && Time.timeScale > 0f
            && !isPaused && !IsChoosingUpgrade && !gameEnded)
        {
            Time.timeScale = Mathf.Max(0.01f, timeScaleBeforeProjectileFastForward);
        }
        timeScaleBeforeProjectileFastForward = 1f;
    }

    private void ResolveEnemyTurn()
    {
        if (resolvingTurn || gameEnded) return;
        StartCoroutine(ResolveEnemyTurnRoutine());
    }

    private IEnumerator ResolveEnemyTurnRoutine()
    {
        bool focusedAfterBreach = false;
        resolvingTurn = true;
        TurnNumber++;
        AdvanceSurvivalDifficulty();
        turnRooms.Clear();
        turnRooms.AddRange(rooms);
        try
        {
            // Existing hazards and statuses damage enemies before anyone steps.
            // Keep this order so a bonfire created by a burn death cannot hit
            // again until the next turn, and group damage by type for each enemy.
            EnemyCombatTextUI combatText = EnemyCombatTextUI.Ensure();
            combatText.BeginAfterTurnBatch();
            try
            {
                FireflyBonfire.ProcessAllAfterEnemyTurn(TurnNumber);
                if (gameEnded) yield break;
                HoneyBadgerPoisonCloud.ProcessAllAfterEnemyTurn(TurnNumber);
                if (gameEnded) yield break;
                EnemyStatusEffects.ProcessAllAfterEnemyTurn(TurnNumber);
                if (gameEnded) yield break;
                FrogRider.ProcessAllAfterEnemyTurn(TurnNumber);
                while ((FrogRider.HasActiveJumps || SquirrelAcorn.HasActiveBlasts) && !gameEnded)
                    yield return null;
            }
            finally
            {
                combatText.FinishAfterTurnBatch();
            }
            LevelGenerator approachingWave = GetUpcomingWaveWarningRoom();
            // The clock director announces its own fronts and events instead.
            if (approachingWave != null && clockDirector == null)
                WaveWarningUI.Show(approachingWave.DefenseLine,
                    approachingWave.SpawnedWaveCount + 1, approachingWave.IsEndlessSurvivalSource);
            // Arrivals happen after movement, so no enemy moves twice in one turn.
            foreach (LevelGenerator room in turnRooms)
            {
                if (room != null) room.StepEnemy();
                if (gameEnded) yield break;
            }
            // The turn stays locked until every enemy reaches its next cell.
            // This keeps collisions and breaches in sync with the jump the
            // player actually sees.
            while (!gameEnded && HasEnemyStepAnimations()) yield return null;
            if (gameEnded) yield break;
            foreach (LevelGenerator room in turnRooms)
                if (room != null) room.AnimateRequestedBreach();
            while (!gameEnded && HasEnemyVisualTransitions()) yield return null;
            if (gameEnded) yield break;
            // Resolve breaches only after the complete movement batch lands.
            // This preserves the final cell of every enemy when their individual
            // travel delay to the next room is calculated.
            foreach (LevelGenerator room in turnRooms)
                if (room != null) room.ResolveRequestedBreach();
            if (gameEnded) yield break;
            foreach (LevelGenerator room in turnRooms)
                if (room != null) room.SpawnNextWave();
            // Deliver only cohorts whose cell-based breach delay has elapsed.
            // Arrival happens after movement so enemies neither overlap jumping
            // units nor move a second time in their arrival phase.
            defenseLineRuntime.DeliverPressure();
            if (gameEnded) yield break;
            foreach (LevelGenerator room in turnRooms)
                if (room != null) room.CompleteEnemyTurn(TurnNumber);
            AdvanceCampaignLineSchedule();
            // Clock fronts open, light eras change and events start between turns.
            clockDirector?.AfterEnemyTurn(TurnNumber);
            if (gameEnded) yield break;
            // Pick one final destination after all simultaneous breaches have
            // been collected, without jumping the camera through each callback.
            focusedAfterBreach = defenseLineRuntime.FocusPendingBreachRoom();
            // Graves block this enemy movement phase, then eligible zombies
            // rise before control returns to the player.
            ZombieResurrection.ProcessAllAfterEnemyTurn(TurnNumber);
            if (IsSurvivalFinalLineActive && !gameEnded) survivedSurvivalWaves++;
            defenseLineRuntime.RefreshAll();
            defenseLineRuntime.NotifyApproachingBreaches();
            defenseLineRuntime.PayTurnIncome();
            if (defenseLineRuntime.IsReady && defenseLineRuntime.IsQuiet)
            {
                if (stage >= maxStage && !IsSurvivalMode) WinGame();
            }
            if (!gameEnded)
            {
                if (!focusedAfterBreach) defenseLineRuntime.EnsureCombatFocus();
                while (!gameEnded && defenseLineRuntime.IsCameraTransitioning) yield return null;
                if (gameEnded) yield break;

                stepPlayer = true;
                // A burn kill can earn an upgrade before enemies move.
                // stepPlayer stays pending so ammunition reloads only after selection.
                if (pendingProjectileUpgrades > 0)
                {
                    OpenUpgradeChoice();
                }
            }
        }
        finally
        {
            stepGame = false;
            resolvingTurn = false;
        }
    }

    private LevelGenerator GetUpcomingWaveWarningRoom()
    {
        LevelGenerator focused = defenseLineRuntime != null
            ? defenseLineRuntime.GetGenerator(defenseLineRuntime.CurrentRoomId) : null;
        if (focused != null && focused.IsInitialized && focused.RemainingWaves > 0)
            return focused;
        LevelGenerator outermost = null;
        foreach (LevelGenerator room in turnRooms)
        {
            if (room == null || !room.IsInitialized || room.RemainingWaves <= 0) continue;
            if (outermost == null || room.DefenseLine > outermost.DefenseLine)
                outermost = room;
        }
        return outermost;
    }

    private bool HasEnemyStepAnimations()
    {
        for (int roomIndex = 0; roomIndex < turnRooms.Count; roomIndex++)
        {
            LevelGenerator room = turnRooms[roomIndex];
            if (room == null) continue;
            for (int enemyIndex = 0; enemyIndex < room.EnemyCollection.Count; enemyIndex++)
            {
                GameObject enemy = room.EnemyCollection[enemyIndex];
                if (enemy != null && enemy.TryGetComponent(out Brick brick) && brick.IsStepping)
                    return true;
            }
        }
        return false;
    }

    private bool HasEnemyVisualTransitions()
    {
        for (int roomIndex = 0; roomIndex < turnRooms.Count; roomIndex++)
        {
            LevelGenerator room = turnRooms[roomIndex];
            if (room == null) continue;
            for (int enemyIndex = 0; enemyIndex < room.EnemyCollection.Count; enemyIndex++)
            {
                GameObject enemy = room.EnemyCollection[enemyIndex];
                if (enemy != null && enemy.TryGetComponent(out Brick brick)
                    && brick.IsVisualTransitioning) return true;
            }
        }
        return false;
    }

    private void OnDestroy()
    {
        if (projectileFastForwardActive && Time.timeScale > 0f)
            Time.timeScale = Mathf.Max(0.01f, timeScaleBeforeProjectileFastForward);
        if (experienceEconomy != null)
        {
            experienceEconomy.Changed -= OnExperienceChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterEnemyDefeated(int experienceReward)
    {
        AddExperience(experienceReward);
    }

    public void RegisterEnemyDefeated(Brick enemy, int experienceReward)
    {
        RegisterEnemyDefeated(experienceReward);
        if (!gameEnded && !IsSurvivalMode && enemy != null && !enemy.IsSummonedVariant)
            defeatedPlannedEnemies++;
    }

    // Clock siege event reward: one extra upgrade choice, offered at the next
    // safe point of the turn exactly like a line-clear bonus.
    public void GrantEventUpgrade()
    {
        if (gameEnded || experienceEconomy == null) return;
        pendingProjectileUpgrades++;
        experienceEconomy.GrantUpgradeLevels(1);
        ExperienceChanged?.Invoke();
    }

    public void RegisterLineCleared(int defenseLine)
    {
        if (gameEnded || experienceEconomy == null || defenseLine < 1 || defenseLine > maxStage)
        {
            return;
        }

        // Inner survival rooms are deliberately pre-liberated and generate
        // gold from turn one, but they are not combat clears and grant no XP.
        if (IsSurvivalMode && !battleStarted)
        {
            return;
        }

        if (!rewardedLines.Add(defenseLine)) return;
        // One bonus level per completed line, independent of room count and
        // later reoccupation. XP progress and the next XP price are preserved.
        pendingProjectileUpgrades++;
        experienceEconomy.GrantUpgradeLevels(1);
        // TryCompletePlayerTurn gives a finished campaign priority over the
        // unused final choice while retaining the earned level in the run stats.
        TryCompletePlayerTurn();
    }

    private void GrantSurvivalStartingUpgrades()
    {
        int upgradeChoices = Mathf.Max(0, survivalStartingPlayerLevel - PlayerUpgradeLevel);
        GrantStartingUpgrades(upgradeChoices);
    }

    private void GrantCampaignStartingUpgrades()
    {
        int globalLevel = LevelProgression.Instance != null
            ? LevelProgression.Instance.CurrentLevel
            : LevelProgression.SelectedMenuLevel;
        GrantStartingUpgrades(LevelProgression.StartingUpgradeChoicesForLevel(globalLevel));
    }

    private void GrantStartingUpgrades(int upgradeChoices)
    {
        if (experienceEconomy == null)
        {
            return;
        }

        upgradeChoices = Mathf.Max(0, upgradeChoices);
        if (upgradeChoices == 0)
        {
            return;
        }

        pendingProjectileUpgrades += upgradeChoices;
        experienceEconomy.GrantUpgradeLevels(upgradeChoices);
        OpenUpgradeChoice();
    }

    private void AddExperience(int amount)
    {
        if (gameEnded || experienceEconomy == null || amount <= 0)
        {
            return;
        }

        pendingProjectileUpgrades += experienceEconomy.AddExperience(amount);
        // Brick removes itself from the room immediately after granting XP.
        // The removal path completes the turn after the defense map has the
        // correct state; checking here could open an upgrade over a win.
    }

    private void OnExperienceChanged()
    {
        ExperienceChanged?.Invoke();
    }

    public void SelectProjectileUpgrade(int animalIndex)
    {
        if (!IsChoosingUpgrade || pendingProjectileUpgrades <= 0)
        {
            return;
        }

        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        if (queue == null || !queue.Upgrade(animalIndex))
        {
            return;
        }

        CompleteProjectileUpgradeSelection();
    }

    public void SelectProjectileUpgrade(int animalIndex, AnimalProjectileQueue.UpgradeKind kind)
    {
        if (!IsChoosingUpgrade || pendingProjectileUpgrades <= 0)
        {
            return;
        }

        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        if (queue == null || !queue.Upgrade(animalIndex, kind))
        {
            return;
        }

        CompleteProjectileUpgradeSelection();
    }

    private void CompleteProjectileUpgradeSelection()
    {

        Player.BlockInputUntilReleased();
        pendingProjectileUpgrades--;
        // Exhausted unique branches must close the modal even with more pending levels.
        if (AnimalProjectileQueue.Instance == null
            || AnimalProjectileQueue.Instance.GetAvailableUpgradeOptions().Count == 0)
            pendingProjectileUpgrades = 0;
        ExperienceChanged?.Invoke();

        ExperienceAndUpgradeUI ui = ExperienceAndUpgradeUI.Ensure(this);
        if (pendingProjectileUpgrades > 0)
        {
            ui.ShowUpgradeChoice();
        }
        else
        {
            bool startEnemyTurn = enemyTurnPendingAfterUpgrade;
            enemyTurnPendingAfterUpgrade = false;
            CloseUpgradeChoice(true);

            if (startEnemyTurn && !isPaused)
            {
                stepGame = true;
            }
        }
    }

    private bool OpenUpgradeChoice()
    {
        if (gameEnded || CampaignVictoryReady) return false;
        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        bool hasAvailableUpgrade = queue != null
            && queue.GetAvailableUpgradeOptions().Count > 0;

        if (!hasAvailableUpgrade)
        {
            pendingProjectileUpgrades = 0;
            ExperienceChanged?.Invoke();
            return false;
        }

        IsChoosingUpgrade = true;
        timeScaleBeforeUpgrade = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        ExperienceAndUpgradeUI.Ensure(this).ShowUpgradeChoice();
        return true;
    }

    private void CloseUpgradeChoice(bool restoreTimeScale)
    {
        if (!IsChoosingUpgrade)
        {
            return;
        }

        IsChoosingUpgrade = false;
        ExperienceAndUpgradeUI.CloseUpgradeChoice();

        if (restoreTimeScale && !isPaused)
        {
            Time.timeScale = Mathf.Max(0.01f, timeScaleBeforeUpgrade);
        }
    }
    private void AdvanceCampaignLineSchedule()
    {
        if (IsSurvivalMode || GameRunMode.IsDeveloperRun || gameEnded) return;
        int scheduledLine = Mathf.Min(maxStage, CampaignLineForTurn(TurnNumber, CampaignLineIntervalTurns));
        while (stage < scheduledLine) OpenNextLine(false);
    }

    public void NextStage() => OpenNextLine(true);

    private void OpenNextLine(bool focusNewLine)
    {
        if (stage >= maxStage) return;
        int completedStage = stage;
        defenseLineRuntime?.LiberateCompletedLines(completedStage);
        stage = Mathf.Min(stage + 1, maxStage);
        defenseLineRuntime?.ActivateLine(stage, focusNewLine);

        if (defenseLineRuntime == null && stage == 2)
        {
            secondStage.SetActive(true);
            secondStageButton.SetActive(true);
        }

        FindRoomSwitchButtons();
        stepPlayer = true;

        if (IsSurvivalMode && stage == maxStage)
            GameAnnouncementUI.Show("Четвёртая линия открыта. Волны больше не закончатся", 3f);
        else
            GameAnnouncementUI.Show("Открыта линия " + stage + " · "
                + LevelProgression.CampaignWavesPerRoom + " волн", 2f);
    }

    public int GetSurvivalWaveEnemyCount(int baseEnemyCount)
    {
        int safeBase = Mathf.Clamp(baseEnemyCount, 1, survivalMaximumEnemiesPerWave);
        if (!IsSurvivalFinalLineActive) return safeBase;
        int interval = Mathf.Max(1, survivalExtraEnemyEveryTurns);
        int additional = SurvivalPressureTurns / interval;
        return Mathf.Clamp(safeBase + additional, 1, survivalMaximumEnemiesPerWave);
    }

    private void AdvanceSurvivalDifficulty()
    {
        if (!IsSurvivalMode) return;
        SurvivalPressureTurns++;
        // Clock health is linear in the turn number, so living enemies grow by
        // the ratio between this turn and the previous one.
        float healthFactor = clockDirector != null
            ? clockDirector.HealthMultiplierForTurn(SurvivalPressureTurns)
                / clockDirector.HealthMultiplierForTurn(SurvivalPressureTurns - 1)
            : 1f + Mathf.Max(0f, survivalHealthGrowthPerTurn);
        float damageFactor = clockDirector != null ? 1f
            : 1f + Mathf.Max(0f, survivalDamageGrowthPerTurn);
        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            LevelGenerator room = rooms[roomIndex];
            if (room == null) continue;
            for (int enemyIndex = 0; enemyIndex < room.EnemyCollection.Count; enemyIndex++)
            {
                GameObject enemy = room.EnemyCollection[enemyIndex];
                if (enemy != null && enemy.TryGetComponent(out Brick brick))
                    brick.ApplySurvivalGrowth(healthFactor, damageFactor);
            }
        }

        defenseLineRuntime?.RefreshAll();
    }

    public void CompleteRoom(LevelGenerator room)
    {
        if (gameEnded || room == null) return;
        defenseLineRuntime?.RoomCompleted(room);
    }

    public LevelGenerator FindFirstActiveRoom(int defenseLine, LevelGenerator except = null)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            LevelGenerator room = rooms[i];
            if (room != null
                && room != except
                && !room.IsLost
                && !room.IsCompleted
                && room.DefenseLine == defenseLine)
            {
                return room;
            }
        }

        return null;
    }

    public void RegisterRoomSwitchButton(Button button)
    {
        if (button != null && !roomSwitchButtons.Contains(button))
        {
            roomSwitchButtons.Add(button);
            button.interactable = CanSwitchRooms;
        }
    }

    private bool HasRoomsOnLine(int defenseLine)
    {
        return FindFirstActiveRoom(defenseLine) != null;
    }

    private static string LineCompletedMessage(int completedLine, bool restoredRooms)
    {
        if (restoredRooms)
        {
            switch (completedLine)
            {
                case 2:
                    return "Вторая линия снова освобождена. Открыта третья линия";
                case 3:
                    return "Третья линия снова освобождена. Открыта четвёртая линия";
            }
        }
        switch (completedLine)
        {
            case 1:
                return "Первая линия обороны пройдена";
            case 2:
                return "Вторая линия обороны пройдена";
            case 3:
                return "Третья линия обороны пройдена";
            default:
                return "Линия обороны пройдена";
        }
    }

    private void FindRoomSwitchButtons()
    {
        roomSwitchButtons.Clear();
        Button[] buttons = FindObjectsOfType<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (IsDefenseRoomButton(button.gameObject.name))
            {
                roomSwitchButtons.Add(button);
                continue;
            }

            for (int eventIndex = 0; eventIndex < button.onClick.GetPersistentEventCount(); eventIndex++)
            {
                if (button.onClick.GetPersistentTarget(eventIndex) is CameraBehaviour)
                {
                    roomSwitchButtons.Add(button);
                    break;
                }
            }
        }

        roomSwitchInteractable = null;
        RefreshRoomSwitchButtons();
    }

    private static bool IsDefenseRoomButton(string objectName)
    {
        if (objectName == "Stage 1Button")
        {
            return true;
        }

        return objectName.StartsWith("Stage 2_")
            || objectName.StartsWith("Stage 3_")
            || objectName.StartsWith("Stage 4_");
    }

    private void RefreshRoomSwitchButtons()
    {
        bool interactable = CanSwitchRooms;
        if (roomSwitchInteractable.HasValue && roomSwitchInteractable.Value == interactable)
        {
            return;
        }

        roomSwitchInteractable = interactable;
        for (int i = 0; i < roomSwitchButtons.Count; i++)
        {
            if (roomSwitchButtons[i] != null)
            {
                roomSwitchButtons[i].interactable = interactable;
            }
        }
    }
}
