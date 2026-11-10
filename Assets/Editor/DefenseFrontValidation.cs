using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Batch validation runs in an isolated project copy, never in a live player's scene.
[InitializeOnLoad]
public static class DefenseFrontValidation
{
    private const string BatchKey = "ForestAnimalWar.DefenseValidation.Running";
    private const string ThreatOnlyKey = "ForestAnimalWar.DefenseValidation.ThreatOnly";
    private const string PrefsKey = "ForestAnimalWar.DefenseValidation.Prefs";
    private static IEnumerator routine;
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;
    private static int checks;
    private static readonly List<string> report = new List<string>();
    private static readonly string[] keys = {
        "ForestAnimalWar.Meta.Gold", "ForestAnimalWar.Meta.Acorns", "ForestAnimalWar.Meta.Victories",
        "ForestAnimalWar.CurrentLevel", "ForestAnimalWar.HighestUnlockedLevel",
        "ForestAnimalWar.MenuSelectedLevel", "ForestAnimalWar.RequestedLevel"
    };

    static DefenseFrontValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(BatchKey, false))
                BeginChecks();
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Directory.CreateDirectory("Logs");
        List<string> saved = new List<string>();
        foreach (string key in keys)
            saved.Add(PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key).ToString() : "-");
        SessionState.SetString(PrefsKey, string.Join("|", saved));
        SessionState.SetBool(BatchKey, true);
        PlayerPrefs.SetInt(keys[0], 0);
        PlayerPrefs.SetInt(keys[1], 0);
        PlayerPrefs.SetInt(keys[2], 0);
        PlayerPrefs.SetInt(keys[3], 1);
        PlayerPrefs.SetInt(keys[4], 1);
        PlayerPrefs.SetInt(keys[5], 1);
        // Level 5 is the Twilight Thicket finale: the only level of the
        // location with all four lines, which this flow walks through.
        LevelProgression.PrepareLevel(5);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        InstallEditableMap();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorApplication.isPlaying = true;
    }

    public static void RunThreatBatch()
    {
        SessionState.SetBool(ThreatOnlyKey, true);
        RunBatch();
    }

    [MenuItem("Forest Animal War/UI/Install editable defense map")]
    public static void InstallEditableMap()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/Asset.unity") return;
        DefenseFrontUI ui = UnityEngine.Object.FindObjectOfType<DefenseFrontUI>(true);
        if (ui == null)
        {
            GameObject root = new GameObject("Defense Front HUD", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DefenseFrontUI));
            Undo.RegisterCreatedObjectUndo(root, "Create defense map");
            ui = root.GetComponent<DefenseFrontUI>();
        }
        ui.BuildInterface();
        // Keep combat text settings as an ordinary editable scene object as well.
        EnemyCombatTextUI.Ensure();
        // Retire the old buttons in the saved scene as well as at runtime.
        foreach (Button button in UnityEngine.Object.FindObjectsOfType<Button>(true))
        {
            if (button.transform.IsChildOf(ui.transform)) continue;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) is CameraBehaviour)
                {
                    button.gameObject.SetActive(false);
                    break;
                }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    [MenuItem("Forest Animal War/UI/Bake breach danger visuals into gameplay scene")]
    public static void BakeBreachDangerMap()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before baking the map.");
        Scene scene = EditorSceneManager.OpenScene("Assets/Asset.unity", OpenSceneMode.Single);
        DefenseFrontUI ui = UnityEngine.Object.FindObjectOfType<DefenseFrontUI>(true);
        if (ui == null) throw new InvalidOperationException("Editable defense map is missing from Assets/Asset.unity.");
        ui.BuildInterface();
        SerializedObject serialized = new SerializedObject(ui);
        foreach (string propertyName in new[] { "breachFills", "breachHalos" })
        {
            SerializedProperty array = serialized.FindProperty(propertyName);
            if (array == null || array.arraySize != 16)
                throw new InvalidOperationException(propertyName + " must contain sixteen editable room images.");
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    throw new InvalidOperationException(propertyName + " is missing room " + i);
        }
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Breach danger map baked into editable Assets/Asset.unity: 16 fills and 16 halos.");
    }

    private static void BeginChecks()
    {
        deadline = EditorApplication.timeSinceStartup + 120;
        routine = SessionState.GetBool(ThreatOnlyKey, false) ? ThreatChecks() : Checks();
        stack.Push(routine);
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out.");
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Check(bool ok, string description)
    {
        if (!ok) throw new Exception(description);
        checks++;
        report.Add("PASS " + description);
    }

    private static IEnumerator ThreatChecks()
    {
        yield return null;
        yield return null;
        DefenseLineRuntime front = GameManager.Instance.Front;
        Check(front != null && front.IsReady, "Threat test has sixteen ready rooms");
        front.ActivateLine(2);
        yield return null;
        LevelGenerator firstRoom = front.GetGenerator(1);
        LevelGenerator secondRoom = front.GetGenerator(2);
        Check(firstRoom.HasEnemies && secondRoom.HasEnemies,
            "Two second-line rooms contain active enemies");
        Brick first = firstRoom.EnemyCollection[0].GetComponent<Brick>();
        Brick second = secondRoom.EnemyCollection[0].GetComponent<Brick>();
        Vector3 firstStart = first.transform.position;
        Vector3 secondStart = second.transform.position;
        first.transform.position = new Vector3(firstStart.x,
            firstRoom.DefenseBoundaryY + 0.5f, firstStart.z);
        second.transform.position = new Vector3(secondStart.x,
            secondRoom.DefenseBoundaryY + 0.5f, secondStart.z);
        front.RefreshRoom(firstRoom);
        front.RefreshRoom(secondRoom);
        Check(front.TurnsUntilRoomBreach(1) == 1 && front.TurnsUntilRoomBreach(2) == 1
            && Mathf.Approximately(front.RoomBreachProgress(1), 1f)
            && Mathf.Approximately(front.RoomBreachProgress(2), 1f),
            "Closest enemies fully fill miniatures one turn from breach");
        Check(front.NotifyApproachingBreaches() == 2 && front.NotifyApproachingBreaches() == 0,
            "Two simultaneous threats generate one warning, not a warning each turn");
        Text announcement = GameAnnouncementUI.GetOrCreate()
            .GetComponentsInChildren<Text>(true).First(text => text.text.Contains("прорыв"));
        Check(announcement.text.Contains("нескольких") && announcement.text.Contains("(2)"),
            "Aggregated warning names the number of threatened rooms");
        Check(front.Interface.OpenMap(true), "Threatened defense map opens");
        yield return null;
        Image fill = front.Interface.GetComponentsInChildren<Image>(true)
            .First(image => image.name == "Burgundy Breach Fill"
                && image.transform.parent.name == "Room 2-1");
        Image[] halos = front.Interface.GetComponentsInChildren<Image>(true)
            .Where(image => image.name == "Breach Halo 2-1" || image.name == "Breach Halo 2-2")
            .ToArray();
        Check(fill.enabled && Mathf.Approximately(fill.rectTransform.anchorMax.y, 1f)
            && halos.Length == 2 && halos.All(image => image.enabled),
            "Map shows burgundy fill and both danger halos");
        first.transform.position = firstStart;
        second.transform.position = secondStart;
        front.RefreshRoom(firstRoom);
        front.RefreshRoom(secondRoom);
        yield return null;
        Check(halos.All(image => !image.enabled) && front.NotifyApproachingBreaches() == 0,
            "Removing danger clears map highlights and warning state");
        first.transform.position = new Vector3(firstStart.x,
            firstRoom.DefenseBoundaryY + 0.5f, firstStart.z);
        front.RefreshRoom(firstRoom);
        Check(front.NotifyApproachingBreaches() == 1,
            "A fresh approach after danger clears warns once again");
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        DefenseLineRuntime front = manager.Front;
        LevelProgression progression = LevelProgression.Instance;
        int[] outerPattern = { 1, 2, 3, 4, 5, 6, 7 };
        Check(LevelProgression.LocationCount == 6 && LevelProgression.LevelsPerLocation == 5
            && LevelProgression.MaxLevel == 30
            && LevelProgression.LocationNameForIndex(1) == "Сумеречная чаща"
            && LevelProgression.LocationNameForIndex(2) == "Мрачная лощина"
            && LevelProgression.LocationNameForIndex(3) == "Топь утопленников"
            && LevelProgression.LocationNameForIndex(4) == "Забытый залив"
            && LevelProgression.LocationNameForIndex(5) == "Снежная мгла"
            && LevelProgression.LocationNameForIndex(6) == "Геенна огненная",
            "Campaign contains six named five-level locations");
        for (int location = 1; location <= LevelProgression.LocationCount; location++)
        {
            for (int localLevel = 1; localLevel <= LevelProgression.LevelsPerLocation; localLevel++)
            {
                int globalLevel = LevelProgression.GlobalLevelFor(location, localLevel);
                int expectedRooms = localLevel <= 2 ? 4 : localLevel <= 4 ? 9 : 16;
                Check(front.Model.Rooms.Count(r => progression.IsRoomActive(globalLevel, r.Line, r.Index)) == expectedRooms
                    && (localLevel < 5 || Enumerable.Range(1, 7)
                        .Where(index => progression.IsRoomActive(globalLevel, 4, index)).SequenceEqual(outerPattern)),
                    "Location " + location + " level " + localLevel + " uses " + expectedRooms + " rooms");
                Check(LevelProgression.StartingUpgradeChoicesForLevel(globalLevel) == 0,
                    "Location " + location + " level " + localLevel
                    + " grants no free starting upgrades");
            }
        }
        Check(progression.IsBossRoom(5, 4, 4) && progression.IsBossRoom(4, 4, 4)
            && progression.IsBossRoom(1, 2, 2) && progression.IsBossRoom(1, 3, 3)
            && !progression.IsBossRoom(1, 1, 1),
            "Every level reserves exactly the three central boss rooms outside line one");
        Check(progression.IsBossRoom(10, 4, 4)
            && progression.IsBossRoom(9, 4, 4)
            && LevelProgression.LocationIndexForLevel(6) == 2
            && LevelProgression.LocalLevelFor(6) == 1,
            "Gloomy Hollow reserves 4-4 on every level, including fifth-level Ghostly Sorrow");
        Check(progression.IsBossRoom(15, 4, 4)
            && progression.IsBossRoom(14, 4, 4),
            "Drowned Bog reserves room 4-4 on every level, including fifth-level Poisonous Gloom");
        Check(DefenseFront.ParentIdFor(3, 1) == DefenseFront.IdFor(2, 1)
            && DefenseFront.ParentIdFor(3, 2) == DefenseFront.IdFor(2, 1)
            && DefenseFront.ParentIdFor(3, 3) == DefenseFront.IdFor(2, 2)
            && DefenseFront.ParentIdFor(3, 4) == DefenseFront.IdFor(2, 3)
            && DefenseFront.ParentIdFor(3, 5) == DefenseFront.IdFor(2, 3),
            "Third-line breaches use the exact 2-1, 2-1, 2-2, 2-3, 2-3 mapping");
        Check(DefenseFront.ParentIdFor(4, 1) == DefenseFront.IdFor(3, 1)
            && DefenseFront.ParentIdFor(4, 2) == DefenseFront.IdFor(3, 1)
            && DefenseFront.ParentIdFor(4, 3) == DefenseFront.IdFor(3, 2)
            && DefenseFront.ParentIdFor(4, 4) == DefenseFront.IdFor(3, 3)
            && DefenseFront.ParentIdFor(4, 5) == DefenseFront.IdFor(3, 4)
            && DefenseFront.ParentIdFor(4, 6) == DefenseFront.IdFor(3, 5)
            && DefenseFront.ParentIdFor(4, 7) == DefenseFront.IdFor(3, 5),
            "Fourth-line breaches use the exact requested seven-room mapping");
        DefenseFront lossRules = new DefenseFront();
        lossRules.ConfigureActiveRooms(Enumerable.Range(0, 16));
        for (int line = 1; line <= 4; line++) lossRules.OpenLine(line);
        // Outer rooms are never lost or locked by a breach. Only the main room
        // can fall, and only when its barricade is destroyed.
        lossRules.LoseRoom(DefenseFront.IdFor(1, 1));
        Check(lossRules.GetRoom(DefenseFront.IdFor(1, 1)).State == DefenseFront.RoomState.Lost
            && lossRules.Rooms.Where(room => room.Line > 1).All(room => room.CanSelect),
            "Losing the main room never locks any outer room");
        Check(front.IsReady && front.Model.Rooms.Count == 16, "Sixteen live rooms registered");
        Check(front.CurrentRoomId == 0, "Main room initially selected");
        Check(front.GetGenerator(0).HasEnemies, "First wave spawned");
        Check(front.Model.GetRoom(1).State == DefenseFront.RoomState.Locked,
            "Second line waits for its scheduled turn");
        Check(front.Model.Rooms.Where(r => r.Line == 4)
            .All(r => r.State == DefenseFront.RoomState.Locked),
            "All seven fourth-line rooms wait for their scheduled line");
        foreach (var room in front.Model.Rooms)
        {
            LevelGenerator generator = front.GetGenerator(room.Id);
            Check(generator.Columns == 9 - room.Line && generator.Rows == 11 - room.Line,
                "Room " + room.Label + " uses its defense line's grid size");
        }

        EnemyCombatTextUI combatText = EnemyCombatTextUI.Ensure();
        yield return null;
        Vector3 combatTextScale = combatText.transform.lossyScale;
        Check(combatTextScale.x > 0.01f && combatTextScale.y > 0.01f,
            "Combat text canvas has a visible non-zero scale");
        Brick healthSample = front.GetGenerator(0).EnemyCollection[0].GetComponent<Brick>();
        Check(combatText.RegisteredEnemyCount >= front.GetGenerator(0).EnemyCollection.Count,
            "Every spawned enemy registers a numeric health label");
        Check(combatText.VisibleHealthLabelCount == front.GetGenerator(0).EnemyCollection.Count,
            "Only enemies visible in the current room display health labels");
        Check(combatText.GetHealthText(healthSample) == CompactNumberFormatter.Format(healthSample.hp),
            "Health label shows the enemy's current numeric HP");
        float healthBeforeTextTest = healthSample.hp;
        healthSample.TakeDamage(55f);
        yield return null;
        Check(combatText.GetHealthText(healthSample) == CompactNumberFormatter.Format(healthBeforeTextTest - 55f),
            "Health number updates immediately after damage");
        Check(combatText.ActiveDamageNumberCount == 1, "Damage creates one floating number");
        WriteCombatTextDiagnostics(combatText, healthSample);
        yield return CaptureCombatText(combatText);
        float damageTimeout = Time.realtimeSinceStartup + 2f;
        while (combatText.ActiveDamageNumberCount > 0 && Time.realtimeSinceStartup < damageTimeout) yield return null;
        Check(combatText.ActiveDamageNumberCount == 0, "Floating damage rises and disappears");

        // Real callback path: clearing the room and choosing upgrades advances
        // exactly once. Lines then open on their turn schedule, not on clears.
        int turn = manager.TurnNumber;
        Kill(front.GetGenerator(0));
        for (int i = 0; i < 6; i++)
        {
            if (manager.IsChoosingUpgrade) manager.SelectProjectileUpgrade(0);
            yield return null;
        }
        Check(manager.TurnNumber == turn + 1, "Clear + upgrade advances exactly one enemy turn");
        Check(MetaProgression.Gold >= 1, "Liberated main room pays at end of turn");
        yield return AdvanceUntilLine(manager, 2);
        Check(manager.stage == 2, "Line two opens on its scheduled turn");
        Check(front.GetGenerator(0) != null && front.Model.GetRoom(0).State == DefenseFront.RoomState.Secured,
            "Secured room retains its controller for reoccupation");
        Check(front.FocusRoom(2), "Accessible room can be selected");
        Check(front.CurrentRoomId == 2, "Selection follows the camera");
        Button defendRoom = front.Interface.DefendRoomButton;
        Check(defendRoom != null, "Editable HUD contains the defend-current-room button");
        defendRoom.onClick.Invoke();
        Check(front.DefendedRoomId == 2 && !front.FocusRoom(1, true)
                && front.CurrentRoomId == 2,
            "Defending a room blocks forced camera focus elsewhere");
        CameraBehaviour defenseCamera = UnityEngine.Object.FindObjectOfType<CameraBehaviour>();
        defenseCamera.ForceFocusRoom(front.GetGenerator(1).GetComponentInParent<Stage>());
        Check(front.CurrentRoomId == 2, "Direct camera requests also respect room defense");
        defendRoom.onClick.Invoke();
        Check(!front.IsDefendingRoom && front.FocusRoom(1, true),
            "Turning room defense off restores room switching");
        front.FocusRoom(2, true);
        front.Interface.ToggleMap();
        yield return null;
        Check(front.Interface.IsMapOpen && manager.IsFrontMapOpen, "Map opens and blocks shooting");
        var graphics = front.Interface.GetComponentsInChildren<Graphic>(true);
        Check(graphics.All(g => g.GetComponent<CanvasRenderer>() != null), "Map graphics have CanvasRenderer");
        Check(front.Interface.GetComponentsInChildren<Button>(true)
            .All(button => button.name != "Wait For Pressure" || !button.gameObject.activeSelf),
            "Manual pressure wait button stays removed from the HUD");
        var borders = front.Interface.GetComponentsInChildren<Image>(true).Where(g => g.name.StartsWith("Selected ")).ToArray();
        Check(borders.Count(g => g.enabled) == 1 && borders.Single(g => g.enabled).name == "Selected 2-2",
            "Exactly the selected room is highlighted");
        Check(front.Interface.GetComponentsInChildren<Text>(true)
            .Any(label => label.text.StartsWith("4-4") && label.text.Contains("НЕТ НА УРОВНЕ")),
            "Map explains that inactive rooms are absent from this level");
        yield return CaptureMap(front.Interface);
        front.Interface.CloseMap();

        // Keep damaged enemies alive and force one of them across the room boundary.
        LevelGenerator outer = front.GetGenerator(2);
        Brick traveller = outer.EnemyCollection[0].GetComponent<Brick>();
        Brick siblingResident = front.GetGenerator(1).EnemyCollection.Select(enemy => enemy.GetComponent<Brick>())
            .FirstOrDefault(enemy => enemy != null);
        Brick roomResident = outer.EnemyCollection.Skip(1).Select(enemy => enemy.GetComponent<Brick>())
            .FirstOrDefault(enemy => enemy != null);
        traveller.hp = 2.5f;
        float hpBefore = traveller.hp;
        int xpBefore = manager.CurrentExperience;
        traveller.transform.position = new Vector3(outer.transform.position.x, outer.DefenseBoundaryY + 0.5f, outer.transform.position.z);
        int breachTurn = manager.TurnNumber;
        defendRoom.onClick.Invoke();
        Check(front.DefendedRoomId == 2, "The threatened room can be defended");
        yield return Advance(manager);
        LevelGenerator main = front.GetGenerator(0);
        Check(!manager.GameEnded && front.Model.Rooms.Where(r => r.Line == 2)
            .All(r => r.CanSelect && r.State != DefenseFront.RoomState.Lost && !front.GetGenerator(r.Id).IsLost),
            "A breach never loses or locks any second-line room");
        Check(manager.TurnNumber == breachTurn + 1 && main.EnemyCollection.Contains(traveller.gameObject)
            && traveller.levelGenerator == main && !outer.EnemyCollection.Contains(traveller.gameObject),
            "Only the crossing enemy moves into the main room in the same enemy phase");
        Check(siblingResident != null && siblingResident.gameObject.activeSelf
            && siblingResident.levelGenerator == front.GetGenerator(1),
            "Residents of sibling rooms stay where they are");
        Check(roomResident == null || (roomResident.gameObject.activeSelf && roomResident.levelGenerator == outer),
            "Other residents of the breached room stay in that room");
        Check(front.PendingTransferEnemies == 0, "No delayed cohorts are queued by a breach");
        Check(front.IsDefendingRoom && front.DefendedRoomId == 2 && front.CurrentRoomId == 2,
            "A breach keeps the defended room selected");
        defendRoom.onClick.Invoke();
        Check(!front.IsDefendingRoom && front.FocusRoom(0, true) && front.FocusRoom(2, true),
            "The breached room stays selectable after the breach");
        front.FocusRoom(0, true);
        Check(traveller != null && Mathf.Approximately(traveller.hp, hpBefore), "Transfer preserves health");
        Check(manager.CurrentExperience == xpBefore, "Transfer grants no kill or clear XP");
        Check(front.Model.GetRoom(0).State == DefenseFront.RoomState.Contested,
            "Immediate arrival reoccupies the main room");
        Check(main.PressureRows == 7, "First-line pressure uses seven upper rows");
        Check(main.EnemyCollection.All(e => IsInsidePressureZone(e, main)),
            "Arrivals occupy cells inside the first-line pressure zone");
        Check(main.EnemyCollection.Select(e => e.transform.localPosition).Distinct().Count()
                == main.EnemyCollection.Count,
            "The arriving cohort never overlaps itself");
        Check(traveller != null && Mathf.Approximately(traveller.hp, hpBefore), "Health unchanged after activation in destination");
        // Let Player initialize the newly focused room's ordinary player turn
        // before the validation simulates destroying that room's enemies.
        while (manager.IsResolvingEnemyTurn || manager.stepPlayer) yield return null;
        Kill(main);
        for (int i = 0; i < 6 && manager.IsChoosingUpgrade; i++)
        {
            manager.SelectProjectileUpgrade(0);
            yield return null;
        }
        front.RefreshRoom(main);
        Check(main.EnemyCollection.Count == 0 && front.Model.GetRoom(0).CanSelect,
            "Killing the arrival clears the main room without any line collapse");
        yield return AdvanceUntilLine(manager, 3);
        Check(!manager.GameEnded && manager.stage == 3, "Line three opens on its scheduled turn");
        Check(front.Model.Rooms.Where(r => r.Line == 3).All(r => r.CanSelect && front.GetGenerator(r.Id).HasEnemies),
            "All five third-line rooms become playable");
        Check(front.Model.Rooms.Where(r => r.Line <= 2).All(r => r.CanSelect
            && r.State != DefenseFront.RoomState.Lost && !front.GetGenerator(r.Id).IsLost),
            "Inner rooms remain accessible throughout");
        for (int i = 0; i < 12 && manager.IsChoosingUpgrade; i++)
        {
            manager.SelectProjectileUpgrade(0);
            yield return null;
        }
        Check(!manager.IsChoosingUpgrade,
            "Room-clear experience earned at the enemy-phase boundary is resolved before navigation");
        yield return ClickRoom(front, 1, true);
        yield return ClickRoom(front, 2, true);
        yield return ClickRoom(front, 3, true);

        // Two simultaneous third-line breaches: each sends only its crossing
        // enemy inward; no branch is closed and every room stays playable.
        int leftAId = DefenseFront.IdFor(3, 1);
        int leftBId = DefenseFront.IdFor(3, 2);
        int middleId = DefenseFront.IdFor(3, 3);
        int rightAId = DefenseFront.IdFor(3, 4);
        int rightBId = DefenseFront.IdFor(3, 5);
        int leftTargetId = DefenseFront.IdFor(2, 1);
        int rightTargetId = DefenseFront.IdFor(2, 3);
        LevelGenerator leftBreachRoom = front.GetGenerator(leftBId);
        LevelGenerator rightBreachRoom = front.GetGenerator(rightAId);
        int leftSourceBefore = leftBreachRoom.EnemyCollection.Count;
        int rightSourceBefore = rightBreachRoom.EnemyCollection.Count;
        int leftSiblingBefore = front.GetGenerator(leftAId).EnemyCollection.Count;
        int rightSiblingBefore = front.GetGenerator(rightBId).EnemyCollection.Count;
        int leftTargetBefore = front.GetGenerator(leftTargetId).EnemyCollection.Count;
        int rightTargetBefore = front.GetGenerator(rightTargetId).EnemyCollection.Count;
        GameObject leftCrosser = leftBreachRoom.EnemyCollection[0];
        GameObject rightCrosser = rightBreachRoom.EnemyCollection[0];
        leftCrosser.transform.position = new Vector3(
            leftBreachRoom.transform.position.x, leftBreachRoom.DefenseBoundaryY,
            leftBreachRoom.transform.position.z);
        rightCrosser.transform.position = new Vector3(
            rightBreachRoom.transform.position.x, rightBreachRoom.DefenseBoundaryY,
            rightBreachRoom.transform.position.z);
        front.BreachRoom(leftBreachRoom);
        front.BreachRoom(rightBreachRoom);
        Check(new[] { leftAId, leftBId, middleId, rightAId, rightBId }.All(id =>
                front.Model.GetRoom(id).CanSelect && !front.GetGenerator(id).IsLost),
            "Third-line breaches keep every third-line room selectable");
        Check(leftBreachRoom.EnemyCollection.Count == leftSourceBefore - 1
            && rightBreachRoom.EnemyCollection.Count == rightSourceBefore - 1
            && front.GetGenerator(leftAId).EnemyCollection.Count == leftSiblingBefore
            && front.GetGenerator(rightBId).EnemyCollection.Count == rightSiblingBefore,
            "Only the crossing enemies leave; sibling rooms are untouched");
        front.DeliverPressure();
        Check(front.PendingTransferEnemies == 0
            && front.GetGenerator(leftTargetId).EnemyCollection.Count == leftTargetBefore + 1
            && front.GetGenerator(rightTargetId).EnemyCollection.Count == rightTargetBefore + 1
            && front.GetGenerator(leftTargetId).EnemyCollection.Contains(leftCrosser)
            && front.GetGenerator(rightTargetId).EnemyCollection.Contains(rightCrosser)
            && front.Model.GetRoom(leftTargetId).State == DefenseFront.RoomState.Contested
            && front.Model.GetRoom(rightTargetId).State == DefenseFront.RoomState.Contested,
            "Each crossing enemy arrives in its parent room in the same phase");
        Check(front.FocusPendingBreachRoom(),
            "Immediate simultaneous breaches provide one final camera destination");
        while (front.IsCameraTransitioning) yield return null;
        front.Interface.ToggleMap();
        yield return null;
        Text leftBattleLabel = front.Interface.GetComponentsInChildren<Text>(true)
            .Single(label => label.text.StartsWith("2-1\n"));
        Text rightBattleLabel = front.Interface.GetComponentsInChildren<Text>(true)
            .Single(label => label.text.StartsWith("2-3\n"));
        Check(leftBattleLabel.text.Contains("враг.") && rightBattleLabel.text.Contains("враг.")
            && Mathf.Approximately(leftBattleLabel.transform.parent.GetComponent<Image>().color.r, 0.46f)
            && Mathf.Approximately(rightBattleLabel.transform.parent.GetComponent<Image>().color.r, 0.46f),
            "Breached destination rooms are immediately shown as brown active battles");
        front.Interface.CloseMap();
        Check(front.CurrentRoomId == rightTargetId,
            "Simultaneous same-line breaches focus the last destination immediately");

        // Resolve all activated threats; breaches do not make the campaign unwinnable.
        bool fourthLinePatternChecked = false;
        for (int step = 0; step < 80 && !manager.GameEnded; step++)
        {
            if (manager.stage == 4 && !fourthLinePatternChecked)
            {
                DefenseFront.Room[] activeOuter = front.Model.Rooms
                    .Where(r => r.Line == 4 && r.IsActive).ToArray();
                Check(activeOuter.Select(r => r.Index).SequenceEqual(outerPattern),
                    "The location finale activates all seven fourth-line rooms");
                Check(activeOuter.All(r => r.CanSelect && front.GetGenerator(r.Id).IsInitialized
                    && front.GetGenerator(r.Id).HasEnemies),
                    "Active fourth-line rooms spawn their enemies");
                Check(front.Model.Rooms.Where(r => r.Line == 4 && !r.IsActive)
                    .All(r => !front.GetGenerator(r.Id).IsInitialized),
                    "Inactive fourth-line rooms never generate enemies");
                Check(front.Model.GetRoom(front.CurrentRoomId).Line == 4
                    && front.Model.GetRoom(front.CurrentRoomId).IsActive,
                    "Camera focuses an active fourth-line room");

                while (manager.IsChoosingUpgrade)
                {
                    manager.SelectProjectileUpgrade(0);
                    yield return null;
                }
                int sourceId = DefenseFront.IdFor(4, 1);
                int targetId = DefenseFront.IdFor(3, 1);
                LevelGenerator breachedOuterRoom = front.GetGenerator(sourceId);
                LevelGenerator survivingOuterRoom = front.GetGenerator(DefenseFront.IdFor(4, 7));
                int sourceBefore = breachedOuterRoom.EnemyCollection.Count;
                int targetBefore = front.GetGenerator(targetId).EnemyCollection.Count;
                breachedOuterRoom.EnemyCollection[0].transform.position = new Vector3(
                    breachedOuterRoom.transform.position.x, breachedOuterRoom.DefenseBoundaryY,
                    breachedOuterRoom.transform.position.z);
                front.BreachRoom(breachedOuterRoom);
                Check(!breachedOuterRoom.IsLost
                    && front.Model.GetRoom(sourceId).CanSelect
                    && front.Model.GetRoom(DefenseFront.IdFor(4, 2)).CanSelect
                    && breachedOuterRoom.EnemyCollection.Count == sourceBefore - 1
                    && survivingOuterRoom.HasEnemies && front.Model.GetRoom(survivingOuterRoom.RoomId).CanSelect,
                    "A breach of room 4-1 keeps 4-1, 4-2 and 4-7 playable");
                front.DeliverPressure();
                front.FocusPendingBreachRoom();
                while (front.IsCameraTransitioning) yield return null;
                Check(front.GetGenerator(targetId).EnemyCollection.Count == targetBefore + 1
                    && front.PendingTransferEnemies == 0
                    && front.Model.GetRoom(targetId).State == DefenseFront.RoomState.Contested
                    && front.Model.GetRoom(targetId).CanSelect,
                    "The fourth-line crosser alone arrives in room 3-1 immediately");
                Check(front.CurrentRoomId == targetId && survivingOuterRoom.HasEnemies,
                    "Arrival focuses room 3-1 even while room 4-7 is still in combat");
                fourthLinePatternChecked = true;
            }
            foreach (LevelGenerator room in manager.rooms.ToArray()) Kill(room);
            if (manager.IsChoosingUpgrade) manager.SelectProjectileUpgrade(0);
            manager.TryCompletePlayerTurn();
            yield return null;
            yield return null;
        }
        Check(fourthLinePatternChecked, "Fourth-line level layout was reached and validated");
        Check(manager.GameEnded && manager.stage == 4, "Victory remains reachable after outer breaches");
        Check(LevelProgression.HighestUnlockedLevel == 6, "Victory persists before clicking Next");
        int goldBeforeExit = MetaProgression.Gold;
        Check(goldBeforeExit > 1 && MetaProgression.TotalVictories == 1, "Gold income and one victory reward saved");
        SceneManager.LoadScene(0);
        yield return null;
        yield return null;
        Check(MetaProgression.Gold == goldBeforeExit && LevelProgression.HighestUnlockedLevel == 6,
            "Returning to menu retains gold and unlocked level");
        LevelProgression.PrepareLevel(1);
        SceneManager.LoadScene(1);
        yield return null;
        yield return null;
        manager = GameManager.Instance;
        front = manager.Front;
        Kill(front.GetGenerator(0));
        for (int i = 0; i < 6; i++)
        {
            if (manager.IsChoosingUpgrade) manager.SelectProjectileUpgrade(0);
            yield return null;
        }
        yield return AdvanceUntilLine(manager, 2);
        Check(manager.stage == 2, "Accessibility regression starts on second line");
        while (manager.IsResolvingEnemyTurn || manager.stepPlayer) yield return null;
        Kill(front.GetGenerator(0));
        Kill(front.GetGenerator(1));
        // Clearing may end the player turn automatically; let it finish.
        for (int i = 0; i < 600 && (manager.IsResolvingEnemyTurn || manager.IsChoosingUpgrade
            || manager.stepGame || front.IsCameraTransitioning); i++)
        {
            if (manager.IsChoosingUpgrade) manager.SelectProjectileUpgrade(0);
            yield return null;
        }
        front.RefreshAll();
        bool sideRoomSecured = front.Model.GetRoom(1).State == DefenseFront.RoomState.Secured;
        yield return ClickRoom(front, 1, true);
        LevelGenerator breachSource = front.GetGenerator(2);
        Check(breachSource.EnemyCollection.Count > 0, "Room 2-2 has an enemy to breach with");
        breachSource.EnemyCollection[0].transform.position = new Vector3(
            breachSource.transform.position.x, breachSource.DefenseBoundaryY,
            breachSource.transform.position.z);
        front.BreachRoom(breachSource);
        front.DeliverPressure();
        front.FocusPendingBreachRoom();
        Check(front.CurrentRoomId == 0, "A breach focuses the room that receives the enemy");
        while (front.IsCameraTransitioning) yield return null;
        yield return ClickRoom(front, 1, true);
        yield return ClickRoom(front, 3, true);
        yield return ClickRoom(front, 2, true);
        Check(front.Model.Rooms.Where(r => r.Line == 2).All(r => r.CanSelect && r.State != DefenseFront.RoomState.Lost),
            "Every second-line room stays selectable after a breach");
        Check(!sideRoomSecured || front.Model.GetRoom(1).State == DefenseFront.RoomState.Secured,
            "A cleared side room keeps its secured state and income after a sibling breach");
        Check(front.Model.GetRoom(0).State == DefenseFront.RoomState.Contested,
            "Only the receiving room is reoccupied");
        int goldBeforeDefeat = MetaProgression.Gold;
        LevelGenerator mainRoom = front.GetGenerator(0);
        Transform barricadeArtwork = front.Barricade != null
            ? front.Barricade.transform.Find("Main Room Barricade Visual") : null;
        Check(front.Barricade != null && Mathf.Approximately(front.BarricadeMaxHealth, 100f)
            && Mathf.Approximately(front.BarricadeHealth, 100f)
            && barricadeArtwork != null
            && barricadeArtwork.GetComponent<SpriteRenderer>()?.sprite != null
            && Camera.main != null
            && Camera.main.WorldToViewportPoint(barricadeArtwork.position).z
                > Camera.main.nearClipPlane,
            "Main room starts with a visible authored barricade sprite and 100 HP");
        Check(Resources.Load<EnemyHealthBarView>("UI/EnemyHealthBars/DefenseShieldHealthBar") != null,
            "Defense structures use an authored shield health bar prefab");
        front.Barricade.AddBeaverHealth(15f);
        Check(Mathf.Approximately(front.BarricadeHealth, 115f)
            && Mathf.Approximately(front.BarricadeMaxHealth, 100f),
            "Beaver fortifies a full barricade above its normal maximum");
        front.Barricade.TakeDamage(25f);
        front.Barricade.AddBeaverHealth(10f);
        Check(Mathf.Approximately(front.BarricadeHealth, 100f),
            "Beaver restores missing barricade health before adding overhealth");
        int experienceBeforeBarricade = manager.CurrentExperience;
        Brick firstAttacker = UnityEngine.Object.Instantiate(mainRoom.brickPrefab,
            new Vector3(mainRoom.transform.position.x, mainRoom.DefenseBoundaryY,
                mainRoom.transform.position.z), Quaternion.identity, mainRoom.transform)
            .GetComponent<Brick>();
        firstAttacker.levelGenerator = mainRoom;
        firstAttacker.hp = 4000f;
        firstAttacker.dmg = 7f;
        mainRoom.EnemyCollection.Add(firstAttacker.gameObject);
        mainRoom.RequestBreach(firstAttacker);
        mainRoom.ResolveRequestedBreach();
        yield return null;
        Check(!manager.GameEnded
            && Mathf.Approximately(front.BarricadeHealth, 100f - DefenseLineRuntime.DefaultOrdinaryBarricadeDamage)
            && manager.CurrentExperience == experienceBeforeBarricade
            && firstAttacker != null && mainRoom.EnemyCollection.Contains(firstAttacker.gameObject)
            && firstAttacker.transform.position.y >= mainRoom.transform.position.y - 2.001f,
            "A breaching enemy deals fixed damage regardless of its 4000 HP and returns alive within the top three rows");
        front.Barricade.TakeDamage(front.BarricadeHealth - DefenseLineRuntime.DefaultOrdinaryBarricadeDamage);
        Brick finalAttacker = UnityEngine.Object.Instantiate(mainRoom.brickPrefab,
            new Vector3(mainRoom.transform.position.x, mainRoom.DefenseBoundaryY,
                mainRoom.transform.position.z), Quaternion.identity, mainRoom.transform)
            .GetComponent<Brick>();
        finalAttacker.levelGenerator = mainRoom;
        finalAttacker.hp = 1f;
        finalAttacker.dmg = 9f;
        mainRoom.EnemyCollection.Add(finalAttacker.gameObject);
        mainRoom.RequestBreach(finalAttacker);
        mainRoom.ResolveRequestedBreach();
        yield return null;
        Check(manager.GameEnded && Mathf.Approximately(front.BarricadeHealth, 0f),
            "The run ends only when accumulated breach damage destroys the barricade");
        Check(MetaProgression.Gold == goldBeforeDefeat, "Defeat retains previously earned gold");
    }

    private static IEnumerator ClickRoom(DefenseLineRuntime front, int id, bool available)
    {
        front.Interface.ToggleMap();
        yield return null;
        string roomName = "Room " + front.Model.GetRoom(id).Label;
        Button button = front.Interface.GetComponentsInChildren<Button>(true).Single(b => b.name == roomName);
        Check(button.gameObject.activeInHierarchy, roomName + " remains visible on map");
        Check(button.interactable == available, roomName + " button availability matches its own state");
        int before = front.CurrentRoomId;
        button.onClick.Invoke();
        Check(front.CurrentRoomId == (available ? id : before), roomName + " click selects only accessible room");
        front.Interface.CloseMap();
        yield return null;
    }

    // Lines open on a fixed turn schedule. Clear every room and end turns until
    // the requested line has opened, choosing any upgrades along the way.
    private static IEnumerator AdvanceUntilLine(GameManager manager, int line)
    {
        for (int i = 0; i < 2000 && manager.stage < line && !manager.GameEnded; i++)
        {
            if (!manager.IsResolvingEnemyTurn && !manager.stepPlayer)
            {
                foreach (LevelGenerator room in manager.rooms.ToArray()) Kill(room);
                if (manager.IsChoosingUpgrade) manager.SelectProjectileUpgrade(0);
                else manager.TryCompletePlayerTurn();
            }
            yield return null;
        }
        while (manager.IsChoosingUpgrade)
        {
            manager.SelectProjectileUpgrade(0);
            yield return null;
        }
        while (manager.IsResolvingEnemyTurn
            || (manager.Front != null && manager.Front.IsCameraTransitioning)) yield return null;
    }

    private static IEnumerator Advance(GameManager manager)
    {
        while (manager.stepPlayer) yield return null;
        int turn = manager.TurnNumber;
        AnimalProjectileQueue.Instance.ClearAllAmmo();
        manager.TryCompletePlayerTurn();
        for (int i = 0; i < 10 && manager.TurnNumber == turn; i++)
        {
            if (manager.IsChoosingUpgrade) manager.SelectProjectileUpgrade(0);
            yield return null;
        }
        Check(manager.TurnNumber == turn + 1, "Explicit turn advances once");
        float timeout = Time.realtimeSinceStartup + 3f;
        while (manager.IsResolvingEnemyTurn && Time.realtimeSinceStartup < timeout) yield return null;
        Check(!manager.IsResolvingEnemyTurn, "Enemy turn finishes its movement and camera transition");
    }

    private static IEnumerator CaptureMap(DefenseFrontUI ui)
    {
        // Batch mode does not paint a Game window, so render the actual UI through a camera.
        Canvas canvas = ui.GetComponent<Canvas>();
        GameObject cameraObject = new GameObject("Map validation camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        RenderTexture target = new RenderTexture(1080, 1920, 24);
        Texture2D image = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/DefenseMap.png", image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.Destroy(image);
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(cameraObject);
        }
    }

    private static IEnumerator CaptureCombatText(EnemyCombatTextUI ui)
    {
        Canvas canvas = ui.GetComponent<Canvas>();
        Camera camera = Camera.main;
        RenderTexture target = new RenderTexture(1080, 1920, 24);
        Texture2D image = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderMode previousMode = canvas.renderMode;
        Camera previousWorldCamera = canvas.worldCamera;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/EnemyCombatText.png", image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousWorldCamera;
            RenderTexture.active = previous;
            camera.targetTexture = previousTarget;
            UnityEngine.Object.Destroy(image);
            UnityEngine.Object.Destroy(target);
        }
    }

    private static void WriteCombatTextDiagnostics(EnemyCombatTextUI ui, Brick sample)
    {
        List<string> lines = new List<string>();
        Canvas canvas = ui.GetComponent<Canvas>();
        RectTransform root = ui.transform as RectTransform;
        Camera camera = Camera.main;
        lines.Add("Screen=" + Screen.width + "x" + Screen.height + " scaleFactor=" + canvas.scaleFactor);
        lines.Add("CanvasRect=" + root.rect + " localScale=" + root.localScale + " lossy=" + root.lossyScale);
        lines.Add("Camera pixel=" + camera.pixelRect + " size=" + camera.orthographicSize + " pos=" + camera.transform.position);
        lines.Add("Enemy world=" + sample.transform.position + " screen=" + camera.WorldToScreenPoint(sample.transform.position)
            + " viewport=" + camera.WorldToViewportPoint(sample.transform.position));
        foreach (TextMeshProUGUI label in ui.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (!label.gameObject.activeInHierarchy) continue;
            lines.Add(label.name + " text=" + label.text + " pos=" + label.rectTransform.anchoredPosition
                + " rect=" + label.rectTransform.rect + " font=" + label.fontSize + " alpha=" + label.alpha
                + " color=" + label.color + " canvasAlpha=" + label.canvasRenderer.GetAlpha());
        }
        File.WriteAllLines("Logs/EnemyCombatTextDiagnostics.txt", lines);
    }

    private static void Kill(LevelGenerator room)
    {
        foreach (GameObject enemy in room.EnemyCollection.ToArray())
            if (enemy != null) enemy.GetComponent<Brick>().TakeDamage(100000f);
    }

    private static bool IsInsidePressureZone(GameObject enemy, LevelGenerator room)
    {
        if (enemy == null) return false;
        Vector3 cell = room.transform.InverseTransformPoint(enemy.transform.position);
        float column = Mathf.Round(cell.x);
        float row = Mathf.Round(-cell.y);
        return Mathf.Abs(cell.x - column) < 0.01f && Mathf.Abs(-cell.y - row) < 0.01f
            && column >= 0f && column < room.Columns && row >= 0f && row < room.PressureRows;
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(BatchKey, false);
        SessionState.SetBool(ThreatOnlyKey, false);
        string[] saved = SessionState.GetString(PrefsKey, "").Split('|');
        for (int i = 0; i < keys.Length && i < saved.Length; i++)
            if (saved[i] == "-") PlayerPrefs.DeleteKey(keys[i]);
            else PlayerPrefs.SetInt(keys[i], int.Parse(saved[i]));
        PlayerPrefs.Save();
        string summary = error == null ? "PASS: " + checks + " Unity integration checks." : "FAIL: " + error;
        report.Add(summary);
        File.WriteAllLines("Logs/DefenseFrontValidation.txt", report);
        if (error == null) Debug.Log(summary); else Debug.LogError(summary);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
