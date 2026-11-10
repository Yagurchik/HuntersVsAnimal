using UnityEngine;

// Add subclasses to projectile prefabs; Brick invokes them before direct impact damage.
public abstract class ProjectileEnemyEffect : MonoBehaviour
{
    public abstract void ApplyTo(Brick enemy);
}
