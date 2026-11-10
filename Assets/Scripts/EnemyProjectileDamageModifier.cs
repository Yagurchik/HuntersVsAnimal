using UnityEngine;

/// <summary>
/// Lets an enemy modify direct projectile damage while Brick retains ownership
/// of combo, health, combat text, death and reward handling.
/// Status effects and area damage intentionally bypass this direct-hit pipeline.
/// </summary>
public abstract class EnemyProjectileDamageModifier : MonoBehaviour
{
    public abstract float ModifyDamage(Brick enemy, Animal projectile,
        Vector2 worldImpactPoint, float baseDamage);
}
