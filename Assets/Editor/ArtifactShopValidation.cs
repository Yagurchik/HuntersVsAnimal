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
public static class ArtifactShopValidation
{
    private const string Running = "ArtifactShopValidation.Running";
    private static readonly List<string> Report = new List<string>();
    private static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    private static int lastFrame = -1;
    private static double deadline;
    private static LevelGenerator room;
    static ArtifactShopValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Report.AddRange(SessionState.GetString("ArtifactShopValidation.Economy", "").Split('\n').Where(s => s.Length > 0));
            Steps.Clear(); Steps.Push(PlayChecks());
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new InvalidOperationException("Run only in the isolated test project.");
        Directory.CreateDirectory("Logs");
        try
        {
            ArtifactShopAuthoring.BakeCatalogue();
            EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
            EconomyChecks();
            SessionState.SetString("ArtifactShopValidation.Economy", string.Join("\n", Report));
            ClearInventory();
            PlayerPrefs.SetInt("ForestAnimalWar.Meta.Gold", 10000);
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(error); }
    }
    [Serializable] private class ReferenceCatalogue { public ReferenceItem[] Items; }
    [Serializable] private class ReferenceItem { public string Id; public int GoldCost; public ArtifactIngredient[] Ingredients; public ArtifactBonus[] Bonuses; }
    [Serializable] private class IngredientsData { public ArtifactIngredient[] Ingredients; }
    private static void EconomyChecks()
    {
        Require(MetaProgression.ArtifactCount == 60, "60 authored artifact assets");
        var ids = new HashSet<string>(); int ordinary = 0, assembled = 0, nested = 0;
        var covered = new HashSet<ArtifactStat>();
        for (int i = 0; i < 60; i++)
        {
            var item = MetaProgression.GetArtifactDefinition(i);
            Require(item != null && ids.Add(item.Id) && item.Icon != null && item.GoldCost > 0
                && !string.IsNullOrEmpty(item.Description) && item.Bonuses.Length > 0, "Valid unique asset and icon: " + item.Name);
            foreach (var bonus in item.Bonuses) { Require(bonus.Value > 0, "Positive effect: " + item.Id + "/" + bonus.Stat); covered.Add(bonus.Stat); }
            if (item.IsAssembled)
            {
                assembled++;
                Require(item.Ingredients.Length >= 2 && item.Ingredients.Length <= 3, "Complete recipe: " + item.Name);
                foreach (var component in item.Ingredients)
                    Require(MetaProgression.FindArtifact(component.Id) >= 0 && component.Amount > 0, "Resolvable component: " + component.Id);
                if (item.Ingredients.Any(c => MetaProgression.GetArtifactDefinition(MetaProgression.FindArtifact(c.Id)).IsAssembled)) nested++;
                Require(MetaProgression.GetArtifactTotalCost(i) > item.GoldCost, "Full recipe price: " + item.Name);
            }
            else ordinary++;
        }
        Require(ordinary == 30 && assembled == 30 && nested == 5, "30 ordinary, 30 assembled, five advanced recipes");
        var retired = new HashSet<int>(Enumerable.Range(13, 7).Concat(Enumerable.Range(21, 7)));
        Require(covered.All(stat => !retired.Contains((int)stat)), "No artifact requires a special animal");
        Require(covered.Count == Enum.GetValues(typeof(ArtifactStat)).Length - retired.Count, "Every active bonus has catalogue items");
        Require(Mathf.Abs(MetaProgression.GetArtifactDefinition(57).Bonuses.First(b => b.Stat == ArtifactStat.DefenseConversionPct).Value - 0.002f) < 0.000001f,
            "Guardian girdle converts exactly 0.2 percent of all owned damage");
        var original = JsonUtility.FromJson<ReferenceCatalogue>(File.ReadAllText("Tools/ArtifactCatalogue.before-universal.json"));
        for (int i = 0; i < 60; i++)
        {
            var current = MetaProgression.GetArtifactDefinition(i); var previous = original.Items[i];
            Require(current.Id == previous.Id && current.GoldCost == previous.GoldCost
                && JsonUtility.ToJson(new IngredientsData { Ingredients = current.Ingredients }) == JsonUtility.ToJson(new IngredientsData { Ingredients = previous.Ingredients }),
                "Ownership, price and recipe retained: " + current.Id);
            var oldFirefly = previous.Bonuses.Where(b => b.Stat == ArtifactStat.FireflyBurnPct || b.Stat == ArtifactStat.FireflyLightRadiusBonus).ToArray();
            Require(oldFirefly.All(b => current.Bonuses.Any(c => c.Stat == b.Stat && c.Value == b.Value)), "Firefly bonuses retained: " + current.Id);
        }
        Require(MetaProgression.GetArtifactDefinition(0).Id == "sun_lens"
            && MetaProgression.GetArtifactDefinition(7).Id == "harvest_crown", "Existing artifact IDs/order retained");

        ClearInventory(); PlayerPrefs.SetInt("ForestAnimalWar.Meta.Gold", 0);
        Require(!MetaProgression.TryAcquireArtifact(0) && !MetaProgression.TryAcquireArtifact(30)
            && MetaProgression.OwnedArtifactAmount(0) == 0, "Cannot buy/craft without resources");
        PlayerPrefs.SetInt("ForestAnimalWar.Meta.Gold", 1000000);
        Require(MetaProgression.TryAcquireArtifact(0) && MetaProgression.TryAcquireArtifact(0)
            && MetaProgression.OwnedArtifactAmount(0) == 2, "Duplicate purchases are saved as quantities");
        Require(MetaProgression.TryEquipArtifact(0, 0) && MetaProgression.AvailableArtifactAmount(0) == 1
            && Mathf.Abs(MetaProgression.PumpkinProjectileDamageMultiplier - 1.05f) < 0.0001f,
            "Only one equipped copy contributes and spare remains available");
        int before = MetaProgression.Gold;
        Require(!MetaProgression.TryAcquireArtifact(30) && MetaProgression.Gold == before
            && MetaProgression.OwnedArtifactAmount(0) == 2, "Failed recipe is atomic");
        EnsureComponent(24);
        Require(MetaProgression.TryAcquireArtifact(30) && MetaProgression.OwnedArtifactAmount(0) == 1
            && MetaProgression.GetEquippedArtifact(0) == 0 && MetaProgression.OwnedArtifactAmount(30) == 1,
            "Craft spends spare component without removing equipped copy");
        Require(!MetaProgression.CanAcquireArtifact(30, out string hint) && hint.Contains("Сними"),
            "Equipped last component is protected with an actionable hint");
        Require(MetaProgression.TryUnequipArtifact(0), "Unequipping returns component availability");
        ClearInventory(); PlayerPrefs.SetInt("ForestAnimalWar.Meta.Gold", 1000000);
        for (int i = 0; i < 60; i++) { EnsureComponent(i); Require(MetaProgression.OwnedArtifactAmount(i) > 0, "Reachable through purchases/recipes: " + i); }
        Require(Enumerable.Range(0, 8).All(i => MetaProgression.GetEquippedArtifact(i) < 0), "Acquisition never equips automatically");
        ClearInventory(); PlayerPrefs.SetInt("ForestAnimalWar.Meta.Gold", 100000);
        for (int i = 0; i < 30; i++) Require(MetaProgression.TrySpinArtifact(out int index) && index < 30, "Roulette ordinary item " + i);
        Require(!MetaProgression.TrySpinArtifact(out _) && MetaProgression.UnownedArtifactCount == 30,
            "Roulette stops at ordinary pool and cannot award assembled items");
        ClearInventory(); Give(0, 1); Give(7, 1);
        Require(MetaProgression.IsArtifactOwned(0) && MetaProgression.TryEquipArtifact(0, 0)
            && MetaProgression.TryEquipArtifact(7, 1), "Legacy boolean ownership migrates naturally");
        Require(Mathf.Abs(MetaProgression.VictoryAcornMultiplier - 1.2f) < 0.0001f, "Legacy crown effect retained");
        ClearInventory();
    }
    private static void EnsureComponent(int index)
    {
        var item = MetaProgression.GetArtifactDefinition(index);
        if (item.IsAssembled)
            foreach (var component in item.Ingredients)
                while (MetaProgression.AvailableArtifactAmount(MetaProgression.FindArtifact(component.Id)) < component.Amount)
                    EnsureComponent(MetaProgression.FindArtifact(component.Id));
        Require(MetaProgression.TryAcquireArtifact(index), "Acquire " + item.Id);
    }
    private static void Give(int index, int amount = 1)
        => PlayerPrefs.SetInt("ForestAnimalWar.Village.Artifact." + MetaProgression.GetArtifactDefinition(index).Id, amount);
    private static void Equip(params int[] indices)
    {
        ClearInventory();
        for (int i = 0; i < indices.Length; i++) { Give(indices[i]); Require(MetaProgression.TryEquipArtifact(indices[i], i), "Equip " + indices[i]); }
    }
    private static void ClearInventory()
    {
        for (int i = 0; i < MetaProgression.ArtifactCount; i++)
            PlayerPrefs.DeleteKey("ForestAnimalWar.Village.Artifact." + MetaProgression.GetArtifactDefinition(i).Id);
        for (int i = 0; i < MetaProgression.EquipmentSlotCount; i++) PlayerPrefs.DeleteKey("ForestAnimalWar.Hero.EquipmentSlot." + i);
        ArtifactEquipmentBonuses.Invalidate();
    }
    private static IEnumerator PlayChecks()
    {
        Screen.SetResolution(720, 1280, false);
        yield return Frames(5);
        var hub = Object.FindObjectOfType<MenuHubController>();
        Require(hub != null, "Menu starts with all authored references");
        hub.SelectTab(2);
        Transform page = hub.tabs[2].page.transform;
        var building = page.Find("Forest Village Runtime/Viewport/Content/Village Field/ForestShopBuilding").GetComponent<VillageBuildingView>();
        building.openButton.onClick.Invoke();
        yield return Click(page.Find("Building Menu/Panel/Shop Offers/Open Catalog").GetComponent<Button>());
        var shop = page.GetComponentInChildren<ArtifactShopUI>(true);
        Require(shop.IsOpen && hub.IsDialogOpen && shop.Cards.Length == 30, "Shop building opens native catalogue overlay");
        int childrenBefore = shop.GetComponentsInChildren<Transform>(true).Length;
        Require(shop.Cards.All(c => c.Icon.sprite != null && c.Button.gameObject.activeSelf), "All 30 ordinary cards show icons");
        shop.Search.text = "пруда";
        Require(shop.Cards.Count(c => c.Button.gameObject.activeSelf) == 1, "Search filters item names");
        shop.Search.text = ""; yield return Click(shop.AssembledTab);
        Require(shop.Cards.All(c => c.Button.gameObject.activeSelf && c.Price.text.StartsWith("СБОРКА")), "Assembled tab shows 30 recipes");
        yield return Capture(hub, "Logs/ArtifactShop.png");
        yield return Click(shop.Cards[0].Button);
        Require(!shop.AcquireButton.interactable && shop.Components[0].Name.text == "Солнечная линза", "Recipe shows missing components");
        yield return Click(shop.Components[0].Button);
        int gold = MetaProgression.Gold;
        yield return Click(shop.AcquireButton);
        Require(MetaProgression.OwnedArtifactAmount(0) == 1 && MetaProgression.Gold == gold - 60, "Purchase button charges gold");
        yield return Click(shop.BackButton); yield return Click(shop.Components[1].Button); yield return Click(shop.AcquireButton); yield return Click(shop.BackButton);
        Require(shop.AcquireButton.interactable, "Buying components and returning enables crafting");
        gold = MetaProgression.Gold; yield return Click(shop.AcquireButton);
        Require(MetaProgression.OwnedArtifactAmount(30) == 1 && MetaProgression.OwnedArtifactAmount(0) == 0
            && MetaProgression.OwnedArtifactAmount(24) == 0 && MetaProgression.Gold == gold - 80, "Craft button consumes exact components and fee");
        shop.SelectArtifact(57);
        yield return Capture(hub, "Logs/ArtifactRecipe.png");
        Require(shop.GetComponentsInChildren<Transform>(true).Length == childrenBefore, "Runtime shop never creates replacement controls");
        yield return Click(shop.CloseButton); Require(!hub.IsDialogOpen && !shop.IsOpen, "Close button exits shop");
        ClearInventory();
        for (int i = 0; i < 60; i++) Give(i, 3);
        hub.SelectTab(1); hub.Refresh();
        Transform hero = hub.tabs[1].page.transform; Transform content = hero.Find("Hero Equipment Runtime/Viewport/Content");
        Require(Enumerable.Range(1, 60).All(i => content.Find("Backpack Cell " + i).gameObject.activeSelf), "Backpack displays every artifact type without overflow");
        yield return Click(content.Find("Equipment Slot 1").GetComponent<Button>());
        hero.Find("Hero Equipment Runtime").GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
        yield return Frames(3);
        yield return Click(content.Find("Backpack Cell 60").GetComponent<Button>());
        yield return Click(hero.Find("Artifact Details/Panel/Equip").GetComponent<Button>());
        hero.Find("Hero Equipment Runtime").GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
        Require(MetaProgression.GetEquippedArtifact(0) == 59 && MetaProgression.AvailableArtifactAmount(59) == 2, "New item equips while spare copies stay in bag");
        yield return Capture(hub, "Logs/ArtifactBackpack.png");
        hero.Find("Artifact Details").gameObject.SetActive(false);
        hub.SelectTab(2); shop.Show(); hub.CloseDialogs();
        Require(!shop.IsOpen, "Menu close/escape route includes catalogue");
        hub.gameObject.SetActive(false);
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
        yield return CombatChecks();
    }
    private static IEnumerator CombatChecks()
    {
        new GameObject("Player").tag = "Player";
        var camera = new GameObject("Combat Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        room = new GameObject("Artifact Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        Brick target = Target(Vector2.zero, 10000);
        Equip(0, 19); ProjectileDamageStats.BeginLevel(); ForceRollAbove(0.02f);
        target.TakeProjectileDamage(100, 0, CombatDamageSource.Boar);
        Require(Mathf.Abs(10000 - target.hp - 110f) < 0.01f
            && Math.Abs(ProjectileDamageStats.ForAnimal(4) - 110) < 0.01, "Common damage modifiers apply once to real HP");
        Equip(15, 23); float hp = target.hp; target.TakeAfterTurnDamage(100, CombatDamageSource.Fire, 7);
        Require(Mathf.Abs(hp - target.hp - 110f) < 0.01f, "Owned fire gains only the fire modifier");
        Equip(16, 18); typeof(Brick).GetField("immuneToPoison", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, false); hp = target.hp; target.TakePoisonDamage(100, false);
        Require(Mathf.Abs(hp - target.hp - 110f) < 0.01f, "Poison gains its elemental modifier without requiring badger");
        hp = target.hp; target.TakeAfterTurnDamage(100, CombatDamageSource.Fire);
        Require(Mathf.Abs(hp - target.hp - 100) < 0.01f, "Enemy/unknown fire does not gain player artifact bonuses");
        Equip(12); ForceRollBelow(0.03f); hp = target.hp;
        target.TakeProjectileDamage(100, 0, CombatDamageSource.Firefly);
        Require(Mathf.Abs(hp - target.hp - 150) < 0.01f, "Critical proc changes actual direct-hit damage");
        ForceRollBelow(0.03f); hp = target.hp; target.TakeAfterTurnDamage(100, CombatDamageSource.Fire, 0);
        Require(Mathf.Abs(hp - target.hp - 100) < 0.01f, "Damage over time cannot critically hit");
        ForceRollBelow(0.03f); hp = target.hp;
        target.TakeProjectileDamage(100, 0, CombatDamageSource.Hedgehog, -1, false);
        Require(Mathf.Abs(hp - target.hp - 100) < 0.01f, "Area damage cannot trigger direct-hit critical artifacts");
        Equip(11); ForceRollBelow(0.02f); target.TakeProjectileDamage(1, 0, CombatDamageSource.Firefly);
        Require(target.GetComponent<EnemyTurnStun>()?.RemainingTurns == 1, "Stun proc applies one enemy turn");
        Equip(14); var nearby = new List<Brick>();
        nearby.Add(Target(new Vector2(-1, 0), 1000)); nearby.Add(Target(new Vector2(1, 0), 1000));
        nearby.Add(Target(new Vector2(0, 1), 1000)); nearby.Add(Target(new Vector2(0, -1), 1000));
        ProjectileDamageStats.BeginLevel(); ForceRollBelow(0.03f); target.TakeProjectileDamage(10, 0, CombatDamageSource.Firefly);
        Require(nearby.Count(e => Mathf.Abs(e.hp - 975) < 0.01f) == 3
            && Math.Abs(ProjectileDamageStats.TotalDamage - 85) < 0.01, "Lightning damages at most three neighbours, attributes once, never recursively procs");
        Require(Object.FindObjectsOfType<ArtifactLightningEffect>().Length == 3,
            "Three short lightning beams use the authored effect prefab");
        foreach (Brick enemy in nearby) Object.Destroy(enemy.gameObject); yield return Frames(2);

        Equip(31);
        var stage = room.GetComponent<Stage>();
        var barricade = stage.gameObject.AddComponent<MainRoomBarricade>(); barricade.Initialize(stage, 100 + MetaProgression.BarricadeHealthBonus);
        barricade.TakeDamage(100);
        Require(Mathf.Abs(barricade.MaxHealth - 200) < 0.01f && Mathf.Abs(barricade.CurrentHealth - 112) < 0.01f,
            "Barricade starts with bonus health and mitigates incoming damage");
        barricade.RestoreHealth(1000); barricade.AddBeaverHealth(40); barricade.RestoreHealth(1000);
        Require(barricade.CurrentHealth == 240, "Ordinary healing caps at maximum and preserves beaver overhealth");
        var dam = new GameObject("Test Dam", typeof(BeaverDam)).GetComponent<BeaverDam>(); dam.AddHealth(100, stage);
        float damHealthBeforeHit = dam.Health;
        float rest = dam.Absorb(200);
        Require(Mathf.Abs(rest - (200 - damHealthBeforeHit / 0.88f)) < 0.01f, "Dam mitigation returns unmitigated overflow for the next defense");
        Equip(57);
        var beaver = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Beaver.prefab")).GetComponent<BeaverProjectile>();
        beaver.ConfigureRunUpgrades(0, 0, false);
        Require(Mathf.Abs((float)Get(beaver, "damConversion") - 0.01f) < 0.0001f, "Guardian item does not change the beaver intrinsic conversion");
        Object.Destroy(beaver.gameObject);
        Equip(56);
        var seedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/SquirrelAcorn.prefab").GetComponent<SquirrelAcorn>();
        var seed = SquirrelAcorn.Plant(target, seedPrefab, 0.05f, 0, false, 0);
        Require(Mathf.Abs(seed.EarlyExplosionChance - 0.05f) < 0.0001f && Mathf.Abs(SquirrelAcorn.BlastRadius - 1.35f) < 0.0001f,
            "Seed radius is shared while native squirrel upgrades remain unchanged");
        Object.Destroy(seed.gameObject);
        Equip(46);
        var frogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/Frog.prefab").GetComponent<FrogProjectile>().RiderPrefab;
        for (int i = 0; i < 2; i++)
        {
            var frog = Object.Instantiate(frogPrefab, target.transform.position, Quaternion.identity, room.transform);
            frog.Initialize(target, 15, 2, 3, false, 0);
            Require(Mathf.Abs(frog.JumpRadius - 2f) < 0.0001f, "Pond item does not change native frog jump radius");
        }
        yield return Frames(8);
        Require(Mathf.Abs(FrogRider.DamageMultiplier(room) - 1.06f) < 0.0001f, "Only intrinsic frog chorus grants per-rider room damage");
        Brick far = Target(new Vector2(6, 0), 1000); ProjectileDamageStats.BeginLevel();
        far.TakeProjectileDamage(100, 0, CombatDamageSource.Firefly);
        Require(Math.Abs(ProjectileDamageStats.ForAnimal(0) - 104) < 0.001 && Math.Abs(ProjectileDamageStats.ForAnimal(6) - 6.24) < 0.001
            && Math.Abs(ProjectileDamageStats.TotalDamage - 110.24) < 0.001, "Actual frog support is credited separately from generic artifact damage");
        foreach (var frog in Object.FindObjectsOfType<FrogRider>()) Object.Destroy(frog.gameObject); yield return Frames(2);
        Equip(24);
        var firefly = new GameObject("Firefly Light", typeof(FireflyImpactEffect)).GetComponent<FireflyImpactEffect>();
        Require(Mathf.Abs(firefly.GetLightRadius(1) - 1.25f) < 0.001, "Lantern expands actual firefly illumination");
        Equip(8, 9, 5);
        var shooterObject = new GameObject("Test Launcher"); shooterObject.SetActive(false);
        var shooter = shooterObject.AddComponent<Player>();
        shooter.transform.position = new Vector3(-10, -10);
        var shotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/BaseBullet.prefab");
        typeof(Player).GetMethod("LaunchProjectileSingle", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(shooter, new object[] { Vector2.up, shotPrefab, 0, 1f });
        var shot = Player.animalsCollect.Last(a => a != null);
        Require(Mathf.Abs(shot.dmg - (shotPrefab.GetComponent<Animal>().dmg + 3)) < 0.001,
            "Real launch adds flat item damage once");
        Require(Mathf.Abs(shot.Health - shotPrefab.GetComponent<Animal>().Health * 1.15f) < 0.001,
            "Real launch scales projectile health");
        Require(Mathf.Abs(shot.GetComponent<Rigidbody2D>().velocity.magnitude - 21.6f) < 0.001,
            "Real launch scales projectile speed");
        Object.Destroy(shot.gameObject); Object.Destroy(shooterObject);

        Equip(40); Require(Mathf.Abs(ArtifactEquipmentBonuses.Get(ArtifactStat.LifeStealPct) - 0.03f) < 0.001, "Lifesteal equipped value");
        var managerObject = new GameObject("Test Damage Manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<GameManager>();
        var front = managerObject.AddComponent<DefenseLineRuntime>();
        typeof(GameManager).GetProperty("Instance").SetValue(null, manager);
        typeof(GameManager).GetField("defenseLineRuntime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, front);
        typeof(DefenseLineRuntime).GetField("barricade", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(front, barricade);
        barricade.Initialize(stage, 100); barricade.TakeDamage(50); hp = barricade.CurrentHealth;
        far.TakeProjectileDamage(100, 0, CombatDamageSource.Firefly);
        Require(Mathf.Abs(barricade.CurrentHealth - hp - 3.3f) < 0.001, "Real owned damage heals barricade through lifesteal");
        typeof(GameManager).GetProperty("Instance").SetValue(null, null); Object.Destroy(managerObject);
        yield return UniversalCombatChecks();
        ClearInventory();
        Require(MetaProgression.PumpkinProjectileDamageMultiplier == 1 && ArtifactEquipmentBonuses.DefenseDamageMultiplier == 1,
            "Unequipped items immediately stop affecting combat");
    }
    private static readonly CombatDamageSource[] Sources = {
        CombatDamageSource.Firefly, CombatDamageSource.Hedgehog, CombatDamageSource.Rabbit,
        CombatDamageSource.HoneyBadger, CombatDamageSource.Boar, CombatDamageSource.Beaver,
        CombatDamageSource.Frog, CombatDamageSource.Squirrel
    };
    private static IEnumerator UniversalCombatChecks()
    {
        foreach (var enemy in room.EnemyCollection.ToArray()) if (enemy != null) Object.Destroy(enemy);
        room.EnemyCollection.Clear(); yield return Frames(2);
        var stage = room.GetComponent<Stage>();
        var barricade = stage.GetComponent<MainRoomBarricade>();
        var target = Target(Vector2.zero, 100000);
        Equip(57); barricade.Initialize(stage, 100);
        for (int owner = 0; owner < 8; owner++)
        {
            float before = barricade.CurrentHealth;
            target.TakeProjectileDamage(100, 0, Sources[owner], owner);
            Require(Mathf.Abs(barricade.CurrentHealth - before - 0.2f) < 0.001f, "Guardian reinforcement from projectile " + owner);
        }
        Require(stage.GetComponentInChildren<BeaverDam>() == null && barricade.CurrentHealth > barricade.MaxHealth, "Main room uses barricade overhealth, never creates a dam");
        float shield = barricade.CurrentHealth;
        target.TakeAfterTurnDamage(100, CombatDamageSource.Fire, 2);
        typeof(Brick).GetField("immuneToPoison", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, false);
        target.TakeAfterTurnDamage(100, CombatDamageSource.Poison, 2);
        target.TakeAfterTurnDamage(100, CombatDamageSource.Fire);
        Require(Mathf.Abs(barricade.CurrentHealth - shield - 0.4f) < 0.001f, "Owned fire and poison fortify; unknown damage does not");
        var weak = Target(new Vector2(7, 0), 5); shield = barricade.CurrentHealth;
        weak.TakeProjectileDamage(1000, 0, CombatDamageSource.Firefly);
        Require(Mathf.Abs(barricade.CurrentHealth - shield - 0.01f) < 0.001f, "Fortification counts actual health lost rather than overkill");
        typeof(Stage).GetField("defenseLine", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stage, 2);
        target.TakeProjectileDamage(100, 0, CombatDamageSource.Rabbit);
        var dam = stage.GetComponentInChildren<BeaverDam>();
        Require(dam != null && Mathf.Abs(dam.Health - 0.2f) < 0.001f && dam.GetComponent<SpriteRenderer>().sprite != null, "Non-beaver creates an authored room dam");
        target.TakeProjectileDamage(100, 0, CombatDamageSource.Squirrel);
        Require(Mathf.Abs(dam.Health - 0.4f) < 0.001f && stage.GetComponentsInChildren<BeaverDam>().Length == 1, "All projectiles strengthen the same dam");
        Object.Destroy(dam.gameObject);
        typeof(Stage).GetField("defenseLine", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stage, 1);

        Equip(20);
        for (int owner = 0; owner < 8; owner++)
        {
            float hp = target.hp; ProjectileDamageStats.BeginLevel(); ForceRollBelow(0.04f);
            target.TakeProjectileDamage(100, 0, Sources[owner], owner);
            Require(Mathf.Abs(hp - target.hp - 125f) < 0.01f && Math.Abs(ProjectileDamageStats.ForAnimal(owner) - 125) < 0.01,
                "Repeat hit from projectile " + owner + " with correct damage credit");
        }
        var neighbour = Target(new Vector2(0.75f, 0), 100000);
        var outside = Target(new Vector2(4, 0), 100000);
        Equip(17);
        for (int owner = 0; owner < 8; owner++)
        {
            float hp = neighbour.hp; float farHP = outside.hp; ProjectileDamageStats.BeginLevel(); ForceRollBelow(0.08f);
            target.TakeProjectileDamage(100, 0, Sources[owner], owner);
            Require(Mathf.Abs(hp - neighbour.hp - 20) < 0.01f && outside.hp == farHP
                && Math.Abs(ProjectileDamageStats.ForAnimal(owner) - 120) < 0.01, "Thorns from projectile " + owner + " damage only neighbours");
        }
        Require(Object.FindObjectsOfType<HedgehogExplosionPulse>().Length > 0, "Universal thorns show an existing short impact effect");
        neighbour.transform.position = new Vector3(2.2f, 0, 0); Equip(22);
        for (int owner = 0; owner < 8; owner++)
        {
            float hp = neighbour.hp; ProjectileDamageStats.BeginLevel(); ForceRollBelow(0.04f);
            target.TakeProjectileDamage(100, 0, Sources[owner], owner);
            Require(Mathf.Abs(hp - neighbour.hp - 25) < 0.01f && Math.Abs(ProjectileDamageStats.ForAnimal(owner) - 125) < 0.01,
                "Pond chain from projectile " + owner + " uses shared range and correct credit");
        }
        Require(Object.FindObjectsOfType<ArtifactLightningEffect>().Length > 0, "Pond chain has an authored arc effect");
        Equip(16); var status = target.GetComponent<EnemyStatusEffects>();
        if (status != null) status.ClearAll();
        ProjectileDamageStats.BeginLevel();
        for (int owner = 0; owner < 8; owner++)
        {
            target.TakeProjectileDamage(100, 0, Sources[owner], owner);
            status = target.GetComponent<EnemyStatusEffects>();
            Require(Mathf.Abs(status.GetPeriodicDamage("artifact_poison_" + owner) - 8.8f) < 0.001f, "Poison planted by projectile " + owner);
        }
        float poisonedHP = target.hp;
        EnemyStatusEffects.ProcessAllAfterEnemyTurn(1000);
        Require(Mathf.Abs(poisonedHP - target.hp - 70.4f) < 0.04f && status.ActivePeriodicDamageCount == 0, "Artifact poison ticks once on next turn without multiplying its bonuses twice");
        for (int owner = 0; owner < 8; owner++)
            Require(Math.Abs(ProjectileDamageStats.ForAnimal(owner) - 108.8) < 0.02, "Poison ownership preserved for projectile " + owner);

        neighbour.transform.position = new Vector3(0.75f, 0, 0);
        Equip(20, 17); float beforeHost = target.hp; float beforeNeighbour = neighbour.hp;
        ForceRollsBelow(0.04f, 0.08f); target.TakeProjectileDamage(100, 0, CombatDamageSource.Firefly);
        Require(Mathf.Abs(beforeHost - target.hp - 125) < 0.01f && Mathf.Abs(beforeNeighbour - neighbour.hp - 20) < 0.01f,
            "Repeat and splash cannot recursively trigger random artifact effects");
        Equip(23);
        for (int owner = 0; owner < 8; owner++)
        {
            var host = Target(new Vector2(10 + 4 * owner, 0), 500);
            var close = Target(new Vector2(10.75f + 4 * owner, 0), 1000);
            ProjectileDamageStats.BeginLevel(); ForceRollBelow(0.015f);
            host.TakeProjectileDamage(100, 0, Sources[owner], owner);
            var seed = SquirrelAcorn.FindOn(host);
            Require(seed != null && seed.IsArtifactSeed && seed.DamageOwner == owner && seed.GetComponentInChildren<SpriteRenderer>().sprite != null,
                "Projectile " + owner + " plants an authored acorn without squirrel");
            host.TakeProjectileDamage(500, 0, Sources[owner], owner);
            yield return WaitForAcornBlasts();
            Require(Mathf.Abs(close.hp - 900) < 0.01f && Math.Abs(ProjectileDamageStats.ForAnimal(owner) - 600) < 0.01,
                "Death acorn explodes for 100 fire damage and credits projectile " + owner);
        }
        var living = Target(new Vector2(50, 0), 1000); ForceRollBelow(0.015f);
        living.TakeProjectileDamage(10, 0, CombatDamageSource.Firefly); yield return Frames(1);
        ForceRollBelow(0.01f); living.TakeProjectileDamage(20, 0, CombatDamageSource.Rabbit); yield return WaitForAcornBlasts();
        Require(SquirrelAcorn.FindOn(living) == null && Mathf.Abs(living.hp - 870) < 0.01f, "Any projectile can detonate a universal acorn early");
        var untouched = Target(new Vector2(55, 0), 1000); ForceRollBelow(0.015f);
        untouched.TakeAfterTurnDamage(100, CombatDamageSource.Fire, 0);
        Require(SquirrelAcorn.FindOn(untouched) == null, "Periodic fire cannot plant acorns");
        Equip(24, 4);
        var light = new GameObject("Preserved Firefly", typeof(FireflyImpactEffect)).GetComponent<FireflyImpactEffect>();
        Require(Mathf.Abs(light.GetLightRadius(1) - 1.25f) < 0.001f && Mathf.Abs(MetaProgression.FireflyBurnDamageMultiplier - 1.2f) < 0.001f,
            "Firefly illumination and burn efficiency remain unchanged");
        Object.Destroy(light.gameObject);
    }
    private static void ForceRollAbove(float chance)
    {
        for (int seed = 0; seed < 100000; seed++) { UnityEngine.Random.InitState(seed); if (UnityEngine.Random.value > chance) { UnityEngine.Random.InitState(seed); return; } }
        throw new Exception("No deterministic seed found.");
    }
    private static void ForceRollsBelow(params float[] chances)
    {
        for (int seed = 0; seed < 100000; seed++)
        {
            UnityEngine.Random.InitState(seed); bool valid = true;
            foreach (float chance in chances) if (UnityEngine.Random.value >= chance) { valid = false; break; }
            if (valid) { UnityEngine.Random.InitState(seed); return; }
        }
        throw new Exception("No deterministic sequence found.");
    }
    private static Brick Target(Vector2 position, float hp)
    {
        var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/SwampDemon.prefab"),
            position, Quaternion.identity, room.transform).GetComponent<Brick>();
        enemy.levelGenerator = room; enemy.hp = hp; room.EnemyCollection.Add(enemy.gameObject); return enemy;
    }
    private static void ForceRollBelow(float chance)
    {
        for (int seed = 0; seed < 100000; seed++) { UnityEngine.Random.InitState(seed); if (UnityEngine.Random.value < chance) { UnityEngine.Random.InitState(seed); return; } }
        throw new Exception("No deterministic proc seed found.");
    }
    private static object Get(object obj, string field) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    private static IEnumerator Capture(MenuHubController hub, string path)
    {
        int width = 720, height = 1280;
        Screen.SetResolution(width, height, false);
        var camera = new GameObject("UI Capture", typeof(Camera)).GetComponent<Camera>(); camera.backgroundColor = VillageUIStyle.Background;
        var canvas = hub.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindObjectsOfType<Canvas>().OrderByDescending(c => c.sortingOrder).First();
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        var render = new RenderTexture(width, height, 24); render.Create(); camera.targetTexture = render;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        yield return Frames(5); Canvas.ForceUpdateCanvases(); camera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
        RenderTexture.active = previous; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
        Object.Destroy(texture); render.Release(); Object.Destroy(render); Object.Destroy(camera.gameObject);
    }
    private static IEnumerator Click(Button button)
    {
        yield return Frames(2);
        yield return ClickAfterRender(button);
    }
    private static IEnumerator ClickAfterRender(Button button)
    {
        Require(button != null && button.gameObject.activeInHierarchy && button.interactable, "Button ready: " + button?.name);
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "Pointer reaches " + button.name + " at " + point + "; top: " + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        yield return Frames(2);
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator WaitForAcornBlasts()
    {
        float limit = Time.time + 3f;
        while (SquirrelAcorn.HasActiveBlasts && Time.time < limit) yield return null;
        Require(!SquirrelAcorn.HasActiveBlasts, "Universal acorn finishes all three timed damage hits");
    }
    private static void Require(bool okay, string message) { if (!okay) throw new Exception(message); Report.Add("PASS " + message); }
    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Artifact validation timed out");
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
        Report.Add(error == null ? "ARTIFACT_SHOP_PASSED" : "FAIL " + error);
        File.WriteAllLines("Logs/ArtifactShopValidation.txt", Report);
        if (error != null) Debug.LogException(error); else Debug.Log("ARTIFACT_SHOP_PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
