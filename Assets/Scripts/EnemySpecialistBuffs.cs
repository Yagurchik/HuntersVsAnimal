using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Brick))]
public sealed class EnemySpecialistBuffs : MonoBehaviour
{
    private float shield;
    private Brick shieldSource;
    private Brick brandSource;
    private int extraStep;
    private LineRenderer indicator;
    private SpriteRenderer head;

    public static EnemySpecialistBuffs Ensure(Brick enemy)
    {
        EnemySpecialistBuffs buffs = enemy.GetComponent<EnemySpecialistBuffs>();
        return buffs != null ? buffs : enemy.gameObject.AddComponent<EnemySpecialistBuffs>();
    }

    public void GrantShield(float amount, Brick source)
    {
        if (amount <= 0f || source == null) return;
        shield = Mathf.Max(shield, amount);
        shieldSource = source;
        RefreshIndicator();
    }

    public void GrantBrand(Brick source)
    {
        if (source == null) return;
        brandSource = source;
        RefreshIndicator();
    }
    public void GrantExtraStep() { extraStep = 1; RefreshIndicator(); }
    public int ConsumeExtraStep()
    {
        int value = extraStep;
        extraStep = 0;
        RefreshIndicator();
        return value;
    }

    public float AbsorbDirectDamage(float damage, CombatDamageSource source)
    {
        if (source == CombatDamageSource.Fire || source == CombatDamageSource.Firefly
            || source == CombatDamageSource.Poison) return damage;
        if (shieldSource != null && !shieldSource.IsDefeated && shield > 0f)
        {
            float blocked = Mathf.Min(shield, damage);
            damage -= blocked;
            shield = 0f;
            shieldSource = null;
            RefreshIndicator();
        }
        return damage;
    }

    public void OnProjectileHit(Animal projectile)
    {
        if (brandSource != null && !brandSource.IsDefeated && projectile != null)
        {
            brandSource = null;
            RefreshIndicator();
            if (projectile.TakeEnemyDamage(2f)) Destroy(projectile.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (indicator == null) return;
        if (head == null) head = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
        if (head != null)
        {
            indicator.sortingLayerID = head.sortingLayerID;
            indicator.sortingOrder = head.sortingOrder + 1;
        }
        RefreshIndicator();
    }

    private void RefreshIndicator()
    {
        bool branded = brandSource != null && !brandSource.IsDefeated;
        bool shielded = shieldSource != null && !shieldSource.IsDefeated && shield > 0f;
        bool accelerated = extraStep > 0;
        if (!branded && !shielded && !accelerated)
        {
            if (indicator != null) indicator.enabled = false;
            return;
        }
        if (indicator == null)
        {
            GameObject marker = new GameObject("Specialist Ability Marker");
            marker.transform.SetParent(transform, false);
            indicator = marker.AddComponent<LineRenderer>();
            indicator.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            indicator.useWorldSpace = false;
            indicator.loop = true;
            indicator.positionCount = 20;
            indicator.startWidth = 0.045f;
            indicator.endWidth = 0.045f;
            for (int i = 0; i < indicator.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / indicator.positionCount;
                indicator.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.45f,
                    Mathf.Sin(angle) * 0.45f, -0.02f));
            }
        }
        indicator.enabled = true;
        Color color = branded ? new Color(1f, 0.38f, 0.12f, 0.95f)
            : shielded ? new Color(0.55f, 0.88f, 1f, 0.95f)
            : new Color(0.32f, 1f, 0.8f, 0.95f);
        indicator.startColor = color;
        indicator.endColor = color;
    }

    private void OnDestroy()
    {
        if (indicator != null && indicator.sharedMaterial != null)
            Destroy(indicator.sharedMaterial);
    }
}
