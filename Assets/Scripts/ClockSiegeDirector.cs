using System.Collections.Generic;
using UnityEngine;

// Clock siege ("Часы"): 12 fronts around the central main room. Every front
// breaches straight into the centre and its single barricade. Fronts open on
// a turn schedule in random places, light dims in eras, and one event at a
// time disturbs the defence. Rules: Tools/ClockSiegeMode.md.
[DisallowMultipleComponent]
public sealed class ClockSiegeDirector : MonoBehaviour
{
    public enum ClockEvent { None, EliteEnemy, BossRaid, DarkStorm, Calm, FireflySwarm, GoldenAcorn, SplitFront }

    public const int ClockHours = 12;

    [Header("Fronts")]
    [SerializeField, Range(1, 4), Tooltip("Every front uses the battlefield size of this defense line.")]
    private int frontBattlefieldLine = 3;
    [SerializeField, Tooltip("Player turns on which one more front is permanently active (1 front before the first).")]
    private int[] extraFrontTurns = { 5, 10, 15 };
    [SerializeField, Min(1), Tooltip("Every open front sends a wave once per this many enemy turns.")]
    private int waveIntervalTurns = 2;

    [Header("Enemy growth")]
    [SerializeField, Min(1)] private int baseEnemiesPerWave = 2;
    [SerializeField, Min(1), Tooltip("Every N turns a wave gains one enemy.")]
    private int extraEnemyEveryTurns = 10;
    [SerializeField, Min(0)] private int maximumExtraEnemies = 4;
    [SerializeField, Range(0f, 0.5f), Tooltip("Enemy health multiplier is 1 + this × turn.")]
    private float healthGrowthPerTurn = 0.05f;

    [Header("Light eras")]
    [SerializeField, Tooltip("Player turn on which each light era starts.")]
    private int[] lightEraStartTurns = { 1, 50, 100 };
    [SerializeField, Tooltip("Lit share of every front in each era.")]
    private float[] lightEraLitFractions = { 0.8f, 0.5f, 0.3f };
    [SerializeField, Min(1)] private int lightWarningTurns = 3;

    [Header("Events")]
    [SerializeField, Min(1)] private int minimumEventGapTurns = 7;
    [SerializeField, Min(1)] private int maximumEventGapTurns = 10;
    [SerializeField, Min(0), Tooltip("A boss raid every N turns, independent of other events. 0 disables it.")]
    private int bossRaidEveryTurns = 25;
    [SerializeField, Min(1)] private int stormDurationTurns = 2;
    [SerializeField, Min(1f)] private float eliteHealthMultiplier = 5f;
    [SerializeField, Min(1)] private int eliteDeadlineTurns = 3;
    [SerializeField, Min(0)] private int eliteGoldReward = 5;
    [SerializeField, Min(0f)] private float calmBarricadeRestore = 10f;
    [SerializeField, Min(1f)] private float goldenAcornHealthMultiplier = 6f;
    [SerializeField, Min(0)] private int goldenAcornGuards = 4;
    [SerializeField, Min(0)] private int goldenAcornGoldReward = 15;
    [SerializeField, Min(1)] private int splitFrontDurationTurns = 3;
    [SerializeField] private Color eliteTint = new Color(1f, 0.78f, 0.28f, 1f);
    [SerializeField] private Color goldenAcornTint = new Color(1f, 0.9f, 0.2f, 1f);

    private readonly List<int> frontIds = new List<int>(ClockHours);
    private readonly HashSet<int> spawning = new HashSet<int>();
    private readonly Dictionary<int, int> openedTurn = new Dictionary<int, int>();
    private readonly List<string> messages = new List<string>(4);
    // Rooms whose enemies the player has seen. Only these show enemies on the map.
    private readonly HashSet<int> revealedRooms = new HashSet<int>();
    private readonly List<int> concealAgain = new List<int>(4);
    private GameManager manager;
    private DefenseLineRuntime front;
    private DefenseFront model;
    private int pendingFrontId = -1;
    private int splitFrontId = -1;
    private int splitEndsAfterTurn;
    private int nextEventTurn;
    private ClockEvent announcedEvent;
    private ClockEvent activeEvent;
    private int activeEventEndsAfterTurn;
    private int calmEnemyTurn = -1;
    private Brick elite;
    private bool eliteTracked;
    private Brick goldenAcorn;
    private bool goldenAcornTracked;
    private bool running;

    public int FrontBattlefieldLine => Mathf.Clamp(frontBattlefieldLine, 1, 4);
    public int BaseEnemiesPerWave => Mathf.Max(1, baseEnemiesPerWave);
    public int PendingFrontId => pendingFrontId;
    public bool IsStormActive => activeEvent == ClockEvent.DarkStorm;
    public ClockEvent ActiveEvent => activeEvent;
    public ClockEvent AnnouncedEvent => announcedEvent;
    public int ActiveFrontCount => spawning.Count;
    public IReadOnlyList<int> FrontIds => frontIds;
    private int CompletedTurns => manager != null ? manager.TurnNumber : 0;
    public float CurrentLitFraction => IsStormActive ? 0f : EraLitFraction(CompletedTurns + 1);

    public int WaveEnemyCount => BaseEnemiesPerWave
        + Mathf.Min(Mathf.Max(0, maximumExtraEnemies), CompletedTurns / Mathf.Max(1, extraEnemyEveryTurns));

    public static ClockSiegeDirector Ensure(GameManager game)
    {
        ClockSiegeDirector director = game.GetComponent<ClockSiegeDirector>();
        if (director == null) director = game.gameObject.AddComponent<ClockSiegeDirector>();
        director.manager = game;
        return director;
    }

    // Clock bosses are raid bosses with the line-two multiplier, whichever
    // front they come from. Other modes keep the multiplier of their line.
    public static int BossHealthLineFor(int defenseLine)
        => GameManager.Instance != null && GameManager.Instance.Clock != null ? 2 : defenseLine;

    public float HealthMultiplierForTurn(int turn) => 1f + Mathf.Max(0f, healthGrowthPerTurn) * Mathf.Max(0, turn);

    public int DesiredFronts(int playerTurn)
    {
        int count = 1;
        if (extraFrontTurns != null)
            foreach (int turn in extraFrontTurns)
                if (playerTurn >= turn) count++;
        return Mathf.Clamp(count, 1, ClockHours);
    }

    public float EraLitFraction(int playerTurn)
    {
        float lit = 1f;
        if (lightEraStartTurns == null || lightEraLitFractions == null) return lit;
        int count = Mathf.Min(lightEraStartTurns.Length, lightEraLitFractions.Length);
        int bestStart = int.MinValue;
        for (int i = 0; i < count; i++)
            if (playerTurn >= lightEraStartTurns[i] && lightEraStartTurns[i] >= bestStart)
            {
                bestStart = lightEraStartTurns[i];
                lit = Mathf.Clamp01(lightEraLitFractions[i]);
            }
        return lit;
    }

    public bool IsFrontSpawning(int roomId) => spawning.Contains(roomId);

    public bool ShouldSpawnWave(int roomId)
    {
        if (!spawning.Contains(roomId) || manager == null) return false;
        int turn = manager.TurnNumber;
        if (turn == calmEnemyTurn) return false;
        int opened = openedTurn.TryGetValue(roomId, out int value) ? value : 0;
        return (turn - opened) % Mathf.Max(1, waveIntervalTurns) == 0;
    }

    public void ConfigureModel(DefenseFront defenseModel)
    {
        model = defenseModel;
        frontIds.Clear();
        foreach (DefenseFront.Room room in defenseModel.Rooms)
            if (room.Line >= 3) frontIds.Add(room.Id);
        defenseModel.ConfigureClockLayout(frontIds, ShuffledHours());
    }

    public void BeginRun(DefenseLineRuntime runtime)
    {
        if (running) return;
        running = true;
        front = runtime;
        if (manager == null) manager = GetComponent<GameManager>();
        model = runtime.Model;
        int minimumGap = Mathf.Max(1, minimumEventGapTurns);
        nextEventTurn = Random.Range(minimumGap, Mathf.Max(minimumGap, maximumEventGapTurns) + 1);
        // Every front is reachable from the start. Quiet fronts have no enemies
        // until the director makes them attack; the map never tells which is which.
        foreach (int id in frontIds)
        {
            front.SetRoomLitFraction(id, CurrentLitFraction);
            front.OpenClockFront(id, false);
            // A front pays income only after it has been attacked and cleared.
            model.SetEarnsIncome(id, false);
        }
        UpdateFronts(1, true);
        ApplyLighting();
        FlushMessages();
        front.NotifyChanged();
    }

    public void AfterEnemyTurn(int completedTurn)
    {
        if (!running || front == null || manager == null || manager.GameEnded) return;
        CheckEventTargets();
        CloseExpiredSplit(completedTurn);
        UpdateTimedEvents(completedTurn);
        UpdateFronts(completedTurn + 1, false);
        UpdateRandomEvents(completedTurn);
        UpdateBossRaid(completedTurn);
        UpdateLightEra(completedTurn + 1);
        ApplyLighting();
        FlushMessages();
        front.NotifyChanged();
    }

    private void Update()
    {
        if (!running || manager == null || manager.GameEnded) return;
        CheckEventTargets();
        UpdateRevealedRooms();
    }

    public bool IsRoomRevealed(int roomId) => revealedRooms.Contains(roomId);

    // A room appears on the map once the player sees one of its enemies in the
    // lit part of the field (or under firefly light). It goes back to "?" when
    // no living enemy is left there, so later waves stay unknown until seen.
    private void UpdateRevealedRooms()
    {
        if (front == null) return;
        bool changed = false;
        int currentId = front.CurrentRoomId;
        if (currentId >= 0 && !revealedRooms.Contains(currentId) && !front.IsCameraTransitioning
            && HasVisibleEnemy(front.GetGenerator(currentId)))
        {
            revealedRooms.Add(currentId);
            changed = true;
        }
        concealAgain.Clear();
        foreach (int id in revealedRooms)
        {
            LevelGenerator generator = front.GetGenerator(id);
            if (generator == null || !generator.HasLivingEnemies) concealAgain.Add(id);
        }
        foreach (int id in concealAgain) revealedRooms.Remove(id);
        if (changed || concealAgain.Count > 0) front.NotifyChanged();
    }

    private static bool HasVisibleEnemy(LevelGenerator generator)
    {
        if (generator == null) return false;
        RoomDarkness darkness = generator.Darkness;
        foreach (GameObject enemy in generator.EnemyCollection)
        {
            if (enemy == null || !enemy.TryGetComponent(out Brick brick) || brick.IsDefeated
                || brick.levelGenerator != generator) continue;
            if (darkness == null || !darkness.isActiveAndEnabled
                || darkness.IsVisible(enemy.transform.position)) return true;
        }
        return false;
    }

    // ----- Fronts -------------------------------------------------------

    private int CoreSpawningCount => spawning.Count
        - (splitFrontId >= 0 && spawning.Contains(splitFrontId) ? 1 : 0);

    private void UpdateFronts(int playerTurn, bool firstFront)
    {
        int desired = DesiredFronts(playerTurn);
        while (CoreSpawningCount < desired)
        {
            int id = pendingFrontId >= 0 ? pendingFrontId : PickClosedFront(-1);
            pendingFrontId = -1;
            if (id < 0) break;
            OpenFront(id);
            if (!firstFront) messages.Add("Враги пошли в наступление ещё на одном фронте");
            firstFront = false;
        }
        if (pendingFrontId < 0 && DesiredFronts(playerTurn + 1) > CoreSpawningCount)
        {
            pendingFrontId = PickClosedFront(-1);
            if (pendingFrontId >= 0)
                messages.Add("Через ход враги нападут ещё с одной стороны");
        }
    }

    private void OpenFront(int id)
    {
        spawning.Add(id);
        openedTurn[id] = CompletedTurns;
        front.SetRoomLitFraction(id, CurrentLitFraction);
        front.OpenClockFront(id, false);
        model.SetEarnsIncome(id, true);
        front.GetGenerator(id)?.SpawnClockWave();
    }

    // Prefers fronts that were never opened, then quiet ones.
    private int PickClosedFront(int excluded)
    {
        var fresh = new List<int>();
        var quiet = new List<int>();
        foreach (int id in frontIds)
        {
            if (spawning.Contains(id) || id == pendingFrontId || id == excluded) continue;
            if (openedTurn.ContainsKey(id)) quiet.Add(id);
            else fresh.Add(id);
        }
        List<int> pool = fresh.Count > 0 ? fresh : quiet;
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : -1;
    }

    private static List<int> ShuffledHours()
    {
        var hours = new List<int>(ClockHours);
        for (int hour = 1; hour <= ClockHours; hour++) hours.Add(hour);
        for (int i = hours.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int swap = hours[i]; hours[i] = hours[j]; hours[j] = swap;
        }
        return hours;
    }

    private int RandomSpawningFront()
    {
        if (spawning.Count == 0) return -1;
        var candidates = new List<int>(spawning);
        return candidates[Random.Range(0, candidates.Count)];
    }

    private List<int> SpawningFrontsInRandomOrder()
    {
        var candidates = new List<int>(spawning);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int swap = candidates[i]; candidates[i] = candidates[j]; candidates[j] = swap;
        }
        return candidates;
    }

    // ----- Light --------------------------------------------------------

    private void ApplyLighting()
    {
        float lit = CurrentLitFraction;
        foreach (int id in frontIds) front.SetRoomLitFraction(id, lit);
    }

    private void UpdateLightEra(int playerTurn)
    {
        if (lightEraStartTurns == null) return;
        foreach (int start in lightEraStartTurns)
        {
            if (start <= 1) continue;
            if (playerTurn + Mathf.Max(1, lightWarningTurns) == start)
                messages.Add("Сумерки сгущаются: через " + lightWarningTurns + " хода станет темнее");
            else if (playerTurn == start)
                messages.Add("Стало темнее: освещено " + Mathf.RoundToInt(EraLitFraction(start) * 100f) + "% фронтов");
        }
    }

    // ----- Events -------------------------------------------------------

    public static string EventName(ClockEvent clockEvent)
    {
        switch (clockEvent)
        {
            case ClockEvent.EliteEnemy: return "Элитный враг";
            case ClockEvent.BossRaid: return "Набег босса";
            case ClockEvent.DarkStorm: return "Тёмная буря";
            case ClockEvent.Calm: return "Затишье";
            case ClockEvent.FireflySwarm: return "Стая светлячков";
            case ClockEvent.GoldenAcorn: return "Золотой жёлудь";
            case ClockEvent.SplitFront: return "Раскол фронта";
            default: return "";
        }
    }

    private void UpdateTimedEvents(int completedTurn)
    {
        if (activeEvent == ClockEvent.DarkStorm && completedTurn >= activeEventEndsAfterTurn)
        {
            activeEvent = ClockEvent.None;
            messages.Add("Буря стихла");
        }
        else if (activeEvent == ClockEvent.Calm && completedTurn >= activeEventEndsAfterTurn)
            activeEvent = ClockEvent.None;
        if (eliteTracked && completedTurn >= activeEventEndsAfterTurn
            && activeEvent == ClockEvent.EliteEnemy)
        {
            eliteTracked = false;
            elite = null;
            activeEvent = ClockEvent.None;
            messages.Add("Элитный враг ушёл от награды");
        }
    }

    private void UpdateRandomEvents(int completedTurn)
    {
        if (activeEvent != ClockEvent.None)
        {
            // Only one event at a time: wait and announce after this one ends.
            if (announcedEvent == ClockEvent.None && nextEventTurn <= completedTurn + 1)
                nextEventTurn = completedTurn + 2;
            return;
        }
        if (announcedEvent != ClockEvent.None && completedTurn >= nextEventTurn)
        {
            ClockEvent started = announcedEvent;
            announcedEvent = ClockEvent.None;
            StartEvent(started, completedTurn);
            int minimumGap = Mathf.Max(1, minimumEventGapTurns);
            nextEventTurn = completedTurn + Random.Range(minimumGap,
                Mathf.Max(minimumGap, maximumEventGapTurns) + 1);
            return;
        }
        if (announcedEvent == ClockEvent.None && completedTurn + 1 >= nextEventTurn)
        {
            nextEventTurn = completedTurn + 1;
            announcedEvent = PickEvent();
            if (announcedEvent != ClockEvent.None)
                messages.Add("Через ход: " + EventName(announcedEvent));
        }
    }

    private ClockEvent PickEvent()
    {
        var pool = new List<ClockEvent> {
            ClockEvent.EliteEnemy, ClockEvent.DarkStorm, ClockEvent.Calm, ClockEvent.FireflySwarm
        };
        if (!goldenAcornTracked) pool.Add(ClockEvent.GoldenAcorn);
        if (PickClosedFront(-1) >= 0) pool.Add(ClockEvent.SplitFront);
        return pool[Random.Range(0, pool.Count)];
    }

    private void StartEvent(ClockEvent clockEvent, int completedTurn)
    {
        switch (clockEvent)
        {
            case ClockEvent.EliteEnemy: StartElite(completedTurn); break;
            case ClockEvent.DarkStorm: StartStorm(completedTurn); break;
            case ClockEvent.Calm: StartCalm(completedTurn); break;
            case ClockEvent.FireflySwarm: StartFireflySwarm(); break;
            case ClockEvent.GoldenAcorn: StartGoldenAcorn(); break;
            case ClockEvent.SplitFront: StartSplitFront(completedTurn); break;
        }
    }

    private void StartElite(int completedTurn)
    {
        foreach (int id in SpawningFrontsInRandomOrder())
        {
            LevelGenerator generator = front.GetGenerator(id);
            Brick spawned = generator != null
                ? generator.SpawnEventEnemy(eliteHealthMultiplier, eliteTint, false) : null;
            if (spawned == null) continue;
            elite = spawned;
            eliteTracked = true;
            activeEvent = ClockEvent.EliteEnemy;
            activeEventEndsAfterTurn = completedTurn + Mathf.Max(1, eliteDeadlineTurns);
            messages.Add("Где-то появился элитный враг! Найдите и убейте его за "
                + eliteDeadlineTurns + " хода");
            return;
        }
    }

    private void StartStorm(int completedTurn)
    {
        // Fronts swap places on the dial, every front goes dark and every
        // light left by fireflies goes out.
        List<int> hours = ShuffledHours();
        for (int i = 0; i < frontIds.Count && i < hours.Count; i++)
            model.SetClockHour(frontIds[i], hours[i]);
        front.ExtinguishAllEnemyLights();
        // After the shuffle the player no longer knows where anyone is.
        revealedRooms.Clear();
        activeEvent = ClockEvent.DarkStorm;
        activeEventEndsAfterTurn = completedTurn + Mathf.Max(1, stormDurationTurns);
        messages.Add("Тёмная буря! Фронты перемешались, весь свет погас");
    }

    private void StartCalm(int completedTurn)
    {
        calmEnemyTurn = completedTurn + 1;
        activeEvent = ClockEvent.Calm;
        activeEventEndsAfterTurn = completedTurn + 1;
        front.RestoreBarricade(calmBarricadeRestore);
        messages.Add("Затишье: в следующий ход волн не будет, баррикада +"
            + Mathf.RoundToInt(calmBarricadeRestore));
    }

    private void StartFireflySwarm()
    {
        int bestId = -1;
        int bestCount = 0;
        foreach (int id in SpawningFrontsInRandomOrder())
        {
            LevelGenerator generator = front.GetGenerator(id);
            int count = generator != null ? generator.LivingEnemyCount : 0;
            if (count > bestCount) { bestCount = count; bestId = id; }
        }
        LevelGenerator target = front.GetGenerator(bestId);
        if (target == null) return;
        foreach (GameObject enemy in target.EnemyCollection)
        {
            if (enemy == null || !enemy.TryGetComponent(out Brick brick) || brick.IsDefeated) continue;
            EnemyIllumination light = enemy.GetComponent<EnemyIllumination>();
            if (light == null) light = enemy.AddComponent<EnemyIllumination>();
            light.Illuminate(1f);
        }
        messages.Add("Стая светлячков осветила всех врагов одного из фронтов");
    }

    private void StartGoldenAcorn()
    {
        foreach (int id in SpawningFrontsInRandomOrder())
        {
            LevelGenerator generator = front.GetGenerator(id);
            Brick spawned = generator != null
                ? generator.SpawnEventEnemy(goldenAcornHealthMultiplier, goldenAcornTint, true) : null;
            if (spawned == null) continue;
            generator.SummonGuardsAround(spawned, goldenAcornGuards);
            goldenAcorn = spawned;
            goldenAcornTracked = true;
            messages.Add("Где-то появился Золотой жёлудь! Найдите и расколите его");
            return;
        }
    }

    private void StartSplitFront(int completedTurn)
    {
        int id = PickClosedFront(-1);
        if (id < 0) return;
        splitFrontId = id;
        splitEndsAfterTurn = completedTurn + Mathf.Max(1, splitFrontDurationTurns);
        activeEvent = ClockEvent.SplitFront;
        activeEventEndsAfterTurn = splitEndsAfterTurn;
        OpenFront(id);
        messages.Add("Раскол фронта: на " + splitFrontDurationTurns + " хода нападает ещё один фронт");
    }

    private void CloseExpiredSplit(int completedTurn)
    {
        if (splitFrontId < 0 || completedTurn < splitEndsAfterTurn) return;
        spawning.Remove(splitFrontId);
        LevelGenerator generator = front.GetGenerator(splitFrontId);
        if (generator != null) front.RefreshRoom(generator);
        messages.Add("Дополнительный фронт больше не присылает волны");
        splitFrontId = -1;
        if (activeEvent == ClockEvent.SplitFront) activeEvent = ClockEvent.None;
    }

    private void UpdateBossRaid(int completedTurn)
    {
        int every = Mathf.Max(0, bossRaidEveryTurns);
        if (every == 0) return;
        if ((completedTurn + 1) % every == 0)
            messages.Add("Через ход: " + EventName(ClockEvent.BossRaid));
        if (completedTurn <= 0 || completedTurn % every != 0) return;
        List<LevelProgression.BossKind> pool = BossPool();
        LevelProgression.BossKind kind = pool[Random.Range(0, pool.Count)];
        foreach (int id in SpawningFrontsInRandomOrder())
        {
            LevelGenerator generator = front.GetGenerator(id);
            if (generator == null || generator.SpawnRaidBoss(kind) == null) continue;
            messages.Add("Набег босса! Найдите его, пока он не дошёл до центра");
            return;
        }
    }

    private static List<LevelProgression.BossKind> BossPool()
    {
        var pool = new List<LevelProgression.BossKind>();
        LevelProgression progression = LevelProgression.Instance;
        if (progression != null)
            for (int level = 1; level <= LevelProgression.LevelsPerLocation; level++)
            for (int line = 2; line <= 4; line++)
            {
                LevelProgression.BossKind kind = progression.GetBossKind(level, line, line);
                if (kind != LevelProgression.BossKind.None && !pool.Contains(kind)) pool.Add(kind);
            }
        if (pool.Count == 0) pool.Add(LevelProgression.BossKind.Lich);
        return pool;
    }

    private void CheckEventTargets()
    {
        if (eliteTracked && (elite == null || elite.IsDefeated))
        {
            eliteTracked = false;
            elite = null;
            if (activeEvent == ClockEvent.EliteEnemy) activeEvent = ClockEvent.None;
            manager.GrantEventUpgrade();
            if (!GameRunMode.IsDeveloperRun) MetaProgression.AwardRoomGold(eliteGoldReward);
            GameAnnouncementUI.Show("Элитный враг повержен! Улучшение и +" + eliteGoldReward + " золота", 2.5f);
            front?.NotifyChanged();
        }
        if (goldenAcornTracked && (goldenAcorn == null || goldenAcorn.IsDefeated))
        {
            goldenAcornTracked = false;
            goldenAcorn = null;
            manager.GrantEventUpgrade();
            if (!GameRunMode.IsDeveloperRun) MetaProgression.AwardRoomGold(goldenAcornGoldReward);
            GameAnnouncementUI.Show("Золотой жёлудь расколот! Улучшение и +" + goldenAcornGoldReward
                + " золота", 2.5f);
            front?.NotifyChanged();
        }
    }

    // Several things can happen at the end of one turn; show them together
    // because a new announcement replaces the previous one.
    private void FlushMessages()
    {
        if (messages.Count == 0) return;
        GameAnnouncementUI.Show(string.Join("\n", messages), 2.5f + messages.Count * 0.75f);
        messages.Clear();
    }

    // Short state line for the map header.
    public string StatusLine()
    {
        string line = "ФРОНТОВ " + spawning.Count + "  ·  СВЕТ "
            + Mathf.RoundToInt(CurrentLitFraction * 100f) + "%";
        if (activeEvent != ClockEvent.None) line += "  ·  " + EventName(activeEvent).ToUpperInvariant();
        else if (announcedEvent != ClockEvent.None)
            line += "  ·  СКОРО: " + EventName(announcedEvent).ToUpperInvariant();
        return line;
    }
}
