using System;
using System.Globalization;
using UnityEngine;

public struct PassiveIncomeResult
{
    public double Minutes;
    public double Acorns;
    public double Gold;

    public bool HasIncome => Acorns > 0.000001d || Gold > 0.000001d;
}

public static class MetaProgression
{
    public const int EquipmentSlotCount = 6;
    public const int MaxVillageBuildingLevel = 10;
    public const double PassiveStorageMinutes = 8d * 60d;
    public const double GreatTreeBaseAcornsPerMinute = 0.02d;
    public const double ForestShopBaseGoldPerMinute = 0.1d;
    public static int ArtifactCount => ArtifactCatalog.Length;
    public const int OrdinaryArtifactCount = 30;
    public const int ArtifactSpinCost = 50;

    private const string AcornsKey = "ForestAnimalWar.Meta.Acorns";
    private const string GoldKey = "ForestAnimalWar.Meta.Gold";
    private const string SeedsKey = "ForestAnimalWar.Meta.Seeds";
    private const string DiamondsKey = "ForestAnimalWar.Meta.Diamonds";
    private const string AcornFractionKey = "ForestAnimalWar.Village.AcornFraction";
    private const string GoldFractionKey = "ForestAnimalWar.Village.GoldFraction";
    private const string GreatTreeLevelKey = "ForestAnimalWar.Village.GreatTreeLevel";
    private const string ForestShopLevelKey = "ForestAnimalWar.Village.ForestShopLevel";
    private const string PassiveTimestampKey = "ForestAnimalWar.Village.PassiveTimestampUtc";
    private const string PassiveStoredMinutesKey = "ForestAnimalWar.Village.PassiveStoredMinutes";
    private const string PendingAcornsKey = "ForestAnimalWar.Village.PendingAcorns";
    private const string PendingGoldKey = "ForestAnimalWar.Village.PendingGold";
    private const string ArtifactKeyPrefix = "ForestAnimalWar.Village.Artifact.";
    private const string VictoriesKey = "ForestAnimalWar.Meta.Victories";
    private const string EventClaimKey = "ForestAnimalWar.Meta.Event.";
    private const string EquipmentSlotKey = "ForestAnimalWar.Hero.EquipmentSlot.";

    private static MetaArtifactCatalog catalog;
    private static MetaArtifactDefinition[] ArtifactCatalog
    {
        get
        {
            if (catalog == null) catalog = Resources.Load<MetaArtifactCatalog>("Artifacts/ArtifactCatalog");
            return catalog != null && catalog.Items != null ? catalog.Items : Array.Empty<MetaArtifactDefinition>();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetArtifacts()
    {
        catalog = null;
        ArtifactEquipmentBonuses.Invalidate();
    }

    private static void NotifyChanged()
    {
        ArtifactEquipmentBonuses.Invalidate();
        Changed?.Invoke();
    }

    public static event Action Changed;

    public static int Acorns => Mathf.Max(0, PlayerPrefs.GetInt(AcornsKey, 0));
    public static int Gold => Mathf.Max(0, PlayerPrefs.GetInt(GoldKey, 0));
    public static int Seeds => Mathf.Max(0, PlayerPrefs.GetInt(SeedsKey, 0));
    public static int Diamonds => Mathf.Max(0, PlayerPrefs.GetInt(DiamondsKey, 0));
#if UNITY_EDITOR
    // Local editor tools must update PlayerPrefs through Unity, which caches registry values.
    public static void SetEditorDiamondBalance(int balance)
    {
        PlayerPrefs.SetInt(DiamondsKey, Mathf.Max(0, balance));
        PlayerPrefs.Save();
        NotifyChanged();
    }
#endif
    public static double AcornsExact => Acorns + ReadFraction(AcornFractionKey);
    public static double GoldExact => Gold + ReadFraction(GoldFractionKey);
    public static int GreatTreeLevel => Mathf.Clamp(PlayerPrefs.GetInt(GreatTreeLevelKey, 1), 1, MaxVillageBuildingLevel);
    public static int ForestShopLevel => Mathf.Clamp(PlayerPrefs.GetInt(ForestShopLevelKey, 1), 1, MaxVillageBuildingLevel);
    public static double GreatTreeAcornsPerMinute => GreatTreeBaseAcornsPerMinute * GreatTreeLevel * PassiveAcornMultiplier;
    public static double ForestShopGoldPerMinute => ForestShopBaseGoldPerMinute * ForestShopLevel * PassiveGoldMultiplier;
    public static int TotalVictories => Mathf.Max(0, PlayerPrefs.GetInt(VictoriesKey, 0));

    public static float PumpkinProjectileDamageMultiplier => ArtifactEquipmentBonuses.Multiplier(ArtifactStat.DamagePct);
    public static float BarricadeHealthBonus => ArtifactEquipmentBonuses.Get(ArtifactStat.BarricadeHealthFlat);
    public static double PassiveGoldMultiplier => 1d + Math.Round(ArtifactEquipmentBonuses.Get(ArtifactStat.PassiveGoldPct), 6);
    public static double PassiveAcornMultiplier => 1d + Math.Round(ArtifactEquipmentBonuses.Get(ArtifactStat.PassiveAcornsPct), 6);
    public static float FireflyBurnDamageMultiplier => ArtifactEquipmentBonuses.Multiplier(ArtifactStat.FireflyBurnPct);
    public static float PumpkinProjectileHealthMultiplier => ArtifactEquipmentBonuses.Multiplier(ArtifactStat.ProjectileHealthPct);
    public static double RoomGoldMultiplier => 1d + Math.Round(ArtifactEquipmentBonuses.Get(ArtifactStat.RoomGoldPct), 6);
    public static float VictoryAcornMultiplier => ArtifactEquipmentBonuses.Multiplier(ArtifactStat.VictoryAcornsPct);

    public static MetaArtifactDefinition GetArtifactDefinition(int index)
    {
        return index >= 0 && index < ArtifactCatalog.Length ? ArtifactCatalog[index] : null;
    }

    public static bool IsArtifactOwned(int index)
    {
        MetaArtifactDefinition definition = GetArtifactDefinition(index);
        return definition != null && PlayerPrefs.GetInt(ArtifactKeyPrefix + definition.Id, 0) > 0;
    }


    // Ownership counts are separate; retired equipment returns to the bag.
    public static void NormalizeEquipmentCapacity()
    {
        bool changed = false;
        for (int slot = EquipmentSlotCount; slot < 8; slot++)
        {
            if (!PlayerPrefs.HasKey(EquipmentSlotKey + slot)) continue;
            PlayerPrefs.DeleteKey(EquipmentSlotKey + slot);
            changed = true;
        }
        if (!changed) return;
        PlayerPrefs.Save();
        NotifyChanged();
    }

    public static int GetEquippedArtifact(int slot)
    {
        if (slot < 0 || slot >= EquipmentSlotCount) return -1;
        string id = PlayerPrefs.GetString(EquipmentSlotKey + slot, string.Empty);
        for (int i = 0; i < ArtifactCount; i++)
            if (ArtifactCatalog[i].Id == id && IsArtifactOwned(i)) return i;
        return -1;
    }

    public static int GetArtifactSlot(int index)
    {
        if (index < 0 || index >= ArtifactCount) return -1;
        for (int slot = 0; slot < EquipmentSlotCount; slot++)
            if (GetEquippedArtifact(slot) == index) return slot;
        return -1;
    }

    public static bool IsArtifactEquipped(int index) => GetArtifactSlot(index) >= 0;

    public static int GetFirstEmptyEquipmentSlot()
    {
        for (int slot = 0; slot < EquipmentSlotCount; slot++)
            if (GetEquippedArtifact(slot) < 0) return slot;
        return -1;
    }

    public static bool TryEquipArtifact(int index, int slot)
    {
        if (!IsArtifactOwned(index) || slot < 0 || slot >= EquipmentSlotCount) return false;
        int oldSlot = GetArtifactSlot(index);
        if (oldSlot == slot) return false;
        // Settle the old rates before changing an income artifact. Replaced
        // artifacts remain owned and immediately become available in the bag.
        AccruePassiveIncome();
        if (oldSlot >= 0) PlayerPrefs.DeleteKey(EquipmentSlotKey + oldSlot);
        PlayerPrefs.SetString(EquipmentSlotKey + slot, ArtifactCatalog[index].Id);
        PlayerPrefs.Save();
        NotifyChanged();
        return true;
    }

    public static bool TryUnequipArtifact(int slot)
    {
        if (GetEquippedArtifact(slot) < 0) return false;
        AccruePassiveIncome();
        PlayerPrefs.DeleteKey(EquipmentSlotKey + slot);
        PlayerPrefs.Save();
        NotifyChanged();
        return true;
    }

    public static int UnownedArtifactCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < ArtifactCount; i++)
                if (!IsArtifactOwned(i)) count++;
            return count;
        }
    }

    public static int UnownedOrdinaryArtifactCount { get { int count = 0; for (int i = 0; i < ArtifactCount; i++) if (!ArtifactCatalog[i].IsAssembled && !IsArtifactOwned(i)) count++; return count; } }

    public static bool CanSpinArtifact => Gold >= ArtifactSpinCost && UnownedOrdinaryArtifactCount > 0;

    public static bool TrySpinArtifact(out int artifactIndex)
    {
        artifactIndex = -1;
        if (!CanSpinArtifact) return false;
        int pick = UnityEngine.Random.Range(0, UnownedOrdinaryArtifactCount);
        for (int i = 0; i < ArtifactCount; i++)
        {
            if (ArtifactCatalog[i].IsAssembled || IsArtifactOwned(i)) continue;
            if (pick-- != 0) continue;
            artifactIndex = i;
            break;
        }
        if (artifactIndex < 0) return false;
        PlayerPrefs.SetInt(GoldKey, Gold - ArtifactSpinCost);
        PlayerPrefs.SetInt(ArtifactKeyPrefix + ArtifactCatalog[artifactIndex].Id, 1);
        PlayerPrefs.Save();
        NotifyChanged();
        return true;
    }


    public static int FindArtifact(string id)
    {
        for (int i = 0; i < ArtifactCount; i++) if (ArtifactCatalog[i].Id == id) return i;
        return -1;
    }
    public static int OwnedArtifactAmount(int index)
    {
        var definition = GetArtifactDefinition(index);
        return definition != null ? Mathf.Clamp(PlayerPrefs.GetInt(ArtifactKeyPrefix + definition.Id, 0), 0, 999) : 0;
    }
    public static int AvailableArtifactAmount(int index)
        => Mathf.Max(0, OwnedArtifactAmount(index) - (IsArtifactEquipped(index) ? 1 : 0));

    public static bool CanAcquireArtifact(int index, out string reason)
    {
        var definition = GetArtifactDefinition(index);
        reason = "";
        if (definition == null) { reason = "Предмет не найден."; return false; }
        if (OwnedArtifactAmount(index) >= 999) { reason = "Рюкзак этого предмета заполнен."; return false; }
        if (definition.IsAssembled)
        {
            // Aggregate identical ingredients so even edited recipes cannot consume too much.
            var needed = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var ingredient in definition.Ingredients)
            {
                int component = FindArtifact(ingredient.Id);
                if (component < 0 || ingredient.Amount <= 0) { reason = "Рецепт недоступен."; return false; }
                if (!needed.ContainsKey(component)) needed[component] = 0;
                needed[component] += ingredient.Amount;
            }
            foreach (var component in needed)
            {
                if (AvailableArtifactAmount(component.Key) >= component.Value) continue;
                reason = IsArtifactEquipped(component.Key)
                    ? "Нужен компонент: " + GetArtifactDefinition(component.Key).Name + ". Сними его с героя или купи ещё один."
                    : "Не хватает компонента: " + GetArtifactDefinition(component.Key).Name + ".";
                return false;
            }
        }
        if (Gold < definition.GoldCost) { reason = "Не хватает " + (definition.GoldCost - Gold) + " золота."; return false; }
        reason = definition.IsAssembled ? "Компоненты из рюкзака будут потрачены." : "Предмет появится в рюкзаке.";
        return true;
    }

    public static bool TryAcquireArtifact(int index)
    {
        if (!CanAcquireArtifact(index, out _)) return false;
        var definition = GetArtifactDefinition(index);
        if (definition.IsAssembled)
            foreach (var ingredient in definition.Ingredients)
            {
                int component = FindArtifact(ingredient.Id);
                PlayerPrefs.SetInt(ArtifactKeyPrefix + ingredient.Id, OwnedArtifactAmount(component) - ingredient.Amount);
            }
        PlayerPrefs.SetInt(GoldKey, Gold - definition.GoldCost);
        PlayerPrefs.SetInt(ArtifactKeyPrefix + definition.Id, OwnedArtifactAmount(index) + 1);
        PlayerPrefs.Save();
        NotifyChanged();
        return true;
    }

    public static int GetArtifactTotalCost(int index)
        => TotalCost(index, new System.Collections.Generic.HashSet<int>());

    private static int TotalCost(int index, System.Collections.Generic.HashSet<int> visited)
    {
        var definition = GetArtifactDefinition(index);
        if (definition == null || !visited.Add(index)) return 0;
        int result = definition.GoldCost;
        foreach (var component in definition.Ingredients)
            result += Mathf.Max(0, component.Amount) * TotalCost(FindArtifact(component.Id), visited);
        visited.Remove(index);
        return result;
    }

    public static int GetGreatTreeUpgradeCost()
    {
        return GreatTreeLevel >= MaxVillageBuildingLevel ? 0 : 100 * GreatTreeLevel;
    }

    public static int GetForestShopUpgradeCost()
    {
        return ForestShopLevel >= MaxVillageBuildingLevel ? 0 : 10 * ForestShopLevel;
    }

    public static bool TryUpgradeGreatTree()
    {
        int cost = GetGreatTreeUpgradeCost();
        if (cost <= 0 || Gold < cost) return false;
        AccruePassiveIncome();
        PlayerPrefs.SetInt(GoldKey, Gold - cost);
        PlayerPrefs.SetInt(GreatTreeLevelKey, GreatTreeLevel + 1);
        PlayerPrefs.Save();
        NotifyChanged();
        return true;
    }

    public static bool TryUpgradeForestShop()
    {
        int cost = GetForestShopUpgradeCost();
        if (cost <= 0 || Acorns < cost) return false;
        AccruePassiveIncome();
        PlayerPrefs.SetInt(AcornsKey, Acorns - cost);
        PlayerPrefs.SetInt(ForestShopLevelKey, ForestShopLevel + 1);
        PlayerPrefs.Save();
        NotifyChanged();
        return true;
    }

    public static PassiveIncomeResult PendingPassiveIncome => new PassiveIncomeResult
    {
        Minutes = ReadStoredAmount(PassiveStoredMinutesKey, PassiveStorageMinutes),
        Acorns = ReadStoredAmount(PendingAcornsKey, double.MaxValue),
        Gold = ReadStoredAmount(PendingGoldKey, double.MaxValue)
    };

    public static PassiveIncomeResult AccruePassiveIncome()
    {
        return AccruePassiveIncomeAt(DateTime.UtcNow.Ticks);
    }

    public static PassiveIncomeResult AccruePassiveIncomeAt(long utcTicks)
    {
        var result = new PassiveIncomeResult();
        long previousTicks;
        string storedTimestamp = PlayerPrefs.GetString(PassiveTimestampKey, string.Empty);
        if (utcTicks <= 0L || !long.TryParse(storedTimestamp, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out previousTicks) || previousTicks <= 0L || previousTicks > utcTicks)
        {
            SavePassiveTimestamp(Math.Max(1L, utcTicks));
            PlayerPrefs.Save();
            return result;
        }

        double elapsedMinutes = TimeSpan.FromTicks(utcTicks - previousTicks).TotalMinutes;
        double storedMinutes = ReadStoredAmount(PassiveStoredMinutesKey, PassiveStorageMinutes);
        result.Minutes = Math.Min(Math.Max(0d, PassiveStorageMinutes - storedMinutes),
            Math.Max(0d, elapsedMinutes));
        SavePassiveTimestamp(utcTicks);
        if (result.Minutes <= 0d)
        {
            PlayerPrefs.Save();
            return result;
        }

        result.Acorns = result.Minutes * GreatTreeAcornsPerMinute;
        result.Gold = result.Minutes * ForestShopGoldPerMinute;
        WriteStoredAmount(PassiveStoredMinutesKey, storedMinutes + result.Minutes);
        WriteStoredAmount(PendingAcornsKey,
            ReadStoredAmount(PendingAcornsKey, double.MaxValue) + result.Acorns);
        WriteStoredAmount(PendingGoldKey,
            ReadStoredAmount(PendingGoldKey, double.MaxValue) + result.Gold);
        PlayerPrefs.Save();
        return result;
    }

    public static PassiveIncomeResult CollectPassiveIncome()
    {
        return CollectPassiveIncomeAt(DateTime.UtcNow.Ticks);
    }

    public static PassiveIncomeResult CollectPassiveIncomeAt(long utcTicks)
    {
        AccruePassiveIncomeAt(utcTicks);
        PassiveIncomeResult result = PendingPassiveIncome;
        if (!result.HasIncome) return result;
        AddFractionalCurrency(AcornsKey, AcornFractionKey, result.Acorns);
        AddFractionalCurrency(GoldKey, GoldFractionKey, result.Gold);
        PlayerPrefs.DeleteKey(PassiveStoredMinutesKey);
        PlayerPrefs.DeleteKey(PendingAcornsKey);
        PlayerPrefs.DeleteKey(PendingGoldKey);
        PlayerPrefs.Save();
        NotifyChanged();
        return result;
    }

    public static void AwardRoomGold(int amount)
    {
        if (amount <= 0) return;
        AddFractionalCurrency(GoldKey, GoldFractionKey, amount * RoomGoldMultiplier);
        PlayerPrefs.Save();
        NotifyChanged();
    }

    public static bool IsEventClaimed(string id) => !string.IsNullOrWhiteSpace(id)
        && PlayerPrefs.GetInt(EventClaimKey + id, 0) == 1;

    public static bool TrySpendGold(int amount)
    {
        if (amount <= 0 || Gold < amount) return false;
        PlayerPrefs.SetInt(GoldKey, Gold - amount);
        PlayerPrefs.Save();
        NotifyChanged();
        return true;
    }

    public static bool TrySpendDiamonds(int amount)
    {
        if (amount <= 0 || Diamonds < amount) return false;
        PlayerPrefs.SetInt(DiamondsKey, Diamonds - amount);
        PlayerPrefs.Save();
        NotifyChanged();
        return true;
    }

    public static bool TryClaimEvent(MenuEventDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.saveId)
            || definition.requiredVictories < 1 || definition.acornReward < 1
            || IsEventClaimed(definition.saveId) || TotalVictories < definition.requiredVictories) return false;
        PlayerPrefs.SetInt(EventClaimKey + definition.saveId, 1);
        AddWholeCurrency(AcornsKey, definition.acornReward);
        PlayerPrefs.Save();
        NotifyChanged();
        return true;
    }

    public static int AwardLevelCompletion(int levelNumber)
    {
        int baseReward = 8 + Mathf.Clamp(levelNumber, 1, LevelProgression.MaxLevel) * 2;
        int reward = Mathf.Max(1, Mathf.RoundToInt(baseReward * VictoryAcornMultiplier));
        PlayerPrefs.SetInt(VictoriesKey, TotalVictories + 1);
        AddWholeCurrency(AcornsKey, reward);
        PlayerPrefs.Save();
        NotifyChanged();
        return reward;
    }

    private static void AddWholeCurrency(string key, int amount)
    {
        int current = Mathf.Max(0, PlayerPrefs.GetInt(key, 0));
        PlayerPrefs.SetInt(key, (int)Math.Min(int.MaxValue, (long)current + Math.Max(0, amount)));
    }

    private static void AddFractionalCurrency(string wholeKey, string fractionKey, double amount)
    {
        if (amount <= 0d) return;
        int current = Mathf.Max(0, PlayerPrefs.GetInt(wholeKey, 0));
        double totalFraction = ReadFraction(fractionKey) + amount;
        long wholeGain = (long)Math.Floor(totalFraction + 0.000000001d);
        long room = int.MaxValue - (long)current;
        int appliedGain = (int)Math.Min(room, wholeGain);
        PlayerPrefs.SetInt(wholeKey, current + appliedGain);
        WriteFraction(fractionKey, appliedGain < wholeGain ? 0d : totalFraction - wholeGain);
    }

    private static double ReadFraction(string key)
    {
        double value;
        if (!double.TryParse(PlayerPrefs.GetString(key, "0"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value)) return 0d;
        return Math.Max(0d, Math.Min(0.999999999d, value));
    }

    private static void WriteFraction(string key, double value)
    {
        PlayerPrefs.SetString(key, Math.Max(0d, Math.Min(0.999999999d, value))
            .ToString("R", CultureInfo.InvariantCulture));
    }

    private static void SavePassiveTimestamp(long utcTicks)
    {
        PlayerPrefs.SetString(PassiveTimestampKey, utcTicks.ToString(CultureInfo.InvariantCulture));
    }

    private static double ReadStoredAmount(string key, double maximum)
    {
        double value;
        if (!double.TryParse(PlayerPrefs.GetString(key, "0"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) || double.IsNaN(value)) return 0d;
        return Math.Max(0d, Math.Min(maximum, value));
    }

    private static void WriteStoredAmount(string key, double value)
    {
        PlayerPrefs.SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
    }

}
