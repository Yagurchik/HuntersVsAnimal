using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MenuHubValidation
{
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        try
        {
            VillageBuildingAuthoring.Install();
            EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
            Validate();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Forest Animal War/UI/Validate menu and economy")]
    public static void Validate()
    {
        var hub = Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null) throw new InvalidOperationException("Open the menu scene first.");
        int checks = 0;
        Action<bool, string> check = (ok, message) => {
            if (!ok) throw new InvalidOperationException("Village validation: " + message);
            checks++;
        };
        var integers = new Dictionary<string, int?>();
        var strings = new Dictionary<string, string>();
        string[] integerKeys = {
            "ForestAnimalWar.Meta.Gold", "ForestAnimalWar.Meta.Acorns",
            "ForestAnimalWar.Meta.Victories", "ForestAnimalWar.Village.GreatTreeLevel",
            "ForestAnimalWar.Village.ForestShopLevel"
        };
        string[] stringKeys = {
            "ForestAnimalWar.Village.AcornFraction", "ForestAnimalWar.Village.GoldFraction",
            "ForestAnimalWar.Village.PassiveTimestampUtc",
            "ForestAnimalWar.Village.PassiveStoredMinutes",
            "ForestAnimalWar.Village.PendingAcorns", "ForestAnimalWar.Village.PendingGold"
        };
        foreach (string key in integerKeys) SaveInt(integers, key);
        for (int i = 0; i < MetaProgression.ArtifactCount; i++)
            SaveInt(integers, "ForestAnimalWar.Village.Artifact." + MetaProgression.GetArtifactDefinition(i).Id);
        foreach (string key in stringKeys) SaveString(strings, key);
        for (int i = 0; i < MetaProgression.EquipmentSlotCount; i++)
            SaveString(strings, "ForestAnimalWar.Hero.EquipmentSlot." + i);
        int originalTab = hub.SelectedTab;
        try
        {
            foreach (string key in integers.Keys) PlayerPrefs.DeleteKey(key);
            foreach (string key in strings.Keys) PlayerPrefs.DeleteKey(key);
            long start = DateTime.UtcNow.Ticks;
            check(!MetaProgression.AccruePassiveIncomeAt(start).HasIncome, "No retroactive first-login reward");
            PassiveIncomeResult capped = MetaProgression.AccruePassiveIncomeAt(start + TimeSpan.FromHours(20).Ticks);
            check(Math.Abs(capped.Minutes - 480d) < 0.00001d, "Eight hour cap");
            check(MetaProgression.AcornsExact == 0d && MetaProgression.GoldExact == 0d,
                "Income waits for a manual collection");
            check(Math.Abs(MetaProgression.PendingPassiveIncome.Acorns - 9.6d) < 0.00001d
                && Math.Abs(MetaProgression.PendingPassiveIncome.Gold - 48d) < 0.00001d,
                "Both buildings accumulate exact base rates");
            PassiveIncomeResult collected = MetaProgression.CollectPassiveIncomeAt(start + TimeSpan.FromHours(20).Ticks);
            check(Math.Abs(collected.Acorns - 9.6d) < 0.00001d
                && Math.Abs(MetaProgression.AcornsExact - 9.6d) < 0.00001d
                && Math.Abs(MetaProgression.GoldExact - 48d) < 0.00001d,
                "Tree collection transfers both currencies");
            check(!MetaProgression.CollectPassiveIncomeAt(start + TimeSpan.FromHours(20).Ticks).HasIncome,
                "Repeated collection cannot duplicate income");
            PlayerPrefs.SetString(stringKeys[2], DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.SetInt(integerKeys[0], 100);
            check(MetaProgression.TryUpgradeGreatTree() && MetaProgression.GreatTreeLevel == 2
                && MetaProgression.Gold == 0, "Tree costs gold");
            PlayerPrefs.SetInt(integerKeys[1], 10);
            check(MetaProgression.TryUpgradeForestShop() && MetaProgression.ForestShopLevel == 2
                && MetaProgression.Acorns == 0, "Shop costs acorns");
            PlayerPrefs.SetInt(integerKeys[3], MetaProgression.MaxVillageBuildingLevel);
            PlayerPrefs.SetInt(integerKeys[4], MetaProgression.MaxVillageBuildingLevel);
            check(!MetaProgression.TryUpgradeGreatTree() && !MetaProgression.TryUpgradeForestShop(),
                "Building level caps");
            int failedSpin;
            check(!MetaProgression.TrySpinArtifact(out failedSpin) && failedSpin == -1,
                "Cannot spin without enough gold");
            check(!MetaProgression.TryEquipArtifact(0, 0), "Cannot equip an unowned item");
            PlayerPrefs.SetInt(integerKeys[0], 10000);
            for (int i = 0; i < MetaProgression.OrdinaryArtifactCount; i++)
            {
                int before = MetaProgression.Gold;
                int unowned = MetaProgression.UnownedArtifactCount;
                int result;
                check(MetaProgression.TrySpinArtifact(out result), "Spin " + i);
                check(result >= 0 && MetaProgression.IsArtifactOwned(result)
                    && MetaProgression.UnownedArtifactCount == unowned - 1,
                    "Spin awards a new artifact " + i);
                check(MetaProgression.Gold == before - MetaProgression.ArtifactSpinCost,
                    "Fixed spin price " + i);
                check(!MetaProgression.IsArtifactEquipped(result), "Spin result stays in bag " + i);
            }
            int exhaustedSpin;
            check(!MetaProgression.TrySpinArtifact(out exhaustedSpin) && exhaustedSpin == -1,
                "Roulette stops when all artifacts are owned");
            check(Mathf.Approximately(MetaProgression.PumpkinProjectileDamageMultiplier, 1f)
                && MetaProgression.PassiveAcornMultiplier == 1d && MetaProgression.BarricadeHealthBonus == 0f,
                "Owned but unequipped items give no bonuses");
            check(MetaProgression.TryEquipArtifact(0, 0)
                && Mathf.Approximately(MetaProgression.PumpkinProjectileDamageMultiplier, 1.05f),
                "Equipping activates the damage bonus");
            check(!MetaProgression.TryEquipArtifact(0, 0) && !MetaProgression.TryEquipArtifact(0, 6),
                "Duplicate equip and invalid slots rejected");
            check(MetaProgression.TryEquipArtifact(0, 1) && MetaProgression.GetEquippedArtifact(0) == -1
                && MetaProgression.GetEquippedArtifact(1) == 0, "Move does not duplicate an item");
            check(MetaProgression.TryEquipArtifact(1, 1) && !MetaProgression.IsArtifactEquipped(0)
                && MetaProgression.IsArtifactOwned(0) && MetaProgression.BarricadeHealthBonus == 25f,
                "Replacement returns the old item to the bag");
            check(MetaProgression.TryUnequipArtifact(1) && MetaProgression.BarricadeHealthBonus == 0f
                && MetaProgression.IsArtifactOwned(1), "Unequip preserves ownership and disables bonus");
            for (int i = 0; i < MetaProgression.EquipmentSlotCount; i++)
                check(MetaProgression.TryEquipArtifact(i, i), "Equip slot " + i);
            check(MetaProgression.GetFirstEmptyEquipmentSlot() == -1, "All six slots filled");
            check(MetaProgression.PassiveGoldMultiplier == 1.15d && MetaProgression.PassiveAcornMultiplier == 1.15d
                && Mathf.Approximately(MetaProgression.FireflyBurnDamageMultiplier, 1.2f)
                && Mathf.Approximately(MetaProgression.PumpkinProjectileHealthMultiplier, 1.15f)
                && MetaProgression.RoomGoldMultiplier == 1d
                && Mathf.Approximately(MetaProgression.VictoryAcornMultiplier, 1f), "All effects follow equipment");
            check(PlayerPrefs.GetString("ForestAnimalWar.Hero.EquipmentSlot.0") == "sun_lens",
                "Equipment persists using stable artifact IDs");
            // Verify old income is settled before removing the income item.
            PlayerPrefs.SetInt(integerKeys[4], 1);
            PlayerPrefs.SetInt(integerKeys[0], 0);
            PlayerPrefs.DeleteKey(stringKeys[1]);
            PlayerPrefs.SetString(stringKeys[2], (DateTime.UtcNow - TimeSpan.FromMinutes(60)).Ticks.ToString());
            check(MetaProgression.TryUnequipArtifact(2)
                && Math.Abs(MetaProgression.PendingPassiveIncome.Gold - 6.9d) < 0.01d
                && MetaProgression.GoldExact == 0d,
                "Removing an income item preserves the preceding hour without auto-collecting");
            for (int i = 0; i < MetaProgression.EquipmentSlotCount; i++)
                PlayerPrefs.DeleteKey("ForestAnimalWar.Hero.EquipmentSlot." + i);

            hub.PrepareVillageAndHero();
            hub.Refresh();
            Transform resourceBar = hub.transform.Find("Safe Area/Portrait Frame/Header/Resource Bar");
            check(resourceBar != null, "Four-resource header is authored in UIscene");
            string[] resourceIds = { "Acorns", "Gold", "Seeds", "Diamonds" };
            for (int i = 0; i < resourceIds.Length; i++)
            {
                Transform badge = resourceBar.Find(resourceIds[i]);
                check(badge != null && badge.Find("Icon")?.GetComponent<ResourceIconGraphic>() != null
                    && badge.Find("Amount")?.GetComponent<Text>() != null,
                    "Resource icon and amount " + resourceIds[i]);
            }
            for (int i = 0; i < hub.tabs.Length; i++)
            {
                hub.SelectTab(i);
                for (int j = 0; j < hub.tabs.Length; j++)
                    check(hub.tabs[j].page.activeSelf == (i == j), "Page visibility " + i + "/" + j);
            }
            hub.SelectTab(2);
            Transform field = hub.tabs[2].page.transform.Find("Forest Village Runtime/Viewport/Content/Village Field");
            check(field != null && field.GetComponentsInChildren<RawImage>(true).Length == 0
                && field.childCount == 4, "Four entity windows replace the clearing and building map");
            foreach (Transform entity in field)
                check(entity.GetComponent<Button>() != null
                    && entity.Find("Character")?.GetComponent<Image>()?.sprite != null,
                    "Editable character portrait and service button " + entity.name);
            VillageBuildingView[] buildings = field.GetComponentsInChildren<VillageBuildingView>();
            check(buildings.Length == 2, "Exactly two independent building instances");
            foreach (VillageBuildingView building in buildings)
            {
                check(building.artwork.sprite != null, "Building sprite loads " + building.kind);
                check(PrefabUtility.IsPartOfPrefabAsset(Resources.Load<GameObject>("Village/"
                    + (building.kind == VillageBuildingKind.GreatTree ? "GreatTreeBuilding" : "ForestShopBuilding"))),
                    "Real prefab asset " + building.kind);
                building.openButton.onClick.Invoke();
                check(hub.IsDialogOpen, "Building click opens its menu " + building.kind);
                hub.CloseDialogs();
                check(!hub.IsDialogOpen, "Building dialog closes");
            }
            Transform villageMenu = hub.tabs[2].page.transform.Find("Building Menu/Panel");
            check(villageMenu.Find("Collect Income") != null, "Tree collect button is authored in UIscene");
            check(villageMenu.Find("Shop Offers/Spin Button") != null,
                "Artifact roulette is authored in UIscene");
            check(villageMenu.Find("Shop Offers/Offer sun_lens/Buy") == null,
                "No targeted artifact purchases remain");
            hub.SelectTab(1);
            Transform hero = hub.tabs[1].page.transform.Find("Hero Equipment Runtime");
            check(hero != null && hub.tabs[1].buttonLabel.text == "АРТЕФАКТЫ", "Hero replaces animal tab");
            Image portrait = hero.Find("Viewport/Content/Hero Stage/Pumpkin Portrait").GetComponent<Image>();
            check(portrait.sprite != null, "Actual pumpkin sprite is visible");
            RectTransform stageRect = (RectTransform)hero.Find("Viewport/Content/Hero Stage");
            check(Mathf.Approximately(stageRect.anchorMin.x, 0.5f)
                && Mathf.Approximately(stageRect.anchoredPosition.x, 0f)
                && Mathf.Approximately(portrait.rectTransform.anchorMin.x, 0.5f)
                && Mathf.Approximately(portrait.rectTransform.anchoredPosition.x, 0f),
                "Pumpkin portrait stays centered at different aspect ratios");
            for (int i = 0; i < MetaProgression.EquipmentSlotCount; i++)
            {
                var cell = hero.Find("Viewport/Content/Equipment Slot " + (i + 1)) as RectTransform;
                check(cell != null && Mathf.Approximately(cell.rect.width, cell.rect.height),
                    "Square equipment cell " + i);
            }
            for (int i = 0; i < 16; i++)
            {
                var cell = hero.Find("Viewport/Content/Backpack Cell " + (i + 1)) as RectTransform;
                check(cell != null && Mathf.Approximately(cell.rect.width, cell.rect.height),
                    "Square backpack cell " + i);
            }
            foreach (Graphic graphic in hero.GetComponentsInChildren<Graphic>(true))
                check(graphic.GetComponent<CanvasRenderer>() != null, "Renderer " + graphic.name);
        }
        finally
        {
            foreach (var item in integers)
                if (item.Value.HasValue) PlayerPrefs.SetInt(item.Key, item.Value.Value);
                else PlayerPrefs.DeleteKey(item.Key);
            foreach (var item in strings)
                if (item.Value != null) PlayerPrefs.SetString(item.Key, item.Value);
                else PlayerPrefs.DeleteKey(item.Key);
            PlayerPrefs.Save();
            ArtifactEquipmentBonuses.Invalidate();
            hub.CloseDialogs();
            hub.SelectTab(originalTab);
            hub.Refresh();
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/VillageEquipmentValidation.txt", "PASS: " + checks
            + " checks. Original progress restored.");
        Debug.Log("PASS: village and equipment, " + checks + " checks.");
    }

    private static void SaveInt(Dictionary<string, int?> saved, string key)
        => saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
    private static void SaveString(Dictionary<string, string> saved, string key)
        => saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
}
