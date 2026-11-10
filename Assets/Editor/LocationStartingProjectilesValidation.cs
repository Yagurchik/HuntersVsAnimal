using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class LocationStartingProjectilesValidation
{
    private const string Running = "LocationStartingProjectilesValidation.Running";
    private static readonly List<string> Report = new List<string>();
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static int lastFrame = -1;
    private static double deadline;
    private static readonly int[] Starters = { 1, 2, 5, 6, 7, 4, 3 };

    static LocationStartingProjectilesValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Steps.Clear(); Steps.Push(Checks());
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new InvalidOperationException("Use the isolated validation project.");
        Directory.CreateDirectory("Logs");
        Report.Clear();
        GameRunMode.SetDeveloperMode(false);
        GameRunMode.PrepareCampaign();
        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return Frames(4);
        var dialog = Object.FindObjectOfType<ProjectileLoadoutUI>(true);
        Require(dialog != null, "Native menu has the editable loadout dialog");
        var fireflyEntry = Object.FindObjectsOfType<BestiaryEntryView>(true)
            .Single(entry => entry.Category == BestiaryEntryView.Section.Projectiles && entry.Title == "Светлячок");
        Require(!fireflyEntry.Body.Contains("Количество —") && fireflyEntry.Body.Contains("двух основных веток")
            && fireflyEntry.Body.Contains("5 снарядов"),
            "Authored firefly bestiary describes its fixed volley and two talent branches");
        var rabbitEntry = Object.FindObjectsOfType<BestiaryEntryView>(true)
            .Single(entry => entry.Category == BestiaryEntryView.Section.Projectiles && entry.Title == "Кролик");
        Require(rabbitEntry.Body.Contains("Сила стаи") && rabbitEntry.Body.Contains("Быстрое размножение")
            && rabbitEntry.Body.Contains("Ранний приплод") && rabbitEntry.Body.Contains("4/3/2")
            && rabbitEntry.Body.Contains("всех трёх основных веток") && !rabbitEntry.Body.Contains("Количество —"),
            "Authored rabbit bestiary describes three talents and their ultimate requirement");
        var data = new SerializedObject(dialog);
        var choices = data.FindProperty("choices");
        Require(choices.arraySize == 8, "All eight projectile choices retain their authored controls");
        var buttons = Enumerable.Range(0, 8).Select(i => (Button)choices.GetArrayElementAtIndex(i)
            .FindPropertyRelative("button").objectReferenceValue).ToArray();
        var statuses = Enumerable.Range(0, 8).Select(i => (Text)choices.GetArrayElementAtIndex(i)
            .FindPropertyRelative("status").objectReferenceValue).ToArray();
        var start = (Button)data.FindProperty("startButton").objectReferenceValue;
        var close = (Button)data.FindProperty("closeButton").objectReferenceValue;
        List<int> selected = null;
        for (int location = 1; location <= 7; location++)
        {
            Require(AnimalProjectileQueue.StartingAnimalForLocation(location) == Starters[location - 1], "Starter mapping for location " + location);
            Require(dialog.Show(values => selected = values.ToList(), location), "Dialog opens for location " + location);
            // Retained optional selections can be cleared without removing the two starters.
            for (int i = 1; i < 8; i++) if (statuses[i].text == "В УЛУЧШЕНИЯХ") buttons[i].onClick.Invoke();
            buttons[0].onClick.Invoke(); buttons[Starters[location - 1]].onClick.Invoke();
            int starterAmmo = location == 2 ? 2 : 3;
            Require(statuses[0].text == "СРАЗУ: 5" && statuses[Starters[location - 1]].text == "СРАЗУ: " + starterAmmo
                && !start.interactable, "Both starters are reserved and show ammo in location " + location);
            foreach (int index in Loadout(location).Where(i => i != 0 && i != Starters[location - 1])) buttons[index].onClick.Invoke();
            Require(start.interactable, "Three optional types complete the five-type set in location " + location);
            start.onClick.Invoke();
            Require(selected != null && selected.Count == 5 && selected.Distinct().Count() == 5
                && selected.Contains(0) && selected.Contains(Starters[location - 1]), "Confirmed selection includes both starters in location " + location);
            Require(AnimalProjectileQueue.PrepareRunLoadout(selected, location), "Prepared run accepts matching location " + location);
        }
        Require(!AnimalProjectileQueue.PrepareRunLoadout(new[] { 0, 2, 3, 4, 5 }, 1), "Run cannot omit its required location starter");
        selected = null; dialog.Show(values => selected = values.ToList(), 3); close.onClick.Invoke();
        Require(!dialog.IsOpen && selected == null, "Closing the dialog does not start a run");

        // The real gameplay scene exercises menu handoff, Awake ordering and replay.
        var firstLoadout = new[] { 0, 1, 4, 6, 7 };
        AnimalProjectileQueue.PrepareRunLoadout(firstLoadout, 1);
        GameRunMode.PrepareCampaign(); LevelProgression.PrepareLevel(1);
        EditorSceneManager.LoadSceneInPlayMode("Assets/Asset.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return Frames(5);
        CheckQueue(AnimalProjectileQueue.Instance, 1);
        var tutorial = Object.FindObjectOfType<FirstLevelTutorial>(true);
        Require(tutorial != null && tutorial.IsRunning && !AnimalProjectileQueue.Instance.HasForcedUpgradeOption,
            "First-level tutorial keeps the hedgehog available from the start");

        for (int location = 2; location <= 6; location++)
        {
            Time.timeScale = 1;
            GameRunMode.PrepareCampaign();
            LevelProgression.PrepareLevel(LevelProgression.GlobalLevelFor(location, 1));
            EditorSceneManager.LoadSceneInPlayMode("Assets/Asset.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Frames(4);
            Require(LevelProgression.Instance.CurrentLocationIndex == location, "Gameplay requested location " + location);
            CheckQueue(AnimalProjectileQueue.Instance, location);
            Require(Enumerable.Range(0, 8).Count(i => AnimalProjectileQueue.Instance.IsAnimalAllowedForRun(i)) == 5,
                "Location transition preserves the five-type selection");
            var queue = AnimalProjectileQueue.Instance;
            queue.ClearAllAmmo(); queue.ReloadAll();
            Require(queue.TotalRemainingAmmo == (location == 2 ? 7 : 8),
                "Reload restores fully upgraded starter quantities in location " + location);
        }

        var current = AnimalProjectileQueue.Instance;
        Require(current.ConfigureRunLoadout(Loadout(7), 7), "Future seventh location accepts honey badger starter");
        CheckQueue(current, 7);
        Require(LevelProgression.LocationCount == 6, "Seventh location remains reserved for future campaign content");
        Require(current.ConfigureRunLoadout(Loadout(2), 2), "Rabbit starter can be configured");
        Require(current.Animals[2].AmmoPerTurn == 2 && current.GetRemainingAmmo(2) == 2
            && current.GetRabbitBreedingUpgradeLevel(2) == AnimalProjectileQueue.MaximumBranchLevel
            && current.GetRabbitMaximumReplicationHits(2) == 7
            && current.GetRabbitInitialReplicationHits(2) == 5,
            "Starter rabbit has two projectiles and the full breeding upgrade");
        Require(!current.GetAvailableUpgradeOptions().Any(option => option.AnimalIndex == 2
            && option.Kind == AnimalProjectileQueue.UpgradeKind.RabbitBreeding),
            "Starter rabbit cannot purchase an already completed breeding branch");
        CheckRabbitReplication(current, 7);
        Require(current.ConfigureRunLoadout(Loadout(1), 1)
            && current.GetAvailableUpgradeOptions().Any(option => option.AnimalIndex == 2
                && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo && option.Title.StartsWith("Открыть:"))
            && current.Upgrade(2, AnimalProjectileQueue.UpgradeKind.Ammo) && current.GetRemainingAmmo(2) == 2,
            "Optional rabbit still unlocks through its real upgrade offer and grants two projectiles");
        Require(current.GetRabbitBreedingUpgradeLevel(2) == 0 && current.GetRabbitMaximumReplicationHits(2) == 10,
            "Rabbit's free breeding levels do not carry into a different location");
        yield return CheckRabbitUpgrades(current);
        Require(!current.Upgrade(2, AnimalProjectileQueue.UpgradeKind.Ammo)
            && !current.GetAvailableUpgradeOptions().Any(option => option.AnimalIndex == 2
                && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo) && current.Animals[2].AmmoPerTurn == 2,
            "Optional rabbit has no quantity offers after unlocking");
        Require(!current.Upgrade(0, AnimalProjectileQueue.UpgradeKind.Ammo) && current.Animals[0].AmmoPerTurn == 5,
            "Firefly has a fixed five-projectile volley without quantity upgrades");
        Require(!ProjectileUpgradeSummary.ForAnimal(current, 0).Any(item => item.Title == "Количество"),
            "Firefly quantity branch is absent from its pause summary");
        current.ForceNextUpgradeOption(0, AnimalProjectileQueue.UpgradeKind.Ammo);
        Require(!current.GetAvailableUpgradeOptions().Any(option => option.AnimalIndex == 0
            && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo),
            "Forcing a removed quantity kind cannot create a firefly quantity card");
        current.ClearForcedUpgradeOption();
        Require(!current.Upgrade(0, AnimalProjectileQueue.UpgradeKind.FireflyUltimate),
            "Firefly ultimate waits for repeat fire and burn");
        for (int rank = 0; rank < AnimalProjectileQueue.MaximumBranchLevel; rank++)
            Require(current.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire)
                && current.Upgrade(0, AnimalProjectileQueue.UpgradeKind.Burn),
                "Firefly's two remaining branches accept rank " + (rank + 1));
        Require(current.Upgrade(0, AnimalProjectileQueue.UpgradeKind.FireflyUltimate)
            && current.HasFireflyUltimate(0)
            && ProjectileUpgradeSummary.ForAnimal(current, 0).Count == 3,
            "Firefly ultimate opens after two branches with no quantity prerequisite");
        Require(current.ConfigureRunLoadout(Loadout(2), 2) && current.GetRabbitFirstBreedingUpgradeLevel(2) == 0
            && current.GetRabbitInitialReplicationHits(2) == 5 && current.GetRabbitMaximumReplicationHits(2) == 7
            && !current.HasRabbitUltimate(2), "New run resets first reproduction while preserving the starter's assigned maximum talent");
        for (int rank = 0; rank < 3; rank++)
            Require(current.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitFirstBreeding),
                "Starter rabbit can improve its first-reproduction talent at rank " + (rank + 1));
        Require(!current.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitUltimate),
            "Completed maximum and first-reproduction talents still require pack strength for the ultimate");
        Require(current.ConfigureRunLoadout(Loadout(1), 1) && current.Upgrade(2, AnimalProjectileQueue.UpgradeKind.Ammo),
            "Fresh optional rabbit starts with all three talents untrained");
        for (int rank = 0; rank < 3; rank++)
            Require(current.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitFirstBreeding)
                && current.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitPackStrength),
                "Optional rabbit improves first reproduction and pack strength independently at rank " + (rank + 1));
        Require(current.GetRabbitInitialReplicationHits(2) == 2 && current.GetRabbitMaximumReplicationHits(2) == 10
            && !current.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitUltimate),
            "Completed pack and first-reproduction talents still require maximum breeding for the ultimate");
        CheckRabbitReplication(current, 10, 2);
        Require(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Beaver.prefab").GetComponent<Animal>().dmg == 5
            && current.ConfigureRunLoadout(Loadout(3), 3)
            && current.Upgrade(5, AnimalProjectileQueue.UpgradeKind.BeaverDamage)
            && current.GetBeaverBonusDamage(5) == 5, "Beaver keeps the halved base and upgrade damage");
    }

    private static int[] Loadout(int location)
    {
        var selected = new List<int> { 0, Starters[location - 1] };
        for (int i = 1; selected.Count < 5; i++) if (!selected.Contains(i)) selected.Add(i);
        return selected.ToArray();
    }

    private static IEnumerator CheckRabbitUpgrades(AnimalProjectileQueue queue)
    {
        var manager = GameManager.Instance;
        var ui = Object.FindObjectOfType<ExperienceAndUpgradeUI>(true);
        Require(manager != null && ui != null, "Rabbit upgrades use the authored gameplay HUD");
        manager.enabled = false;
        foreach (Player player in Object.FindObjectsOfType<Player>()) player.enabled = false;
        int[] savedCards = ui.ChoicesRoot.GetComponentsInChildren<Button>(true)
            .Select(button => button.GetInstanceID()).ToArray();
        Require(savedCards.Length == 3, "Rabbit upgrade HUD contains the three authored card objects");
        var kinds = queue.GetAvailableUpgradeOptions().Where(option => option.AnimalIndex == 2)
            .Select(option => option.Kind).ToArray();
        Require(kinds.Length == 3 && !kinds.Contains(AnimalProjectileQueue.UpgradeKind.Ammo)
            && kinds.Contains(AnimalProjectileQueue.UpgradeKind.RabbitPackStrength)
            && kinds.Contains(AnimalProjectileQueue.UpgradeKind.RabbitBreeding)
            && kinds.Contains(AnimalProjectileQueue.UpgradeKind.RabbitFirstBreeding),
            "Optional rabbit offers pack strength, maximum threshold and first reproduction without a quantity branch");
        Require(!queue.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitUltimate),
            "Rabbit ultimate waits for all three talent branches");
        CheckRabbitReplication(queue, 10);
        for (int rank = 1; rank <= 3; rank++)
        {
            int maximum = 10 - rank;
            typeof(GameManager).GetField("pendingProjectileUpgrades", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, 1);
            Require(queue.ForceNextUpgradeOption(2, AnimalProjectileQueue.UpgradeKind.RabbitBreeding)
                && (bool)typeof(GameManager).GetMethod("OpenUpgradeChoice", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null), "Game opens the rabbit breeding card at rank " + rank);
            yield return Frames(3);
            var card = ui.ChoicesRoot.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.activeInHierarchy);
            Require(card.name.Contains("Кролик: быстрое размножение")
                && card.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("Максимум ударов до размножения")
                    && text.text.Contains((maximum + 1) + "  →  " + maximum)),
                "Visible breeding card reduces the maximum from " + (maximum + 1) + " to " + maximum);
            card.onClick.Invoke();
            Require(queue.GetRabbitMaximumReplicationHits(2) == maximum && queue.GetRabbitInitialReplicationHits(2) == 5
                && manager.PendingProjectileUpgrades == 0 && !manager.IsChoosingUpgrade && !ui.UpgradeModal.activeSelf,
                "Choosing breeding rank " + rank + " reduces only the maximum and closes the choice");
            Require(ProjectileUpgradeSummary.ForAnimal(queue, 2).Any(item => item.Title == "Быстрое размножение"
                && item.Description.Contains("Максимум ударов до размножения: " + maximum + ".")
                && item.Description.Contains("Начальный порог: 5 ударов")),
                "Pause summary shows the actual reproduction maximum of " + maximum);
            CheckRabbitReplication(queue, maximum);
            Require(!queue.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitUltimate),
                "Rabbit ultimate waits until pack strength also completes at rank " + rank);
            Require(queue.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitPackStrength),
                "Rabbit pack skill remains selectable at rank " + rank);
        }
        Require(!queue.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitBreeding)
            && !queue.GetAvailableUpgradeOptions().Any(option => option.AnimalIndex == 2
                && option.Kind == AnimalProjectileQueue.UpgradeKind.RabbitBreeding),
            "Breeding branch stops at seven hits after three improvements");
        Require(!queue.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitUltimate)
            && !queue.GetAvailableUpgradeOptions().Any(option => option.AnimalIndex == 2
                && option.Kind == AnimalProjectileQueue.UpgradeKind.RabbitUltimate),
            "Completed pack and maximum branches cannot unlock the ultimate without first reproduction");
        for (int rank = 1; rank <= 3; rank++)
        {
            int initial = 5 - rank;
            typeof(GameManager).GetField("pendingProjectileUpgrades", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, 1);
            Require(queue.ForceNextUpgradeOption(2, AnimalProjectileQueue.UpgradeKind.RabbitFirstBreeding)
                && (bool)typeof(GameManager).GetMethod("OpenUpgradeChoice", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null), "Game opens the first-reproduction card at rank " + rank);
            yield return Frames(3);
            var card = ui.ChoicesRoot.GetComponentsInChildren<Button>(true)
                .Single(button => button.gameObject.activeInHierarchy);
            Require(card.name.Contains("Кролик: ранний приплод")
                && card.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("Ударов до первого размножения")
                    && text.text.Contains((initial + 1) + "  →  " + initial)),
                "Visible first-reproduction card reduces the initial threshold to " + initial);
            card.onClick.Invoke();
            Require(queue.GetRabbitInitialReplicationHits(2) == initial && queue.GetRabbitMaximumReplicationHits(2) == 7
                && queue.GetRabbitFirstBreedingUpgradeLevel(2) == rank && manager.PendingProjectileUpgrades == 0
                && !manager.IsChoosingUpgrade && !ui.UpgradeModal.activeSelf,
                "First-reproduction rank " + rank + " reduces its own threshold independently and closes its card");
            Require(ProjectileUpgradeSummary.ForAnimal(queue, 2).Any(item => item.Title == "Ранний приплод"
                && item.Rank == rank && item.Description.Contains("Ударов до первого размножения: " + initial + ".")),
                "Pause summary shows first-reproduction rank " + rank);
            CheckRabbitReplication(queue, 7, initial);
            if (rank < 3)
                Require(!queue.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitUltimate),
                    "Ultimate remains locked until the third talent reaches rank three");
        }
        Require(!queue.Upgrade(2, AnimalProjectileQueue.UpgradeKind.RabbitFirstBreeding)
            && !queue.GetAvailableUpgradeOptions().Any(option => option.AnimalIndex == 2
                && option.Kind == AnimalProjectileQueue.UpgradeKind.RabbitFirstBreeding),
            "First-reproduction branch caps at two hits after three improvements");
        Require(!queue.Upgrade(2, AnimalProjectileQueue.UpgradeKind.Ammo)
            && !queue.GetAvailableUpgradeOptions().Any(option => option.AnimalIndex == 2
                && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo)
            && queue.Animals[2].AmmoPerTurn == 2 && queue.Animals[2].AmmoUpgradeLevel == 1,
            "Optional rabbit keeps two projectiles and cannot purchase a quantity offer");
        queue.ForceNextUpgradeOption(2, AnimalProjectileQueue.UpgradeKind.Ammo);
        Require(!queue.GetAvailableUpgradeOptions().Any(option => option.AnimalIndex == 2
            && option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo),
            "Forcing a removed quantity kind does not create a rabbit quantity offer");
        queue.ClearForcedUpgradeOption();
        typeof(GameManager).GetField("pendingProjectileUpgrades", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, 1);
        Require(queue.ForceNextUpgradeOption(2, AnimalProjectileQueue.UpgradeKind.RabbitUltimate)
            && (bool)typeof(GameManager).GetMethod("OpenUpgradeChoice", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, null), "Rabbit ultimate card opens only after all three talents are completed");
        yield return Frames(3);
        var ultimateCard = ui.ChoicesRoot.GetComponentsInChildren<Button>(true)
            .Single(button => button.gameObject.activeInHierarchy);
        Require(ultimateCard.name.Contains("Кролик: пуховая буря"), "Authored HUD offers the rabbit ultimate");
        ultimateCard.onClick.Invoke();
        queue.ClearAllAmmo(); queue.ReloadAll();
        Require(queue.HasRabbitUltimate(2) && queue.GetRemainingAmmo(2) == 2
            && manager.PendingProjectileUpgrades == 0 && !manager.IsChoosingUpgrade && !ui.UpgradeModal.activeSelf,
            "Choosing the ultimate closes its card and retains the initial pair of rabbits");
        Require(savedCards.SequenceEqual(ui.ChoicesRoot.GetComponentsInChildren<Button>(true)
            .Select(button => button.GetInstanceID())), "Rabbit offers preserve the authored card objects");
        var summary = ProjectileUpgradeSummary.ForAnimal(queue, 2);
        Require(summary.Count == 5 && summary.Count(item => item.Ultimate) == 1
            && summary.Any(item => item.Title == "Снаряд открыт")
            && !summary.Any(item => item.Title == "Количество"),
            "Optional rabbit pause summary includes its unlock, three talent branches and ultimate");
    }

    private static void CheckRabbitReplication(AnimalProjectileQueue queue, int maximum, int initial = 5)
    {
        Player player = Object.FindObjectOfType<Player>();
        GameObject prefab = queue.Animals[2].Prefab;
        typeof(Player).GetMethod("LaunchProjectileSingle", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(player, new object[] { Vector2.up, prefab, 2, 1f });
        RabbitReplication rabbit = Object.FindObjectsOfType<RabbitReplication>().Single();
        Brick target = Object.Instantiate(GameManager.Instance.rooms.First(room => room.brickPrefab != null).brickPrefab)
            .GetComponent<Brick>();
        target.hp = 1000000f;
        try
        {
            Require(rabbit.RequiredHitsForNextReplica == initial && rabbit.MaximumReplicationHits == maximum,
                "Real player launch starts at " + initial + " hits with reproduction maximum " + maximum);
            for (int required = initial; required <= maximum; required++)
            {
                var previous = new HashSet<RabbitReplication>(Object.FindObjectsOfType<RabbitReplication>());
                for (int hit = 1; hit < required; hit++)
                    Require(target.ApplyProjectileHit(rabbit.GetComponent<Animal>(), target.transform.position, Vector2.up),
                        "Rabbit direct hit " + hit + " resolves for threshold " + required + " and maximum " + maximum);
                Require(Object.FindObjectsOfType<RabbitReplication>().Length == previous.Count,
                    "Rabbit waits for all " + required + " hits before reproducing with maximum " + maximum);
                Require(target.ApplyProjectileHit(rabbit.GetComponent<Animal>(), target.transform.position, Vector2.up),
                    "Final direct hit resolves for threshold " + required + " and maximum " + maximum);
                RabbitReplication[] replicas = Object.FindObjectsOfType<RabbitReplication>()
                    .Where(candidate => !previous.Contains(candidate)).ToArray();
                int next = Math.Min(required + 1, maximum);
                Require(replicas.Length == 1 && rabbit.RequiredHitsForNextReplica == next
                    && replicas[0].RequiredHitsForNextReplica == next && replicas[0].MaximumReplicationHits == maximum,
                    "Parent and its actual replica inherit threshold " + next + " and cap " + maximum);
                rabbit = replicas[0];
            }
        }
        finally
        {
            foreach (RabbitReplication instance in Object.FindObjectsOfType<RabbitReplication>())
                Object.DestroyImmediate(instance.gameObject);
            Object.DestroyImmediate(target.gameObject);
        }
    }

    private static void CheckQueue(AnimalProjectileQueue queue, int location)
    {
        int starter = Starters[location - 1];
        int starterAmmo = location == 2 ? 2 : 3;
        Require(queue != null && queue.StartingAnimalIndex == starter && queue.HasRunLoadout, "Real queue chooses starter for location " + location);
        Require(queue.IsAvailable(0) && queue.GetRemainingAmmo(0) == 5 && queue.IsAvailable(starter)
            && queue.GetRemainingAmmo(starter) == starterAmmo && queue.TotalRemainingAmmo == 5 + starterAmmo,
            "Initial volley is 5 fireflies plus " + starterAmmo + " location projectiles in location " + location);
        Require(queue.GetAmmoUpgradeLevel(0) == 1
            && queue.GetAmmoUpgradeLevel(starter) == (starter == AnimalProjectileQueue.RabbitIndex ? 1 : AnimalProjectileQueue.MaximumBranchLevel)
            && (starter != AnimalProjectileQueue.RabbitIndex
                || queue.GetRabbitBreedingUpgradeLevel(starter) == AnimalProjectileQueue.MaximumBranchLevel),
            "Location starter begins with its assigned quantity or breeding talent in location " + location);
        Require(!queue.GetAvailableUpgradeOptions().Any(option => option.Kind == AnimalProjectileQueue.UpgradeKind.Ammo
            && (option.AnimalIndex == 0 || option.AnimalIndex == starter)),
            "Completed starter quantity branches are absent from upgrade offers in location " + location);
        Require(Enumerable.Range(0, 8).Count(i => queue.IsAvailable(i)) == 2, "All other types await upgrades in location " + location);
        Require(queue.GetAvailableUpgradeOptions().Where(option => option.AnimalIndex == starter)
            .All(option => !option.Title.StartsWith("Открыть:")), "Starter does not need an unlock upgrade in location " + location);
    }

    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static void Require(bool okay, string message) { if (!okay) throw new Exception(message); Report.Add("PASS " + message); }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Starting projectile validation timed out");
            while (Steps.Count > 0)
            {
                var step = Steps.Peek(); if (!step.MoveNext()) { Steps.Pop(); continue; }
                if (step.Current is IEnumerator child) { Steps.Push(child); continue; } return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Running, false); Time.timeScale = 1;
        Report.Add(error == null ? "LOCATION_STARTERS_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/LocationStartingProjectilesValidation.txt", Report);
        if (error != null) Debug.LogException(error); else Debug.Log("LOCATION_STARTERS_PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
