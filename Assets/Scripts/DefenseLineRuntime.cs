using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DefenseLineRuntime : MonoBehaviour
{
    [Header("Room income")]
    [SerializeField, Min(0)] private int goldPerSecuredRoom = 1;
    [Header("Main room barricade")]
    [SerializeField, Min(1f)] private float startingBarricadeHealth = MainRoomBarricade.DefaultMaxHealth;
    public const float DefaultOrdinaryBarricadeDamage = 5f;
    public const float DefaultBossBarricadeDamage = 25f;
    [SerializeField, Min(0f), Tooltip("Barricade damage dealt by one ordinary enemy breach, on every level.")]
    private float ordinaryBarricadeDamage = DefaultOrdinaryBarricadeDamage;
    [SerializeField, Min(0f), Tooltip("Barricade damage dealt by one boss breach, on every level.")]
    private float bossBarricadeDamage = DefaultBossBarricadeDamage;

    private readonly Dictionary<int, Stage> stages = new Dictionary<int, Stage>();
    private readonly Dictionary<int, LevelGenerator> generators = new Dictionary<int, LevelGenerator>();
    private readonly Dictionary<int, GameObject> lineRoots = new Dictionary<int, GameObject>();
    private readonly DefensePressureQueue<Brick> pressure = new DefensePressureQueue<Brick>();
    private GameManager gameManager;
    private CameraBehaviour roomCamera;
    private GameObject legacyLineButtons;
    private Transform travellingRoot;
    private bool roomsPrepared;
    private bool interfaceBound;
    private int pendingBreachFocusId = -1;
    private int pendingBreachFocusLine = int.MaxValue;
    private int nextPressureCohort = 1;
    private int defendedRoomId = -1;
    private MainRoomBarricade barricade;
    public void RestoreBarricade(float amount) => barricade?.RestoreHealth(amount);
    private readonly HashSet<int> warnedBreachRooms = new HashSet<int>();

    public DefenseFront Model { get; private set; } = new DefenseFront();
    public event Action Changed;
    public int CurrentRoomId { get; private set; } = -1;
    public int DefendedRoomId => defendedRoomId;
    public bool IsDefendingRoom => defendedRoomId >= 0;
    public int GoldPerSecuredRoom => GameRunMode.IsDeveloperRun
        ? 0 : Mathf.Max(0, goldPerSecuredRoom);
    public int IncomePerTurn => Model.Income(GoldPerSecuredRoom);
    public int PendingTransferEnemies => pressure.Count;
    public bool HasPendingPressureForRoom(int roomId)
    {
        foreach (DefensePressureQueue<Brick>.Passage passage in pressure.Passages)
        {
            DefenseFront.Room target = Model.GetRoom(passage.Target);
            while (target != null && target.State == DefenseFront.RoomState.Lost)
                target = Model.GetRoom(target.ParentId);
            if (target != null && target.Id == roomId) return true;
        }
        return false;
    }
    public bool IsQuiet => Model.IsQuiet(pressure.Count);
    public bool IsReady => generators.Count == 16;
    public bool IsCameraTransitioning => roomCamera != null && roomCamera.IsTransitioning;
    public int PendingBreachFocusId => pendingBreachFocusId;
    public DefenseFrontUI Interface { get; private set; }
    public MainRoomBarricade Barricade => barricade;
    public float BarricadeHealth => barricade != null ? barricade.CurrentHealth : 0f;

    // Fixed per breach in every mode, independent of the enemy's HP, the
    // campaign level or the survival turn.
    public float BarricadeDamageFor(Brick attacker)
    {
        if (attacker == null) return 0f;
        return Mathf.Max(0f, attacker.UsesBossHealth ? bossBarricadeDamage : ordinaryBarricadeDamage);
    }

    public static float BarricadeDamageOf(Brick attacker)
    {
        DefenseLineRuntime front = GameManager.Instance != null ? GameManager.Instance.Front : null;
        if (front != null) return front.BarricadeDamageFor(attacker);
        return attacker == null ? 0f
            : attacker.UsesBossHealth ? DefaultBossBarricadeDamage : DefaultOrdinaryBarricadeDamage;
    }
    public float BarricadeMaxHealth => barricade != null ? barricade.MaxHealth
        : Mathf.Max(1f, startingBarricadeHealth + MetaProgression.BarricadeHealthBonus);

    public static DefenseLineRuntime Ensure(GameManager manager, GameObject line2Root, GameObject line2Buttons)
    {
        DefenseLineRuntime runtime = manager.GetComponent<DefenseLineRuntime>();
        if (runtime == null) runtime = manager.gameObject.AddComponent<DefenseLineRuntime>();
        runtime.PrepareRooms(manager, line2Root, line2Buttons);
        return runtime;
    }

    public void PrepareRooms(GameManager manager, GameObject existingLine2Root, GameObject existingLine2ButtonsRoot)
    {
        if (roomsPrepared) return;
        roomsPrepared = true;
        gameManager = manager;
        legacyLineButtons = existingLine2ButtonsRoot;
        roomCamera = FindObjectOfType<CameraBehaviour>();
        if (existingLine2Root != null) lineRoots[2] = existingLine2Root;

        // Discover authored rooms before making missing outer rooms from the existing template.
        Stage[] authored = FindObjectsOfType<Stage>(true);
        foreach (Stage stage in authored)
        {
            for (int line = 1; line <= 4; line++)
            for (int index = 1; index <= line * 2 - 1; index++)
                if (stage.name == RoomName(line, index) || stage.name == "Line" + line + "_Room" + index)
                    RegisterStage(stage, line, index);
        }
        if (!stages.TryGetValue(DefenseFront.IdFor(2, 2), out Stage template))
        {
            Debug.LogError("Для карты обороны нужен шаблон Stage 2_2.");
            return;
        }
        CreateLineFromTemplate(3, 5, template);
        CreateLineFromTemplate(4, 7, template);
        foreach (var pair in generators)
            if (Model.GetRoom(pair.Key).Line > 1) stages[pair.Key].gameObject.SetActive(false);

        if (stages.TryGetValue(DefenseFront.IdFor(1, 1), out Stage mainStage))
        {
            barricade = mainStage.GetComponent<MainRoomBarricade>();
            if (barricade == null) barricade = mainStage.gameObject.AddComponent<MainRoomBarricade>();
            barricade.Initialize(mainStage,
                Mathf.Max(1f, startingBarricadeHealth + MetaProgression.BarricadeHealthBonus));
            barricade.Changed -= OnBarricadeChanged;
            barricade.Changed += OnBarricadeChanged;
        }

        travellingRoot = new GameObject("Enemies travelling between rooms").transform;
        travellingRoot.SetParent(transform, false);
        if (!IsReady) Debug.LogError("Карта обороны должна содержать ровно 16 комнат.");
    }

    public void BindInterface()
    {
        if (interfaceBound) return;
        interfaceBound = true;
        ConfigureCampaignRooms();
        // The connected map replaces legacy unconnected navigation buttons.
        foreach (Button button in FindObjectsOfType<Button>(true))
        {
            bool roomButton = button.name == "Stage 1Button";
            foreach (Stage stage in stages.Values) roomButton |= button.name == stage.name;
            if (roomButton) button.gameObject.SetActive(false);
        }
        if (legacyLineButtons != null) legacyLineButtons.SetActive(false);
        if (roomCamera != null) roomCamera.RoomChanged += OnRoomChanged;
        Interface = DefenseFrontUI.Ensure(this, gameManager);
        ActivateLine(1);
        if (gameManager.Clock != null)
        {
            gameManager.Clock.BeginRun(this);
            return;
        }
        if (gameManager.IsSurvivalMode || GameRunMode.IsDeveloperRun)
        {
            ActivateLine(2);
            ActivateLine(3);
            ActivateLine(4);
            if (GameRunMode.IsDeveloperRun)
                FocusRoom(DefenseFront.IdFor(1, 1), true);
        }
    }

    public void ActivateLine(int defenseLine, bool focusNewLine = true)
    {
        if (defenseLine != Model.OpenedLine + 1) return;
        Model.OpenLine(defenseLine);
        if (lineRoots.TryGetValue(defenseLine, out GameObject root) && root != null) root.SetActive(true);
        int focusRoomId = -1;
        int centerRoomId = DefenseFront.IdFor(defenseLine, defenseLine);
        foreach (DefenseFront.Room room in Model.Rooms)
        {
            if (room.Line != defenseLine || !generators.TryGetValue(room.Id, out LevelGenerator generator)) continue;
            if (!room.IsActive)
            {
                stages[room.Id].gameObject.SetActive(false);
                continue;
            }
            stages[room.Id].gameObject.SetActive(true);
            generator.InitializeRoom();
            RefreshRoom(generator);
            if (room.CanSelect && (focusRoomId < 0 || room.Id == centerRoomId)) focusRoomId = room.Id;
        }
        if (focusRoomId >= 0)
        {
            if (focusNewLine || CurrentRoomId < 0) FocusRoom(focusRoomId, true);
            WaveWarningUI.Show(defenseLine, 1,
                gameManager.IsSurvivalFinalLineActive && defenseLine == 4);
        }
        Changed?.Invoke();
    }

    private void ConfigureCampaignRooms()
    {
        if (gameManager.Clock != null)
        {
            gameManager.Clock.ConfigureModel(Model);
            return;
        }
        var activeRoomIds = new List<int>(16);
        LevelProgression progression = LevelProgression.Instance;
        foreach (DefenseFront.Room room in Model.Rooms)
            if (GameRunMode.IsDeveloperRun || progression == null || gameManager.IsSurvivalMode
                || progression.IsRoomActive(room.Line, room.Index))
                activeRoomIds.Add(room.Id);
        Model.ConfigureActiveRooms(activeRoomIds);
    }

    // Clock siege: opens one front and starts its first wave. Other rooms of
    // the same line stay hidden.
    public bool OpenClockFront(int id, bool focus)
    {
        DefenseFront.Room room = Model.GetRoom(id);
        if (room == null || !stages.TryGetValue(id, out Stage stage)
            || !generators.TryGetValue(id, out LevelGenerator generator)) return false;
        bool opened = Model.OpenRoom(id);
        if (!opened && room.State == DefenseFront.RoomState.Locked) return false;
        if (lineRoots.TryGetValue(room.Line, out GameObject root) && root != null) root.SetActive(true);
        stage.gameObject.SetActive(true);
        generator.InitializeRoom();
        RefreshRoom(generator);
        if (focus) FocusRoom(id, true);
        Changed?.Invoke();
        return true;
    }

    public void SetRoomLitFraction(int roomId, float fraction)
    {
        if (!generators.TryGetValue(roomId, out LevelGenerator generator) || generator == null) return;
        RoomDarkness darkness = generator.Darkness;
        if (darkness != null) darkness.SetLitOverride(fraction);
    }

    public int ExtinguishAllEnemyLights()
    {
        int extinguished = 0;
        foreach (LevelGenerator generator in generators.Values)
        {
            if (generator == null) continue;
            foreach (GameObject enemy in generator.EnemyCollection)
            {
                if (enemy == null || !enemy.TryGetComponent(out EnemyIllumination light)
                    || !light.IsIlluminated) continue;
                light.Extinguish();
                extinguished++;
            }
        }
        Changed?.Invoke();
        return extinguished;
    }

    public void NotifyChanged() => Changed?.Invoke();

    public LevelGenerator GetGenerator(int id)
    {
        generators.TryGetValue(id, out LevelGenerator generator);
        return generator;
    }

    public bool CanSelect(int id) => Model.GetRoom(id)?.CanSelect == true;

    public bool CanFocusWhileDefending(int id)
    {
        ReleaseLostDefendedRoom();
        return defendedRoomId < 0 || defendedRoomId == id;
    }

    public bool ToggleDefendCurrentRoom()
    {
        if (gameManager == null || !gameManager.CanSwitchRooms) return false;
        if (defendedRoomId == CurrentRoomId)
            defendedRoomId = -1;
        else if (CanSelect(CurrentRoomId))
            defendedRoomId = CurrentRoomId;
        else
            return false;
        Changed?.Invoke();
        return defendedRoomId >= 0;
    }

    private void ReleaseLostDefendedRoom()
    {
        if (defendedRoomId < 0 || CanSelect(defendedRoomId)) return;
        defendedRoomId = -1;
        Changed?.Invoke();
    }

    public bool FocusRoom(int id, bool force = false)
    {
        ReleaseLostDefendedRoom();
        if (!CanFocusWhileDefending(id)) return false;
        if (!CanSelect(id) || roomCamera == null || !stages.TryGetValue(id, out Stage stage)) return false;
        if (!force && !gameManager.CanSwitchRooms) return false;
        Player.BlockInputUntilReleased();
        if (force) roomCamera.ForceFocusRoom(stage);
        else roomCamera.FocusRoom(stage);
        return roomCamera.CurrentRoom == stage.transform;
    }

    private bool FocusRoomAnimated(int id)
    {
        ReleaseLostDefendedRoom();
        if (!CanFocusWhileDefending(id)) return false;
        if (!CanSelect(id) || roomCamera == null || !stages.TryGetValue(id, out Stage stage)) return false;
        Player.BlockInputUntilReleased();
        roomCamera.ForceFocusRoomAnimated(stage);
        return roomCamera.CurrentRoom == stage.transform;
    }

    private void OnRoomChanged(Stage selected)
    {
        foreach (var pair in stages)
            if (pair.Value == selected) { CurrentRoomId = pair.Key; break; }
        Changed?.Invoke();
    }

    public void RefreshRoom(LevelGenerator room)
    {
        if (room == null || !room.IsInitialized) return;
        Model.Observe(room.RoomId, room.EnemyCollection.Count, room.RemainingWaves);
        ReleaseLostDefendedRoom();
        if (room.ClosestBreachInTurns > 1) warnedBreachRooms.Remove(room.RoomId);
        Model.ClaimFirstClear(room.RoomId);
        // Clock fronts are not lines, so they never give line-clear bonuses.
        if (gameManager.Clock == null && Model.ClaimLineClear(room.DefenseLine))
            gameManager.RegisterLineCleared(room.DefenseLine);
        Changed?.Invoke();
    }

    public void RefreshAll()
    {
        foreach (LevelGenerator room in generators.Values) RefreshRoom(room);
    }

    public int TurnsUntilRoomBreach(int roomId)
    {
        DefenseFront.Room room = Model.GetRoom(roomId);
        if (room == null || room.State != DefenseFront.RoomState.Contested
            || !generators.TryGetValue(roomId, out LevelGenerator generator))
            return int.MaxValue;
        return generator.ClosestBreachInTurns;
    }

    // Share of the room's height that is lit, from 0 (dark) to 1 (fully lit).
    public float RoomLitFraction(int roomId)
    {
        if (!generators.TryGetValue(roomId, out LevelGenerator generator) || generator == null) return 1f;
        RoomDarkness darkness = generator.Darkness;
        return darkness != null && darkness.enabled ? Mathf.Clamp01(darkness.LitFraction) : 1f;
    }

    public float RoomBreachProgress(int roomId)
    {
        int turns = TurnsUntilRoomBreach(roomId);
        if (turns == int.MaxValue || !generators.TryGetValue(roomId, out LevelGenerator generator))
            return 0f;
        // The first spawn row starts empty; the final safe row is fully filled.
        int longestApproach = Mathf.Max(2, generator.Rows - 1);
        return Mathf.Clamp01((longestApproach - turns) / (float)(longestApproach - 1));
    }

    public int NotifyApproachingBreaches()
    {
        // The clock siege hides where the enemies are, including breach warnings.
        if (gameManager.Clock != null) return 0;
        int newlyDangerous = 0;
        foreach (DefenseFront.Room room in Model.Rooms)
        {
            if (TurnsUntilRoomBreach(room.Id) > 1)
            {
                warnedBreachRooms.Remove(room.Id);
                continue;
            }
            if (warnedBreachRooms.Add(room.Id)) newlyDangerous++;
        }
        if (newlyDangerous == 1)
            GameAnnouncementUI.Show("В одной комнате готовится прорыв! Проверьте карту обороны.", 3f);
        else if (newlyDangerous > 1)
            GameAnnouncementUI.Show("В нескольких комнатах готовится прорыв ("
                + newlyDangerous + ")! Проверьте карту обороны.", 3.5f);
        return newlyDangerous;
    }

    public void RoomCompleted(LevelGenerator room)
    {
        RefreshRoom(room);
    }

    public void BreachRoom(LevelGenerator room, IReadOnlyList<Brick> requestedAttackers = null)
    {
        if (room == null || gameManager.GameEnded) return;
        if (room.DefenseLine == 1)
        {
            ResolveMainRoomBreach(room, requestedAttackers);
            return;
        }
        BeaverDam dam = FindDam(room);
        if (dam != null && requestedAttackers != null && requestedAttackers.Count > 0)
        {
            float incomingDamage = 0f;
            for (int i = 0; i < requestedAttackers.Count; i++)
            {
                Brick attacker = requestedAttackers[i];
                if (attacker != null && !attacker.IsDefeated && attacker.levelGenerator == room)
                    incomingDamage += attacker.UsesBossHealth
                        ? attacker.BreachDamage : Mathf.Max(1f, attacker.ProjectileContactDamage);
            }
            if (incomingDamage > 0f)
            {
                bool held = dam.Health >= incomingDamage;
                dam.Absorb(incomingDamage);
                if (held)
                {
                    // The room-wide retreat also hides enemies that did not attack.
                    // Restore everyone before starting the attackers' return animation.
                    for (int i = 0; i < room.EnemyCollection.Count; i++)
                    {
                        GameObject enemy = room.EnemyCollection[i];
                        if (enemy != null && enemy.TryGetComponent(out Brick resident)
                            && !resident.IsDefeated && resident.levelGenerator == room)
                            resident.CancelBreachAnimation();
                    }
                    for (int i = 0; i < requestedAttackers.Count; i++)
                    {
                        Brick attacker = requestedAttackers[i];
                        if (attacker == null || attacker.IsDefeated || attacker.levelGenerator != room) continue;
                        if (room.TryReturnBreachingEnemy(attacker, out Vector3 position))
                            attacker.StrikeBarricadeAndReturn(position);
                        else attacker.PlayArrivalAnimation();
                    }
                    RefreshRoom(room);
                    GameAnnouncementUI.Show("Плотина удержала прорыв!", 2f);
                    return;
                }
            }
        }
        // Rooms are no longer lost or locked by a breach. Only the enemies that
        // reached the final row move inward; the room keeps its other enemies,
        // its remaining waves and stays selectable.
        DefenseFront.Room breachedRoom = Model.GetRoom(room.RoomId);
        DefenseFront.Room destination = breachedRoom != null ? Model.GetRoom(breachedRoom.ParentId) : null;
        if (destination == null) return;

        var attackers = new List<Brick>();
        if (requestedAttackers != null)
            for (int i = 0; i < requestedAttackers.Count; i++)
            {
                Brick candidate = requestedAttackers[i];
                if (candidate != null && !candidate.IsDefeated
                    && candidate.levelGenerator == room && !attackers.Contains(candidate))
                    attackers.Add(candidate);
            }
        if (attackers.Count == 0)
            for (int i = 0; i < room.EnemyCollection.Count; i++)
            {
                GameObject candidateObject = room.EnemyCollection[i];
                if (candidateObject != null && candidateObject.TryGetComponent(out Brick candidate)
                    && !candidate.IsDefeated
                    && candidate.LowestCellY <= room.DefenseBoundaryY + 0.0001f)
                    attackers.Add(candidate);
            }
        if (attackers.Count == 0) return;

        int cohort = CreatePressureCohort();
        int moved = 0;
        foreach (Brick attacker in attackers)
        {
            if (!room.ReleaseBreachingEnemy(attacker)) continue;
            SendToInnerRoom(room, attacker, 0, cohort);
            moved++;
        }
        if (moved == 0) return;

        RefreshRoom(room);
        QueueBreachFocus(destination);
        // In the clock siege the source front stays secret.
        if (gameManager.Clock != null)
            GameAnnouncementUI.Show("Враги прорвались в центр!", 2.5f);
        else
            GameAnnouncementUI.Show("Прорыв из " + breachedRoom.Label + " в " + destination.Label
                + (moved > 1 ? " · врагов: " + moved : ""), 2.5f);
        Changed?.Invoke();
    }

    public void ResolveMainRoomBreach(LevelGenerator room, IReadOnlyList<Brick> requestedAttackers)
    {
        if (room == null || room.DefenseLine != 1 || gameManager.GameEnded) return;
        var attackers = new List<Brick>();
        if (requestedAttackers != null)
        {
            for (int i = 0; i < requestedAttackers.Count; i++)
            {
                Brick candidate = requestedAttackers[i];
                if (candidate != null && !candidate.IsDefeated
                    && candidate.levelGenerator == room && !attackers.Contains(candidate))
                    attackers.Add(candidate);
            }
        }

        // Keep direct/legacy breach calls useful by resolving enemies that are
        // physically standing on the final row.
        if (attackers.Count == 0)
        {
            for (int i = 0; i < room.EnemyCollection.Count; i++)
            {
                GameObject candidateObject = room.EnemyCollection[i];
                if (candidateObject != null && candidateObject.TryGetComponent(out Brick candidate)
                    && !candidate.IsDefeated
                    && candidate.LowestCellY <= room.DefenseBoundaryY + 0.0001f)
                    attackers.Add(candidate);
            }
        }
        if (attackers.Count == 0) return;

        float totalDamage = 0f;
        for (int i = 0; i < attackers.Count; i++)
        {
            Brick attacker = attackers[i];
            if (attacker == null) continue;
            if (!room.TryReturnBreachingEnemy(attacker, out Vector3 returnPosition))
            {
                // With every safe position occupied by other bosses, keep this
                // boss on the barricade instead of stacking bosses or silently
                // discarding its breach damage. It can attack again next turn.
                if (!attacker.UsesBossHealth)
                {
                    // The breach animation fades this enemy out. If there is no
                    // return cell, it remains in this room and must be visible.
                    attacker.PlayArrivalAnimation();
                    continue;
                }
                returnPosition = attacker.transform.position;
            }
            totalDamage += attacker.StrikeBarricadeAndReturn(returnPosition);
        }
        if (totalDamage <= 0f) return;

        bool destroyed = barricade == null || barricade.TakeDamage(totalDamage);
        RefreshRoom(room);
        if (destroyed)
        {
            Model.LoseRoom(room.RoomId);
            GameAnnouncementUI.Show("Баррикада разрушена!", 2f);
            gameManager.LoseGame();
        }
        else
        {
            GameAnnouncementUI.Show("Баррикада получила " + Mathf.CeilToInt(totalDamage)
                + " урона · осталось " + Mathf.CeilToInt(barricade.CurrentHealth), 2f);
        }
        Changed?.Invoke();
    }

    private BeaverDam FindDam(LevelGenerator room)
    {
        if (room == null) return null;
        Stage stage = room.GetComponentInParent<Stage>();
        return stage != null ? stage.GetComponentInChildren<BeaverDam>(true) : null;
    }

    private void OnBarricadeChanged()
    {
        Changed?.Invoke();
    }

    private void QueueBreachFocus(DefenseFront.Room destination)
    {
        if (destination == null) return;
        // A lower destination line is more dangerous and wins. If destinations
        // share a line, assignment is intentional: the latest breach wins.
        if (pendingBreachFocusId < 0 || destination.Line <= pendingBreachFocusLine)
        {
            pendingBreachFocusId = destination.Id;
            pendingBreachFocusLine = destination.Line;
        }
    }

    public bool FocusPendingBreachRoom()
    {
        if (pendingBreachFocusId < 0) return false;
        // The clock siege never moves the camera to enemies: the player looks for them.
        if (gameManager.Clock != null)
        {
            pendingBreachFocusId = -1;
            pendingBreachFocusLine = int.MaxValue;
            return false;
        }
        ReleaseLostDefendedRoom();
        if (IsDefendingRoom)
        {
            pendingBreachFocusId = -1;
            pendingBreachFocusLine = int.MaxValue;
            return false;
        }
        DefenseFront.Room destination = Model.GetRoom(pendingBreachFocusId);
        pendingBreachFocusId = -1;
        pendingBreachFocusLine = int.MaxValue;
        while (destination != null && !destination.CanSelect)
            destination = Model.GetRoom(destination.ParentId);
        if (destination == null) return false;
        Interface?.CloseMap();
        return FocusRoomAnimated(destination.Id);
    }

    public int CreatePressureCohort()
    {
        if (nextPressureCohort == int.MaxValue) nextPressureCohort = 1;
        return nextPressureCohort++;
    }

    public void SendToInnerRoom(LevelGenerator source, Brick brick, int delayTurns = 0,
        int cohort = 0)
    {
        if (brick == null) return;
        DefenseFront.Room room = Model.GetRoom(source.RoomId);
        if (room == null || room.ParentId < 0) return;
        brick.levelGenerator = null;
        FrogRider.InvalidateRoomBonuses();
        brick.gameObject.SetActive(false);
        brick.transform.SetParent(travellingRoot, true);
        pressure.Enqueue(brick, room.Id, room.ParentId, gameManager.TurnNumber,
            Mathf.Max(0, delayTurns), cohort > 0 ? cohort : CreatePressureCohort());
    }

    public void RemoveTravellingEnemy(Brick brick)
    {
        if (brick != null && pressure.Remove(brick)) Changed?.Invoke();
    }

    public void DeliverPressure()
    {
        pressure.DeliverGroups(gameManager.TurnNumber, TryArriveGroup);
        Changed?.Invoke();
    }

    private bool TryArriveGroup(IReadOnlyList<DefensePressureQueue<Brick>.Passage> group)
    {
        if (group == null || group.Count == 0) return true;
        DefensePressureQueue<Brick>.Passage first = group[0];
        DefenseFront.Room target = Model.GetRoom(first.Target);
        var enemies = new List<Brick>(group.Count);
        for (int i = 0; i < group.Count; i++)
            if (group[i].Enemy != null) enemies.Add(group[i].Enemy);
        if (TryPlacePressureGroup(first.Source, target, enemies)) return true;

        // A crowded cohort must not keep a boss waiting behind its own lesser
        // escorts. Deliver bosses separately; leave only the escorts in the
        // passage if there is still no safe place for them.
        while (target != null && target.State == DefenseFront.RoomState.Lost)
            target = Model.GetRoom(target.ParentId);
        LevelGenerator destination = target != null ? GetGenerator(target.Id) : null;
        if (destination == null || !destination.IsInitialized || destination.IsLost) return false;

        bool deliveredBoss = false;
        bool waiting = false;
        for (int i = 0; i < group.Count; i++)
        {
            Brick enemy = group[i].Enemy;
            if (enemy == null) continue;
            if (!enemy.UsesBossHealth || !destination.TryReceiveBossByAbsorption(enemy))
            {
                waiting = true;
                continue;
            }
            pressure.Remove(enemy);
            deliveredBoss = true;
        }
        if (deliveredBoss)
        {
            RefreshRoom(destination);
            QueueBreachFocus(target);
        }
        return deliveredBoss && !waiting;
    }

    private bool TryPlacePressureGroup(int sourceId, DefenseFront.Room target, List<Brick> enemies)
    {
        // A previously lost room is not a travel stop. Follow the graph inward
        // in this same phase until the first position that can still be defended.
        while (target != null && target.State == DefenseFront.RoomState.Lost)
        {
            sourceId = target.Id;
            target = Model.GetRoom(target.ParentId);
        }
        if (target == null) return false;

        LevelGenerator destination = GetGenerator(target.Id);
        if (destination == null || !destination.IsInitialized)
        {
            Debug.LogError("Не удалось доставить группу прорыва из комнаты " + sourceId
                + ": целевая комната " + target.Label + " не инициализирована.");
            return false;
        }

        // Transfer cohorts wait outside while enemies and blocking graves occupy
        // at least two thirds of all cells in the destination room.
        if (destination.IsTransferOccupancyLimitReached)
        {
            QueueBreachFocus(target);
            return false;
        }

        if (!destination.TryReceivePressureGroup(enemies, true))
        {
            // A crowded destination is not a breach. Keep the group in the
            // passage until combat frees enough safe cells. Otherwise one outer
            // breach could cascade through several full arrival zones and cause
            // defeat without any enemy physically crossing line one's boundary.
            QueueBreachFocus(target);
            return false;
        }

        RefreshRoom(destination);
        // The destination may compete with an unfinished fight on the outer
        // line. Focus it on this same-phase arrival so the combat stays visible.
        QueueBreachFocus(target);
        return true;
    }

    public int PayTurnIncome()
    {
        int amount = Model.CompleteTurn(gameManager.TurnNumber, GoldPerSecuredRoom);
        if (!GameRunMode.IsDeveloperRun) MetaProgression.AwardRoomGold(amount);
        Changed?.Invoke();
        return amount;
    }

    public int LiberateCompletedLines(int completedLine)
    {
        // Travel and pending waves are part of the line battle, so no room returns early.
        if (!IsQuiet) return 0;
        int restored = Model.LiberateLostRoomsThroughLine(completedLine);
        if (restored == 0) return 0;
        foreach (DefenseFront.Room room in Model.Rooms)
        {
            if (room.Line > completedLine || room.State != DefenseFront.RoomState.Secured) continue;
            LevelGenerator generator = GetGenerator(room.Id);
            if (generator != null && generator.RestoreAfterLineClear()) RefreshRoom(generator);
        }
        Changed?.Invoke();
        return restored;
    }

    public void EnsureCombatFocus()
    {
        if (gameManager.Clock != null) return;
        ReleaseLostDefendedRoom();
        if (IsDefendingRoom) return;
        LevelGenerator current = GetGenerator(CurrentRoomId);
        if (current != null && !current.IsLost && current.HasLivingEnemies) return;
        foreach (DefenseFront.Room room in Model.Rooms)
        {
            LevelGenerator generator = GetGenerator(room.Id);
            if (room.CanSelect && generator != null && !generator.IsLost
                && generator.HasLivingEnemies)
            {
                FocusRoom(room.Id, true);
                return;
            }
        }
    }

    private void CreateLineFromTemplate(int line, int count, Stage template)
    {
        if (!lineRoots.TryGetValue(line, out GameObject root) || root == null)
        {
            root = new GameObject("Stage " + line + " Runtime");
            root.SetActive(false);
            lineRoots[line] = root;
        }
        for (int index = 1; index <= count; index++)
        {
            int id = DefenseFront.IdFor(line, index);
            if (stages.ContainsKey(id)) continue;
            GameObject clone = Instantiate(template.gameObject, root.transform, false);
            clone.name = RoomName(line, index);
            clone.transform.position = new Vector3(id * 30f, template.transform.position.y, template.transform.position.z);
            Stage stage = clone.GetComponent<Stage>();
            RegisterStage(stage, line, index);
        }
    }

    private void RegisterStage(Stage stage, int line, int index)
    {
        int id = DefenseFront.IdFor(line, index);
        stages[id] = stage;
        if (line > 1 && stage.transform.parent != null)
            lineRoots[line] = stage.transform.parent.gameObject;
        LevelGenerator room = stage.GetComponentInChildren<LevelGenerator>(true);
        if (room == null) return;
        room.ConfigureRoomIdentity(line, index);
        // Clock fronts share one battlefield size whatever line they came from.
        ClockSiegeDirector clock = gameManager != null ? gameManager.Clock : null;
        stage.ConfigureBattlefield(clock != null && line > 1 ? clock.FrontBattlefieldLine : line);
        generators[id] = room;
    }

    private void OnDestroy()
    {
        if (roomCamera != null) roomCamera.RoomChanged -= OnRoomChanged;
        if (barricade != null) barricade.Changed -= OnBarricadeChanged;
    }

    private static string RoomName(int line, int index) => line == 1 ? "Stage 1" : "Stage " + line + "_" + index;
}

[DisallowMultipleComponent]
public sealed class MainRoomBarricade : MonoBehaviour, IDefenseHealthTarget
{
    public const float DefaultMaxHealth = 100f;
    private const string VisualName = "Main Room Barricade Visual";
    private Stage stage;
    private SpriteRenderer artwork;

    public event Action Changed;
    public float MaxHealth { get; private set; } = DefaultMaxHealth;
    public float CurrentHealth { get; private set; } = DefaultMaxHealth;
    public bool IsDestroyed => CurrentHealth <= 0f;
    public float DefenseHealth => CurrentHealth;
    public float DefenseMaxHealth => MaxHealth;
    public Vector3 DefenseHealthAnchor
    {
        get
        {
            return stage != null ? stage.DefenseHealthAnchor : transform.position;
        }
    }

    public void Initialize(Stage owner, float maximumHealth)
    {
        stage = owner != null ? owner : GetComponent<Stage>();
        MaxHealth = Mathf.Max(1f, maximumHealth);
        CurrentHealth = MaxHealth;
        BuildVisual();
        RefreshVisual();
        EnemyCombatTextUI.Ensure().RegisterDefense(this);
        Changed?.Invoke();
    }

    public bool TakeDamage(float damage)
    {
        if (IsDestroyed || damage <= 0f) return IsDestroyed;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage * ArtifactEquipmentBonuses.DefenseDamageMultiplier);
        RefreshVisual();
        Changed?.Invoke();
        return IsDestroyed;
    }

    public void RestoreHealth(float amount)
    {
        if (IsDestroyed || amount <= 0f || CurrentHealth >= MaxHealth) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        RefreshVisual();
        Changed?.Invoke();
    }

    public void AddBeaverHealth(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;
        // Missing health is restored first; any remaining amount becomes
        // permanent overhealth while the base maximum stays unchanged.
        CurrentHealth += amount;
        RefreshVisual();
        Changed?.Invoke();
    }

    private void BuildVisual()
    {
        if (stage == null) return;
        artwork = stage.transform.Find(VisualName)?.GetComponent<SpriteRenderer>();
        if (artwork == null)
        {
            Debug.LogWarning("Main room barricade artwork is missing from the Stage 1 scene.", this);
            return;
        }
        artwork.sortingOrder = BattlefieldForestStyle.DefenseStructureSortingOrder;
        // Legacy Stage roots sit on the camera plane. Keep the authored sprite,
        // but place it at the battlefield depth if an older scene still has it there.
        Camera camera = Camera.main;
        if (camera != null && artwork.transform.position.z <= camera.transform.position.z
            + camera.nearClipPlane)
        {
            LevelGenerator battlefield = stage.GetComponentInChildren<LevelGenerator>(true);
            if (battlefield != null)
            {
                Vector3 position = artwork.transform.position;
                position.z = battlefield.transform.position.z;
                artwork.transform.position = position;
            }
        }
    }

    private void RefreshVisual()
    {
        if (artwork == null) return;
        float ratio = MaxHealth > 0f ? Mathf.Clamp01(CurrentHealth / MaxHealth) : 0f;
        artwork.color = Color.Lerp(new Color(0.72f, 0.55f, 0.46f, 1f),
            Color.white, ratio);
    }

    private void OnDestroy()
    {
        if (EnemyCombatTextUI.Instance != null)
            EnemyCombatTextUI.Instance.UnregisterDefense(this);
    }
}
