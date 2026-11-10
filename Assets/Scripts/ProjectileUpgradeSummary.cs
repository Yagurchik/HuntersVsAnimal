using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Read the current run state; this view never purchases or applies upgrades.
public static class ProjectileUpgradeSummary
{
    public sealed class Item
    {
        public string Title;
        public string Description;
        public int Rank;
        public int MaximumRank;
        public bool Ultimate;
    }

    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private static string Number(float value) => value.ToString("0.#", Russian);
    private static string Cells(float value)
    {
        int whole = Mathf.RoundToInt(value);
        if (!Mathf.Approximately(value, whole))
        {
            return Number(value) + " клетки";
        }

        int lastTwo = whole % 100, last = whole % 10;
        string word = lastTwo >= 11 && lastTwo <= 14 ? "клеток"
            : last == 1 ? "клетка" : last >= 2 && last <= 4 ? "клетки" : "клеток";
        return Number(value) + " " + word;
    }

    public static List<Item> ForAnimal(AnimalProjectileQueue queue, int index)
    {
        var result = new List<Item>(5);
        if (queue == null || index < 0 || index >= queue.Animals.Count || !queue.IsAvailable(index))
        {
            return result;
        }

        var entry = queue.Animals[index];
        if (index != AnimalProjectileQueue.FireflyIndex && index != AnimalProjectileQueue.RabbitIndex
            && entry.AmmoUpgradeLevel > 1)
        {
            Add(result, "Количество", "Снарядов за залп: " + entry.AmmoPerTurn, entry.AmmoUpgradeLevel);
        }
        else if (entry.UpgradeLevel > 0 && index != AnimalProjectileQueue.FireflyIndex && index != queue.StartingAnimalIndex)
        {
            Add(result, "Снаряд открыт", "Снарядов за залп: " + entry.AmmoPerTurn, 1, 1);
        }

        switch (index)
        {
            case AnimalProjectileQueue.FireflyIndex:
                Add(result, "Повторный залп", "Залпов за ход: " + queue.GetManualVolleyCount(index)
                    + ". Урон каждого залпа: " + Number(queue.GetManualVolleyDamageMultiplier(index) * 100f) + "%.", entry.RepeatFireUpgradeLevel);
                Add(result, "Горение", "Урон одного стака за ход: " + Number(queue.GetBurnDamage(index))
                    + ". После урона за ход исчезает один стак.", entry.BurnUpgradeLevel);
                Ultimate(result, "Вечный огонь", "Стаки горения не исчезают. Количество стаков не ограничено.", entry.HasFireflyUltimate);
                break;
            case AnimalProjectileQueue.HedgehogIndex:
                Add(result, "Радиус взрыва", "Радиус: " + Cells(queue.GetExplosionRadius(index)) + ".", entry.HedgehogRadiusUpgradeLevel);
                Add(result, "Острые иголки", "За каждый взрыв в этом ходу урон по врагу растёт на "
                    + Number(queue.GetHedgehogQuillDamageGrowthPerHit(index)) + ".", entry.HedgehogQuillDamageUpgradeLevel);
                Ultimate(result, "Двойной залп", "Два взрыва при столкновении. Второй — через 0,09 секунды.", entry.HasHedgehogUltimate);
                break;
            case AnimalProjectileQueue.RabbitIndex:
                Add(result, "Сила стаи", "Урон за каждого живого кролика: +" + Number(queue.GetRabbitPackDamagePerRabbit(index)) + ".", entry.RabbitPackStrengthUpgradeLevel);
                Add(result, "Быстрое размножение", "Максимум ударов до размножения: " + queue.GetRabbitMaximumReplicationHits(index)
                    + ". Начальный порог: " + queue.GetRabbitInitialReplicationHits(index)
                    + " ударов; затем растёт на один до максимума.", entry.RabbitBreedingUpgradeLevel);
                Add(result, "Ранний приплод", "Ударов до первого размножения: " + queue.GetRabbitInitialReplicationHits(index)
                    + ".", entry.RabbitFirstBreedingUpgradeLevel);
                Ultimate(result, "Пуховая буря", "Каждый 10-й общий удар заставляет всех кроликов выпустить облако на 10 урона.", entry.HasRabbitUltimate);
                break;
            case AnimalProjectileQueue.HoneyBadgerIndex:
                Add(result, "Радиус атаки", "Радиус: " + Cells(queue.GetHoneyBadgerAttackRadius(index)) + ".", entry.HoneyBadgerRadiusUpgradeLevel);
                Add(result, "Яростная цепь", "До " + queue.GetHoneyBadgerMaximumAttacks(index) + " атак; подряд по одному врагу: "
                    + queue.GetHoneyBadgerConsecutiveAttacks(index) + ".", entry.HoneyBadgerChainUpgradeLevel);
                Ultimate(result, "Абсолютная ярость", "Каждый следующий удар получает ещё +10 урона: +10, +20, +30…", entry.HasHoneyBadgerUltimate);
                break;
            case AnimalProjectileQueue.BoarIndex:
                Add(result, "Неудержимый разбег", "Отскоков за полёт: " + queue.GetBoarMaximumBounces(index) + ".", entry.BoarBouncesUpgradeLevel);
                Add(result, "Первый таран", "Первый удар по каждому врагу: " + Number(queue.GetBoarFirstHitDamageMultiplier(index) * 100f) + "% урона.", entry.BoarFirstHitUpgradeLevel);
                Ultimate(result, "Тройной прорыв", "Три кабана летят одновременно. Боковые траектории отклонены на ±4°.", entry.HasBoarUltimate);
                break;
            case AnimalProjectileQueue.BeaverIndex:
                Add(result, "Крепкая плотина", "В здоровье плотины или баррикады переходит "
                    + (1 + queue.GetBeaverDefenseHealthUpgradeLevel(index)) + "% нанесённого урона. "
                    + "Предел плотины: 25% максимального HP баррикады. Укрепление баррикады без ограничений.", entry.BeaverDefenseHealthUpgradeLevel);
                Add(result, "Крепкие зубы", "Дополнительный урон за укус: +" + Number(queue.GetBeaverBonusDamage(index)) + ".", entry.BeaverDamageUpgradeLevel);
                Ultimate(result, "Отскок от тыквы", "Один отскок от поля тыквы за полёт.", entry.HasBeaverUltimate);
                break;
            case AnimalProjectileQueue.FrogIndex:
                Add(result, "Дальний прыжок", "Радиус прыжка: " + Cells(queue.GetFrogJumpRadius(index)) + ".", entry.FrogRadiusUpgradeLevel);
                Add(result, "Лягушачий хор", "Каждая живая лягушка усиливает урон в своей комнате на " + queue.GetFrogChorusRank(index) + "%.", entry.FrogChorusUpgradeLevel);
                Ultimate(result, "КВА", "Урон лягушки поражает цель и врагов в радиусе одной клетки.", entry.HasFrogUltimate);
                break;
            case AnimalProjectileQueue.SquirrelIndex:
                Add(result, "Ранний взрыв", "Шанс взрыва жёлудя при повторном попадании белочки: "
                    + Number(queue.GetSquirrelEarlyExplosionChance(index) * 100f) + "%.", entry.SquirrelDetonationUpgradeLevel);
                Add(result, "Лесные путы", "Шанс оцепенения при взрыве: " + (queue.GetSquirrelBindingRank(index) * 3)
                    + "% для каждого врага. Лозы удерживают врага один ход.", entry.SquirrelBindingUpgradeLevel);
                Ultimate(result, "Цепная реакция", "Взрыв жёлудя подрывает соседние жёлуди.", entry.HasSquirrelUltimate);
                break;
        }
        return result;
    }

    private static void Add(List<Item> result, string title, string description, int rank, int maximum = AnimalProjectileQueue.MaximumBranchLevel)
    {
        if (rank > 0)
        {
            result.Add(new Item { Title = title, Description = description, Rank = rank, MaximumRank = maximum });
        }
    }
    private static void Ultimate(List<Item> result, string title, string description, bool selected)
    {
        if (selected)
        {
            result.Add(new Item { Title = title, Description = description, Ultimate = true });
        }
    }
}
