using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Brick))]
public sealed class BogPustulantExplosion : MonoBehaviour, IEnemyDefeatHandler
{
    [SerializeField, Min(0.1f)] private float radius = 2f;
    [SerializeField, Min(0f)] private float projectileDamage = 5f;
    [SerializeField, Min(0f)] private float poisonMaxHealthMultiplier = 1f;
    [SerializeField, Min(0f)] private float burningMaxHealthMultiplier = 1.3f;

    private bool exploded;

    public bool TryHandleDefeat(Brick enemy)
    {
        if (exploded || enemy == null) return false;
        exploded = true;

        EnemyStatusEffects statuses = enemy.GetComponent<EnemyStatusEffects>();
        bool burning = statuses != null
            && statuses.GetStackCount(FireflyImpactEffect.BurnEffectId) > 0;
        CombatDamageSource damageSource = burning
            ? CombatDamageSource.Fire : CombatDamageSource.Poison;
        float enemyDamage = enemy.MaxHp * (burning
            ? burningMaxHealthMultiplier : poisonMaxHealthMultiplier);

        Vector2 center = enemy.transform.position;
        EnemyImpactEffectPool.PlayBogExplosion(center, burning, radius);

        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, radius);
        var affectedEnemies = new HashSet<Brick>();
        var affectedProjectiles = new HashSet<Animal>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null) continue;
            Brick targetEnemy = collider.GetComponentInParent<Brick>();
            if (targetEnemy != null)
            {
                if (targetEnemy != enemy && !targetEnemy.IsDefeated)
                    affectedEnemies.Add(targetEnemy);
                continue;
            }

            Animal projectile = collider.GetComponentInParent<Animal>();
            if (projectile != null) affectedProjectiles.Add(projectile);
        }

        foreach (Brick target in affectedEnemies)
            if (target != null && !target.IsDefeated)
                target.TakeDamage(enemyDamage, damageSource);

        foreach (Animal projectile in affectedProjectiles)
            if (projectile != null && projectile.TakeEnemyDamage(projectileDamage))
                Destroy(projectile.gameObject);

        // The normal Brick death path still awards XP and removes this enemy.
        return false;
    }
}
