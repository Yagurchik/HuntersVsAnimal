using UnityEngine;

/// <summary>
/// Lets a projectile change its direct impact damage without duplicating
/// Brick's hit, combo, health and combat-text pipeline.
/// </summary>
public abstract class ProjectileDamageModifier : MonoBehaviour
{
    public abstract float ModifyDamage(Brick enemy, float baseDamage);
}

/// <summary>
/// Receives one callback after an ordinary projectile hit has passed through
/// Brick's damage and combo pipeline. Secondary damage must not call this hook.
/// </summary>
public interface IProjectileHitResolved
{
    void OnProjectileHitResolved(Brick enemy, Vector2 impactPoint, Vector2 impactNormal);
}
