using System;
using UnityEngine;

// Per-level totals include every room and the projectile's delayed effects.
public static class ProjectileDamageStats
{
    private static readonly double[] Damage = new double[AnimalProjectileQueue.AnimalTypeCount];
    public static event Action Changed;
    public static double TotalDamage { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Changed = null;
        BeginLevel();
    }

    public static void BeginLevel()
    {
        Array.Clear(Damage, 0, Damage.Length);
        TotalDamage = 0;
        Changed?.Invoke();
    }

    public static double ForAnimal(int index) => index >= 0 && index < Damage.Length ? Damage[index] : 0;

    public static void Record(float healthLost, CombatDamageSource source, int ownerIndex = -1,
        float frogSupportDamage = 0f)
    {
        int index = ownerIndex >= 0 ? ownerIndex : IndexFor(source);
        if (index < 0 || index >= Damage.Length || healthLost <= 0f
            || float.IsNaN(healthLost) || float.IsInfinity(healthLost))
        {
            return;
        }

        if (index != AnimalProjectileQueue.FrogIndex && frogSupportDamage > 0f)
        {
            float bonus = Mathf.Min(healthLost, frogSupportDamage);
            Damage[index] += healthLost - bonus;
            Damage[AnimalProjectileQueue.FrogIndex] += bonus;
        }
        else
        {
            Damage[index] += healthLost;
        }

        TotalDamage += healthLost;
        Changed?.Invoke();
    }

    public static int IndexFor(CombatDamageSource source)
    {
        switch (source)
        {
            case CombatDamageSource.Firefly: return AnimalProjectileQueue.FireflyIndex;
            case CombatDamageSource.Hedgehog: return AnimalProjectileQueue.HedgehogIndex;
            case CombatDamageSource.Rabbit: return AnimalProjectileQueue.RabbitIndex;
            case CombatDamageSource.HoneyBadger: return AnimalProjectileQueue.HoneyBadgerIndex;
            case CombatDamageSource.Boar: return AnimalProjectileQueue.BoarIndex;
            case CombatDamageSource.Beaver: return AnimalProjectileQueue.BeaverIndex;
            case CombatDamageSource.Frog: return AnimalProjectileQueue.FrogIndex;
            case CombatDamageSource.Squirrel:
            case CombatDamageSource.AcornExplosion: return AnimalProjectileQueue.SquirrelIndex;
            // Fire and poison also occur in enemy explosions. Their projectile
            // effects supply an owner explicitly, so enemy damage stays uncredited.
            default: return -1;
        }
    }
}
