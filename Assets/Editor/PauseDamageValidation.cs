using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PauseDamageValidation
{
    private const string Running = "ForestAnimalWar.PauseDamageValidation";
    private const string FrogSupport = "ForestAnimalWar.FrogSupportDamageValidation";
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static readonly List<string> Report = new List<string>();
    private static LevelGenerator room;
    private static Camera camera;
    private static int lastFrame = -1;
    private static double deadline;
    static PauseDamageValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Steps.Clear(); Steps.Push(SessionState.GetBool(FrogSupport, false) ? FrogSupportChecks() : Checks());
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use the isolated validation project.");
        Directory.CreateDirectory("Logs"); PauseDamageAuthoring.Bake();
        SessionState.SetBool(FrogSupport, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
    }
    public static void RunFrogSupportBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use the isolated validation project.");
        Directory.CreateDirectory("Logs");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(FrogSupport, true); SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }
    private static IEnumerator FrogSupportChecks()
    {
        var player = new GameObject("Player"); player.tag = "Player"; player.transform.position = Vector3.down * 8;
        camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        room = new GameObject("Frog Support Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        Brick host = Target(Vector2.left * 6, 10000);
        var frogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Frog.prefab")
            .GetComponent<FrogProjectile>().RiderPrefab;
        for (int i = 0; i < 2; i++)
        {
            var frog = Object.Instantiate(frogPrefab, host.transform.position, Quaternion.identity, room.transform);
            frog.Initialize(host, 15, 2, 3, false, 0);
        }
        yield return Frames(5);
        Require(FrogRider.CountOn(host) == 2 && Mathf.Approximately(FrogRider.DamageMultiplier(room), 1.06f),
            "Two actual seated rank-three frogs provide six percent room support");
        Brick target = Target(Vector2.zero, 10000); Set(target, "immuneToPoison", false);
        CombatDamageSource[] sources = { CombatDamageSource.Firefly, CombatDamageSource.Hedgehog,
            CombatDamageSource.Rabbit, CombatDamageSource.HoneyBadger, CombatDamageSource.Boar,
            CombatDamageSource.Beaver, CombatDamageSource.Squirrel };
        int[] indices = { 0, 1, 2, 3, 4, 5, 7 };
        ProjectileDamageStats.BeginLevel();
        for (int i = 0; i < sources.Length; i++)
        {
            target.TakeProjectileDamage(100, 0, sources[i]);
            Require(Math.Abs(ProjectileDamageStats.ForAnimal(indices[i]) - 100) < 0.001,
                "Unboosted hit credited to " + sources[i]);
        }
        Require(Math.Abs(ProjectileDamageStats.ForAnimal(6) - 42) < 0.001
            && Math.Abs(ProjectileDamageStats.TotalDamage - 742) < 0.001,
            "All seven other projectile bonuses credit frogs without increasing the level total twice");

        ProjectileDamageStats.BeginLevel();
        target.TakeAfterTurnDamage(25, CombatDamageSource.Fire, AnimalProjectileQueue.FireflyIndex);
        target.TakeAfterTurnDamage(100, CombatDamageSource.Fire, AnimalProjectileQueue.SquirrelIndex);
        target.TakePoisonDamage(20, false);
        Require(Math.Abs(ProjectileDamageStats.ForAnimal(0) - 25) < 0.001
            && Math.Abs(ProjectileDamageStats.ForAnimal(7) - 100) < 0.001
            && Math.Abs(ProjectileDamageStats.ForAnimal(3) - 20) < 0.001
            && Math.Abs(ProjectileDamageStats.ForAnimal(6) - 8.7) < 0.005,
            "Burn, acorn fire and poison retain their owners while chorus bonuses credit frogs");
        double before = ProjectileDamageStats.TotalDamage, frogBefore = ProjectileDamageStats.ForAnimal(6);
        target.TakeDamage(20, CombatDamageSource.Fire);
        Set(target, "immuneToPoison", true); target.TakePoisonDamage(20, false);
        Require(ProjectileDamageStats.TotalDamage == before && ProjectileDamageStats.ForAnimal(6) == frogBefore,
            "Enemy fire and blocked poison never add support damage");

        ProjectileDamageStats.BeginLevel();
        var otherRoom = new GameObject("Unbuffed Room", typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        Brick other = Target(Vector2.up * 5, 1000); room.EnemyCollection.Remove(other.gameObject);
        other.transform.SetParent(otherRoom.transform); other.levelGenerator = otherRoom;
        otherRoom.EnemyCollection.Add(other.gameObject); other.TakeDamage(100, CombatDamageSource.Boar);
        Require(ProjectileDamageStats.ForAnimal(4) == 100 && ProjectileDamageStats.ForAnimal(6) == 0,
            "Frogs do not receive damage credit from another room");

        ProjectileDamageStats.BeginLevel();
        Brick fragile = Target(Vector2.right * 4, 103); fragile.TakeDamage(100, CombatDamageSource.Boar);
        Require(ProjectileDamageStats.ForAnimal(4) == 100 && ProjectileDamageStats.ForAnimal(6) == 3
            && ProjectileDamageStats.TotalDamage == 103, "Lethal boosted hit credits only the realised three-HP bonus");
        ProjectileDamageStats.BeginLevel();
        fragile = Target(Vector2.right * 4, 7); fragile.TakeDamage(100, CombatDamageSource.Boar);
        Require(ProjectileDamageStats.ForAnimal(4) == 7 && ProjectileDamageStats.ForAnimal(6) == 0
            && ProjectileDamageStats.TotalDamage == 7, "Overkill cannot create nonexistent support damage");
        ProjectileDamageStats.BeginLevel(); target.TakeFrogDamage(15, 0, true);
        Require(Math.Abs(ProjectileDamageStats.ForAnimal(6) - 15.9) < 0.001
            && ProjectileDamageStats.TotalDamage == ProjectileDamageStats.ForAnimal(6),
            "Frogs' own boosted bites are counted exactly once");

        ProjectileDamageStats.BeginLevel(); target.TakeDamage(100, CombatDamageSource.Firefly);
        var panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PauseDamageAuthoring.PrefabPath));
        panel.SetActive(true); yield return Frames(2);
        var rows = (PauseDamageUI.DamageRow[])Get(panel.GetComponent<PauseDamageUI>(), "rows");
        var frogRow = rows.Single(r => r.root.gameObject.activeSelf && r.name.text == "Лягушка");
        Require(frogRow.damage.text == "6" && rows[0].damage.text == "100"
            && Math.Abs(frogRow.fill.rectTransform.anchorMax.x - 6f / 106f) < 0.0001,
            "Pause shows support damage in the frog row with the correct bar share");
        target.TakeDamage(100, CombatDamageSource.Firefly);
        Require(frogRow.damage.text == "12" && rows[0].damage.text == "200"
            && ProjectileDamageStats.TotalDamage == 212, "Pause updates both damage rows together after a supported hit");
        Object.Destroy(panel);
        FrogRider.UpgradeAll(2, 0, false); ProjectileDamageStats.BeginLevel();
        target.TakeDamage(100, CombatDamageSource.Firefly);
        Require(ProjectileDamageStats.ForAnimal(0) == 100 && ProjectileDamageStats.ForAnimal(6) == 0,
            "Rank-zero frogs add no support damage");
        ProjectileDamageStats.BeginLevel(); target.TakeDamage(0.2f, CombatDamageSource.Firefly);
        Require(ProjectileDamageStats.ForAnimal(6) == 0 && ProjectileDamageStats.TotalDamage == ProjectileDamageStats.ForAnimal(0),
            "Floating-point HP rounding without chorus never creates phantom frog damage");
    }
    public static void ValidateSceneBindingBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use the isolated validation project.");
        EditorSceneManager.OpenScene("Assets/PauseSceneBinding.unity");
        var transition = Object.FindObjectOfType<LevelTransition>(true);
        if (transition == null || transition.PausePanel == null || transition.PausePanel.activeSelf
            || transition.PausePanel.GetComponent<PauseDamageUI>() == null
            || transition.PausePanel.GetComponentInChildren<PauseProjectileUpgradesUI>(true) == null
            || PrefabUtility.GetCorrespondingObjectFromOriginalSource(transition.PausePanel)
                != AssetDatabase.LoadAssetAtPath<GameObject>(PauseDamageAuthoring.PrefabPath))
            throw new Exception("Gameplay pause prefab reference did not survive scene serialization.");
        transition.ConfigurePanelLayers();
        if (transition.PausePanel.GetComponent<Canvas>().sortingOrder != 400)
            throw new Exception("Pause must stay above the tutorial and other gameplay UI.");
        Debug.Log("PAUSE_SCENE_BINDING_PASSED"); EditorApplication.Exit(0);
    }
    private static IEnumerator Checks()
    {
        var player = new GameObject("Player"); player.tag = "Player"; player.transform.position = Vector3.down * 8;
        camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 6;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.04f, 0.09f, 0.065f);
        room = new GameObject("Damage Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        Brick target = Target(Vector2.zero, 10000);
        ProjectileDamageStats.BeginLevel();
        CombatDamageSource[] sources = { CombatDamageSource.Firefly, CombatDamageSource.Hedgehog,
            CombatDamageSource.Rabbit, CombatDamageSource.HoneyBadger, CombatDamageSource.Boar,
            CombatDamageSource.Beaver, CombatDamageSource.Frog, CombatDamageSource.Squirrel };
        for (int i = 0; i < sources.Length; i++)
        {
            target.TakeProjectileDamage((i + 1) * 10, 0, sources[i]);
            Require(ProjectileDamageStats.ForAnimal(i) == (i + 1) * 10, "Direct damage credited to " + sources[i]);
        }
        Require(ProjectileDamageStats.TotalDamage == 360, "Level total sums all eight projectile types");
        Brick fragile = Target(Vector2.right * 3, 7); fragile.TakeDamage(100, CombatDamageSource.Boar);
        Require(ProjectileDamageStats.ForAnimal(4) == 57, "Overkill counts only seven remaining HP");
        double before = ProjectileDamageStats.TotalDamage;
        target.TakeDamage(20); target.TakeDamage(20, CombatDamageSource.Fire); target.TakeDamage(20, CombatDamageSource.Poison);
        Require(ProjectileDamageStats.TotalDamage == before, "Enemy and unowned damage does not inflate projectile totals");
        Set(target, "immuneToPoison", true); target.TakePoisonDamage(100, false);
        Require(ProjectileDamageStats.TotalDamage == before, "Poison immunity adds no damage");
        Set(target, "immuneToPoison", false);
        var firefly = new GameObject("Firefly Effect").AddComponent<FireflyImpactEffect>();
        firefly.ApplyTo(target); EnemyStatusEffects.ProcessAllAfterEnemyTurn(1);
        Require(ProjectileDamageStats.ForAnimal(0) == 35, "Firefly periodic burn belongs to firefly");
        FireflyBonfire.Spawn(room, Vector3.zero, 17, 1, 1, 1); FireflyBonfire.ProcessAllAfterEnemyTurn(1);
        Require(ProjectileDamageStats.ForAnimal(0) == 52, "Firefly bonfire belongs to firefly");
        FireflyBonfire.Spawn(room, Vector3.zero, 11, 1, 1, 1, AnimalProjectileQueue.SquirrelIndex);
        FireflyBonfire.ProcessAllAfterEnemyTurn(2);
        Require(ProjectileDamageStats.ForAnimal(7) == 91 && ProjectileDamageStats.ForAnimal(0) == 52,
            "Acorn bonfire belongs to squirrel instead of firefly");
        var cloud = HoneyBadgerPoisonCloud.Spawn(room, Vector3.zero, 19, 1, 2);
        HoneyBadgerPoisonCloud.ProcessAllAfterEnemyTurn(1);
        Require(ProjectileDamageStats.ForAnimal(3) == 59, "Poison cloud belongs to honey badger");
        typeof(HoneyBadgerPoisonCloud).GetMethod("Ignite", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cloud, null);
        HoneyBadgerPoisonCloud.ProcessAllAfterEnemyTurn(2);
        Require(ProjectileDamageStats.ForAnimal(3) == 78 && ProjectileDamageStats.ForAnimal(0) == 52,
            "Burning poison cloud retains its honey badger owner");
        target.TakeFrogDamage(15, 0, true);
        Require(ProjectileDamageStats.ForAnimal(6) == 85, "Seated frog periodic damage belongs to frog");
        yield return Clear(); Object.Destroy(firefly.gameObject);

        ProjectileDamageStats.BeginLevel(); target = Target(Vector2.zero, 10000); Brick neighbour = Target(Vector2.right, 1000);
        var squirrel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Squirrel.prefab"));
        squirrel.GetComponent<Rigidbody2D>().simulated = false; squirrel.GetComponent<Animal>().enabled = false;
        target.ApplyProjectileHit(squirrel.GetComponent<Animal>(), Vector2.zero, Vector2.up); Object.Destroy(squirrel);
        Require(ProjectileDamageStats.ForAnimal(7) == 100, "Actual squirrel planting hit credited to squirrel");
        target.TakeDamage(target.hp); yield return WaitForAcornBlasts();
        Require(ProjectileDamageStats.ForAnimal(7) == 200 && neighbour.hp == 900 && ProjectileDamageStats.ForAnimal(0) == 0,
            "Actual acorn death explosion credits squirrel fire damage");
        FireflyBonfire.ProcessAllAfterEnemyTurn(100);
        Require(ProjectileDamageStats.ForAnimal(7) == 210 && ProjectileDamageStats.ForAnimal(0) == 0,
            "Actual acorn weak bonfire credits squirrel");
        yield return Clear();

        var queueObject = new GameObject("Queue"); queueObject.SetActive(false);
        var queue = queueObject.AddComponent<AnimalProjectileQueue>(); Set(queue, "createWheelOnStart", false);
        var entries = new List<AnimalProjectileQueue.AnimalProjectileEntry>();
        for (int i = 0; i < 8; i++)
        {
            var entry = new AnimalProjectileQueue.AnimalProjectileEntry();
            Set(entry, "prefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/BaseBullet.prefab"));
            Set(entry, "icon", AssetDatabase.LoadAssetAtPath<Sprite>(PauseDamageAuthoring.IconPaths[i])); entries.Add(entry);
        }
        Set(queue, "animals", entries); queueObject.SetActive(true);
        Require(queue.ConfigureRunLoadout(new[] { 0, 2, 4, 6, 7 }, 2), "Fixture has five selected projectile types");
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PauseDamageAuthoring.PrefabPath));
        var ui = panel.GetComponent<PauseDamageUI>();
        Require(!panel.activeSelf, "Authored pause starts closed");
        Require(panel.GetComponent<Canvas>().sortingOrder == 400, "Pause canvas sits above gameplay layers");
        var buttons = panel.GetComponentsInChildren<Button>(true).Where(b => b.onClick.GetPersistentEventCount() == 1
            && new[] { "Continue", "MainMenu", "Restart" }.Contains(b.onClick.GetPersistentMethodName(0))).ToArray();
        Require(buttons.Length == 3 && buttons.All(b => b.onClick.GetPersistentEventCount() == 1), "All three authored navigation buttons are wired");
        Require(buttons.Select(b => b.onClick.GetPersistentMethodName(0)).OrderBy(s => s).SequenceEqual(new[] { "Continue", "MainMenu", "Restart" }),
            "Saved buttons continue, restart and exit to menu");
        ProjectileDamageStats.BeginLevel(); panel.SetActive(true); yield return Frames(3);
        var rows = (PauseDamageUI.DamageRow[])Get(ui, "rows");
        Require(rows.All(r => r.button != null && r.button.targetGraphic.raycastTarget), "All eight saved damage rows can be clicked");
        Require(rows.Count(r => r.root.gameObject.activeSelf) == 5 && rows.Take(5).All(r => r.damage.text == "0" && r.fill.rectTransform.anchorMax.x == 0),
            "Empty level shows all five chosen types with zero bars, including locked types");
        Require(((Text)Get(ui, "hint")).text.Contains("первых попаданий"), "Zero-damage hint is visible");
        double[] sample = { 12340, 0, 4800, 0, 7350, 0, 3120, 5610 };
        for (int i = 0; i < 8; i++) ProjectileDamageStats.Record((float)sample[i], sources[i]);
        Require(rows.Take(5).Select(r => r.name.text).SequenceEqual(new[] { "Светлячок", "Кабан", "Белка", "Кролик", "Лягушка" }),
            "Rows sort by descending actual damage");
        double total = sample.Sum();
        Require(Math.Abs(rows[0].fill.rectTransform.anchorMax.x - sample[0] / total) < 0.0001,
            "Bar length matches the projectile share of all damage");
        Require(rows.Take(5).All(r => r.icon.sprite != null), "All five rows show authored projectile icons");
        panel.SetActive(false); panel.SetActive(true);
        Require(ProjectileDamageStats.TotalDamage == total && rows[0].name.text == "Светлячок", "Closing and reopening pause preserves level statistics");

        var gmObject = new GameObject("Paused Manager"); gmObject.SetActive(false);
        var gm = gmObject.AddComponent<GameManager>();
        typeof(GameManager).GetProperty("Instance").SetValue(null, gm);
        var transitionObject = new GameObject("Pause Controller"); transitionObject.SetActive(false);
        var transition = transitionObject.AddComponent<LevelTransition>(); transition.PausePanel = panel;
        transitionObject.SetActive(true); transition.PauseButtonPressed();
        Require(gm.isPaused && Time.timeScale == 0 && panel.activeSelf, "Pause opens and stops gameplay");
        yield return Capture(panel, "Logs/PauseDamagePortrait.png", 540, 960);
        Require(panel.transform.Find("Pause Card/Forest Panel/Pause Title").GetComponent<Text>().cachedTextGenerator.vertexCount > 4
            && ((Text)Get(ui, "totalDamage")).cachedTextGenerator.vertexCount > 4,
            "Large pause heading and total damage render their glyphs");
        yield return CheckProjectileUpgrades(panel, ui, queue, gm, sample, sources);
        Button resume = buttons.Single(b => b.onClick.GetPersistentMethodName(0) == "Continue"); resume.onClick.Invoke();
        Require(!gm.isPaused && Time.timeScale == 1 && !panel.activeSelf, "Authored Continue button resumes gameplay");
        Require(ProjectileDamageStats.TotalDamage == total, "Resuming preserves accumulated damage");
        transition.PauseButtonPressed();
        Require(!((PauseProjectileUpgradesUI)Get(ui, "projectileUpgrades")).IsOpen,
            "Reopening pause starts at statistics with the old detail window closed");
        yield return Capture(panel, "Logs/PauseDamageLandscape.png", 960, 540);
        var scroll = (ScrollRect)Get(ui, "scroll");
        Require(scroll.content.rect.height > scroll.viewport.rect.height, "Landscape view scrolls the damage list");
        var position = scroll.content.anchoredPosition;
        scroll.OnScroll(new UnityEngine.EventSystems.PointerEventData(null) { scrollDelta = Vector2.down * 5 });
        Canvas.ForceUpdateCanvases();
        Require(scroll.content.anchoredPosition != position, "Damage list scrolls while time is paused");
        resume.onClick.Invoke();
        ProjectileDamageStats.BeginLevel();
        Require(ProjectileDamageStats.TotalDamage == 0 && Enumerable.Range(0, 8).All(i => ProjectileDamageStats.ForAnimal(i) == 0),
            "New level resets every damage total");
        typeof(GameManager).GetProperty("Instance").SetValue(null, null);
        Object.Destroy(gmObject); Time.timeScale = 1;
    }
    private static IEnumerator CheckProjectileUpgrades(GameObject panel, PauseDamageUI ui,
        AnimalProjectileQueue queue, GameManager manager, double[] sample, CombatDamageSource[] sources)
    {
        if (EventSystem.current == null) new GameObject("Pause Input", typeof(EventSystem), typeof(StandaloneInputModule));
        var rows = (PauseDamageUI.DamageRow[])Get(ui, "rows");
        var details = (PauseProjectileUpgradesUI)Get(ui, "projectileUpgrades");
        var upgradeRows = (PauseProjectileUpgradesUI.UpgradeRow[])Get(details, "rows");
        Require(details != null && !details.gameObject.activeSelf, "Authored upgrade window starts closed inside pause");
        Require(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.RepeatFire)
            && queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.Burn), "Fixture chooses actual firefly upgrades");
        int ammo = queue.TotalRemainingAmmo, level = queue.GetUpgradeLevel(0), gold = MetaProgression.Gold;
        double damage = ProjectileDamageStats.TotalDamage;
        Click(rows[0].button, panel.GetComponent<Canvas>());
        Require(details.IsOpen && details.AnimalIndex == 0 && ((Text)Get(details, "projectileName")).text == "Светлячок",
            "Pointer click on a damage row opens its projectile's upgrades");
        Require(upgradeRows.Count(r => r.root.gameObject.activeSelf) == 2
            && upgradeRows.Take(2).Select(r => r.rank.text).SequenceEqual(new[] { "1 / 3", "1 / 3" }),
            "Only the two chosen firefly branches show their actual levels");
        Require(queue.TotalRemainingAmmo == ammo && queue.GetUpgradeLevel(0) == level && MetaProgression.Gold == gold
            && ProjectileDamageStats.TotalDamage == damage && manager.isPaused && Time.timeScale == 0,
            "Viewing upgrades preserves ammo, levels, gold, damage and pause state");
        Require(queue.Upgrade(0, AnimalProjectileQueue.UpgradeKind.Burn) && upgradeRows[1].rank.text == "2 / 3",
            "Open details follow an actual upgrade-state change");
        yield return Frames(2);
        var resumeRect = (RectTransform)panel.transform.Find("Pause Card/Forest Panel/Continue");
        Vector2 resumePoint = RectTransformUtility.WorldToScreenPoint(camera, resumeRect.TransformPoint(resumeRect.rect.center));
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = resumePoint }, hits);
        Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(details.transform),
            "The modal intercepts clicks over the pause navigation (" + (hits.Count > 0 ? hits[0].gameObject.name : "no raycast") + ")");
        ((Button)Get(details, "closeButton")).onClick.Invoke();
        Require(!details.IsOpen && panel.activeSelf && manager.isPaused && Time.timeScale == 0,
            "Back returns to statistics without resuming gameplay");

        ProjectileDamageStats.Record(50000f, CombatDamageSource.Frog);
        Require(rows[0].animalIndex == 6, "Damage sorting rebinds the first row to the frog");
        Click(rows[0].button, panel.GetComponent<Canvas>());
        Require(details.AnimalIndex == 6 && ((Text)Get(details, "projectileName")).text == "Лягушка"
            && upgradeRows.All(r => !r.root.gameObject.activeSelf) && ((Text)Get(details, "empty")).gameObject.activeSelf,
            "The same row button opens the newly sorted, locked frog with an empty state");
        details.transform.Find("Backdrop").GetComponent<Button>().onClick.Invoke();
        Require(!details.IsOpen && manager.isPaused, "Clicking outside the card closes only the detail window");

        Require(queue.Upgrade(6, AnimalProjectileQueue.UpgradeKind.Ammo), "Fixture unlocks frog");
        for (int rank = 0; rank < 3; rank++)
        {
            if (queue.GetAmmoUpgradeLevel(6) < 3) queue.Upgrade(6, AnimalProjectileQueue.UpgradeKind.Ammo);
            Require(queue.Upgrade(6, AnimalProjectileQueue.UpgradeKind.FrogRadius)
                && queue.Upgrade(6, AnimalProjectileQueue.UpgradeKind.FrogChorus), "Fixture improves frog branches " + (rank + 1));
        }
        Require(queue.Upgrade(6, AnimalProjectileQueue.UpgradeKind.FrogUltimate), "Fixture selects KVA");
        Click(rows[0].button, panel.GetComponent<Canvas>());
        Require(upgradeRows.Count(r => r.root.gameObject.activeSelf) == 4 && upgradeRows[3].title.text == "КВА"
            && upgradeRows[3].rank.text == "УЛЬТА" && upgradeRows[1].description.text.Contains("5")
            && upgradeRows[2].description.text.Contains("3%"), "Frog window shows its three maxed branches and chosen KVA");
        yield return Capture(panel, "Logs/PauseProjectileUpgradesPortrait.png", 540, 960);
        var allowed = Get(queue, "selectedForRun"); details.Close(); Set(queue, "selectedForRun", null);
        for (int index = 0; index < 8; index++)
        {
            queue.Unlock(index); var entry = queue.Animals[index];
            for (int rank = 0; rank < 3; rank++)
            {
                if (index != AnimalProjectileQueue.FireflyIndex && index != AnimalProjectileQueue.RabbitIndex) entry.UpgradeAmmo(3);
                switch (index)
                {
                    case 0: entry.UpgradeRepeatFire(3); entry.UpgradeBurn(3); break;
                    case 1: entry.UpgradeHedgehogRadius(3); entry.UpgradeHedgehogQuillDamage(3); break;
                    case 2: entry.UpgradeRabbitPackStrength(3); entry.UpgradeRabbitBreeding(3); entry.UpgradeRabbitFirstBreeding(3); break;
                    case 3: entry.UpgradeHoneyBadgerRadius(3); entry.UpgradeHoneyBadgerChain(3); break;
                    case 4: entry.UpgradeBoarBounces(3); entry.UpgradeBoarFirstHit(3); break;
                    case 5: entry.UpgradeBeaverDefenseHealth(3); entry.UpgradeBeaverDamage(3); break;
                    case 6: entry.UpgradeFrogRadius(3); entry.UpgradeFrogChorus(3); break;
                    case 7: entry.UpgradeSquirrelDetonation(3); entry.UpgradeSquirrelBinding(3); break;
                }
            }
            switch (index)
            {
                case 0: entry.UpgradeFireflyUltimate(); break;
                case 1: entry.UpgradeHedgehogUltimate(); break;
                case 2: entry.UpgradeRabbitUltimate(); break;
                case 3: entry.UpgradeHoneyBadgerUltimate(); break;
                case 4: entry.UpgradeBoarUltimate(); break;
                case 5: entry.UpgradeBeaverUltimate(); break;
                case 6: entry.UpgradeFrogUltimate(); break;
                case 7: entry.UpgradeSquirrelUltimate(); break;
            }
            var chosen = ProjectileUpgradeSummary.ForAnimal(queue, index);
            int expectedRows = index == AnimalProjectileQueue.FireflyIndex ? 3
                : index == AnimalProjectileQueue.RabbitIndex && queue.StartingAnimalIndex != AnimalProjectileQueue.RabbitIndex ? 5 : 4;
            Require(chosen.Count == expectedRows && chosen.Count(item => item.Ultimate) == 1
                && ((index != AnimalProjectileQueue.FireflyIndex && index != AnimalProjectileQueue.RabbitIndex)
                    || !chosen.Any(item => item.Title == "Количество"))
                && chosen.All(item => !string.IsNullOrEmpty(item.Description)), "All selected branches and ultimate have readable summaries: " + PauseDamageUI.AnimalNames[index]);
        }
        Require(ProjectileUpgradeSummary.ForAnimal(queue, 5).Any(item => item.Description.Contains("4%"))
            && ProjectileUpgradeSummary.ForAnimal(queue, 5).Any(item => item.Description.Contains("+15")), "Beaver summary reflects four-percent defense and halved damage upgrades");
        Require(ProjectileUpgradeSummary.ForAnimal(queue, 7).Any(item => item.Description.Contains("20%"))
            && ProjectileUpgradeSummary.ForAnimal(queue, 7).Any(item => item.Description.Contains("9%")), "Squirrel summary reflects current detonation and binding chances");
        Require(ProjectileUpgradeSummary.ForAnimal(queue, 3).Single(item => item.Ultimate).Title == "Абсолютная ярость",
            "Honey badger summary uses its actual selected ultimate");
        Set(queue, "selectedForRun", allowed); Set(queue, "selectedUpgradeCount", 1);
        details.Show(queue, 0, rows[1].icon.sprite);
        Require(upgradeRows.Count(r => r.root.gameObject.activeSelf) == 5 && upgradeRows[4].description.text.Contains("8%"),
            "Shared damage upgrade appears alongside the projectile's chosen branches");
        yield return Capture(panel, "Logs/PauseProjectileUpgradesLandscape.png", 960, 540);
        var detailCard = (RectTransform)Get(details, "card"); var detailScroll = (ScrollRect)Get(details, "scroll");
        Require(detailCard.rect.width <= panel.GetComponent<RectTransform>().rect.width
            && detailCard.rect.height <= panel.GetComponent<RectTransform>().rect.height,
            "Upgrade card fits landscape screen bounds");
        Require(detailScroll.content.rect.height > detailScroll.viewport.rect.height, "Long upgrade list is scrollable in landscape");
        var position = detailScroll.content.anchoredPosition;
        detailScroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = Vector2.down * 4 }); Canvas.ForceUpdateCanvases();
        Require(detailScroll.content.anchoredPosition != position && Time.timeScale == 0, "Upgrade list scrolls while paused");
        details.Close();
        ProjectileDamageStats.BeginLevel();
        for (int index = 0; index < sample.Length; index++) ProjectileDamageStats.Record((float)sample[index], sources[index]);
        rows[0].button.onClick.Invoke();
        Require(details.IsOpen, "Details can reopen before the parent pause is closed");
    }

    private static void Click(Button button, Canvas canvas)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "Visible damage row receives the first UI raycast");
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static Brick Target(Vector2 position, float hp)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/SwampDemon.prefab");
        var enemy = Object.Instantiate(prefab, position, Quaternion.identity, room.transform).GetComponent<Brick>();
        enemy.levelGenerator = room; enemy.hp = hp; room.EnemyCollection.Add(enemy.gameObject); return enemy;
    }
    private static IEnumerator Clear()
    {
        foreach (Brick enemy in Object.FindObjectsOfType<Brick>()) Object.Destroy(enemy.gameObject);
        foreach (FireflyBonfire fire in Object.FindObjectsOfType<FireflyBonfire>()) Object.Destroy(fire.gameObject);
        foreach (HoneyBadgerPoisonCloud cloud in Object.FindObjectsOfType<HoneyBadgerPoisonCloud>()) Object.Destroy(cloud.gameObject);
        foreach (SquirrelAcorn seed in Object.FindObjectsOfType<SquirrelAcorn>()) Object.Destroy(seed.gameObject);
        yield return Frames(3); room.EnemyCollection.Clear();
    }
    private static IEnumerator Capture(GameObject panel, string path, int width, int height)
    {
        Screen.SetResolution(width, height, false);
        var render = new RenderTexture(width, height, 24); render.Create(); camera.targetTexture = render;
        Canvas canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 1;
        yield return Frames(5); Canvas.ForceUpdateCanvases();
        var card = (RectTransform)Get(panel.GetComponent<PauseDamageUI>(), "card");
        var root = panel.GetComponent<RectTransform>();
        Require(card.rect.width <= root.rect.width && card.rect.height <= root.rect.height, "Pause card fits " + width + "x" + height);
        var viewport = ((ScrollRect)Get(panel.GetComponent<PauseDamageUI>(), "scroll")).viewport;
        Require(viewport.rect.height > 100, "Damage viewport remains usable at " + width + "x" + height);
        camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(image); render.Release(); Object.Destroy(render);
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator WaitForAcornBlasts()
    {
        float limit = Time.time + 3f;
        while (SquirrelAcorn.HasActiveBlasts && Time.time < limit) yield return null;
        Require(!SquirrelAcorn.HasActiveBlasts, "All timed acorn hits finish before damage totals are checked");
    }
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool success, string text) { if (!success) throw new Exception(text); Report.Add("PASS " + text); }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Pause validation timed out");
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
        bool support = SessionState.GetBool(FrogSupport, false); SessionState.SetBool(FrogSupport, false);
        string success = support ? "FROG_SUPPORT_DAMAGE_PASSED" : "PAUSE_DAMAGE_PASSED";
        Report.Add(error == null ? success : "FAIL " + error);
        File.WriteAllLines(support ? "Logs/FrogSupportDamageValidation.txt" : "Logs/PauseDamageValidation.txt", Report);
        if (error != null) Debug.LogError(error); else Debug.Log(success);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
