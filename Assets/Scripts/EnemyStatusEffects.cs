using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyStatusEffects : MonoBehaviour
{
    public enum StackingMode
    {
        Refresh,
        AddDamage
    }

    [Serializable]
    private sealed class PeriodicDamage
    {
        public string Id;
        public float Damage;
        public int RemainingTurns;
        public int Stacks;
        public bool Permanent;
        public CombatDamageSource Source;
        public int Owner;
        public bool AlreadyModified;
    }

    private static readonly List<EnemyStatusEffects> All = new List<EnemyStatusEffects>();
    private readonly List<PeriodicDamage> periodicDamage = new List<PeriodicDamage>();
    private Brick enemy;
    private int lastProcessedTurn = -1;

    public int ActivePeriodicDamageCount => periodicDamage.Count;
    public int LastProcessedTurn => lastProcessedTurn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => All.Clear();

    private void Awake()
    {
        enemy = GetComponent<Brick>();
        if (!All.Contains(this))
        {
            All.Add(this);
        }
    }

    public bool HasPeriodicDamage(string effectId)
    {
        return periodicDamage.Exists(effect => effect.Id == effectId);
    }

    public float GetPeriodicDamage(string effectId)
    {
        PeriodicDamage effect = periodicDamage.Find(candidate => candidate.Id == effectId);
        return effect != null ? effect.Damage : 0f;
    }

    public int GetRemainingTurns(string effectId)
    {
        PeriodicDamage effect = periodicDamage.Find(candidate => candidate.Id == effectId);
        return effect != null ? effect.RemainingTurns : 0;
    }

    public int GetStackCount(string effectId)
    {
        PeriodicDamage effect = periodicDamage.Find(candidate => candidate.Id == effectId);
        return effect != null ? Mathf.Max(1, effect.Stacks) : 0;
    }

    public int ApplyPeriodicDamage(string effectId, float damage, int turns,
        StackingMode stacking = StackingMode.Refresh, int maximumStacks = int.MaxValue,
        bool permanent = false, CombatDamageSource source = CombatDamageSource.Other,
        int damageOwner = -1, bool alreadyModified = false)
    {
        if (enemy == null)
        {
            enemy = GetComponent<Brick>();
        }

        if (enemy == null || enemy.IsDefeated || string.IsNullOrWhiteSpace(effectId)
            || damage <= 0f || turns <= 0)
        {
            return 0;
        }

        bool isBurn = effectId == FireflyImpactEffect.BurnEffectId;
        if (isBurn) { source = CombatDamageSource.Fire; damageOwner = AnimalProjectileQueue.FireflyIndex; }
        maximumStacks = Mathf.Max(1, maximumStacks);
        PeriodicDamage existing = periodicDamage.Find(effect => effect.Id == effectId);
        if (existing == null)
        {
            periodicDamage.Add(new PeriodicDamage {
                Id = effectId, Damage = damage, RemainingTurns = permanent ? int.MaxValue : isBurn ? 1 : turns, Stacks = 1,
                Permanent = permanent, Source = source, Owner = damageOwner, AlreadyModified = alreadyModified
            });
            return 1;
        }

        if (stacking == StackingMode.AddDamage)
        {
            existing.Stacks = Mathf.Clamp(existing.Stacks, 1, maximumStacks);
            if (existing.Stacks < maximumStacks)
            {
                existing.Damage += damage;
                existing.Stacks++;
            }
            else
            {
                // A stronger later application can refresh a capped stack set
                // without allowing the number of stacks to grow forever.
                existing.Damage = Mathf.Max(existing.Damage, damage * maximumStacks);
            }
        }
        else
        {
            existing.Damage = Mathf.Max(existing.Damage, damage);
            existing.Stacks = Mathf.Max(1, existing.Stacks);
        }
        existing.Permanent |= permanent;
        existing.RemainingTurns = existing.Permanent
            ? int.MaxValue : isBurn ? existing.Stacks : Mathf.Max(existing.RemainingTurns, turns);
        return existing.Stacks;
    }

    public void ClearAll()
    {
        periodicDamage.Clear();
    }

    public static int ProcessAllAfterEnemyTurn(int turnNumber)
    {
        int ticks = 0;
        // Damage can destroy an enemy and unregister its controller during this pass.
        EnemyStatusEffects[] controllers = All.ToArray();
        for (int i = controllers.Length - 1; i >= 0; i--)
        {
            EnemyStatusEffects controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            ticks += controller.ProcessAfterEnemyTurn(turnNumber);
        }
        All.RemoveAll(controller => controller == null);
        return ticks;
    }

    private int ProcessAfterEnemyTurn(int turnNumber)
    {
        if (turnNumber <= lastProcessedTurn || periodicDamage.Count == 0)
        {
            return 0;
        }

        lastProcessedTurn = turnNumber;
        if (enemy == null)
        {
            enemy = GetComponent<Brick>();
        }

        if (enemy == null || enemy.IsDefeated) { periodicDamage.Clear(); return 0; }

        int ticks = 0;
        // A lethal tick can turn a zombie into a grave, which clears this list
        // synchronously while TakeAfterTurnDamage is still on the stack.
        PeriodicDamage[] effects = periodicDamage.ToArray();
        for (int i = effects.Length - 1; i >= 0; i--)
        {
            PeriodicDamage effect = effects[i];
            if (!periodicDamage.Contains(effect))
            {
                continue;
            }

            float damage = effect.Damage;
            bool decayingBurn = !effect.Permanent && effect.Id == FireflyImpactEffect.BurnEffectId;
            if (!effect.Permanent && !decayingBurn)
            {
                effect.RemainingTurns--;
            }
            // Keep the effect registered while its final tick deals damage. Death
            // reactions can then identify which status caused the defeat.
            enemy.TakeAfterTurnDamage(damage, effect.Source, effect.Owner, effect.AlreadyModified);
            ticks++;
            if (enemy == null || enemy.IsDefeated)
            {
                periodicDamage.Clear();
                break;
            }
            // Remove by identity: callbacks may clear, add, or refresh effects.
            if (!periodicDamage.Contains(effect))
            {
                continue;
            }

            if (decayingBurn)
            {
                // The current stacks deal their damage before one burns out.
                // Keep the damage per remaining stack unchanged.
                int stacks = Mathf.Max(1, effect.Stacks);
                effect.Damage *= (stacks - 1f) / stacks;
                effect.Stacks = stacks - 1;
                effect.RemainingTurns = effect.Stacks;
            }
            if (!effect.Permanent && effect.RemainingTurns <= 0)
            {
                periodicDamage.Remove(effect);
            }

            if (decayingBurn)
            {
                enemy.GetComponent<EnemyBurnTint>()?.Refresh();
            }
        }
        return ticks;
    }

    private void OnDestroy()
    {
        All.Remove(this);
        periodicDamage.Clear();
    }
}
