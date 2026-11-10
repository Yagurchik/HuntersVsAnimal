using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Brick))]
[DisallowMultipleComponent]
public sealed class UniversalBossAbility : EnemyProjectileDamageModifier
{
    public enum AbilityKind { BellRinger, KnotBinder, VolleyEater, ThreeSealIdol, Impostor }

    [SerializeField] private AbilityKind kind;
    [SerializeField] private GameObject decoyPrefab;
    [SerializeField] private SpriteRenderer[] sealRenderers;
    [SerializeField] private LineRenderer[] linkLines;
    [SerializeField, Range(0f, 0.5f)] private float redirectedDamagePerLink = 0.2f;
    [SerializeField, Range(0f, 1f)] private float swallowedProjectileHealFraction = 0.03f;
    [SerializeField, Range(1f, 2f)] private float exposedIdolDamageMultiplier = 1.3f;

    private readonly List<Brick> candidates = new List<Brick>(16);
    private readonly HashSet<Animal> swallowedProjectiles = new HashSet<Animal>();
    private readonly List<Brick> livingDecoys = new List<Brick>(2);
    private Brick enemy;
    private int swallowedVolley = int.MinValue;
    private int sealsRemaining = 3;
    private int pendingBellVolley = int.MinValue;
    private bool splitScheduled;
    private bool splitDone;

    public AbilityKind Kind => kind;
    public int SealsRemaining => sealsRemaining;
    public bool BellPulsePending => pendingBellVolley != int.MinValue;
    public bool SplitDone => splitDone;
    public int SwallowedProjectileCount => swallowedProjectiles.Count;

    private void Awake()
    {
        enemy = GetComponent<Brick>();
    }

    private void LateUpdate()
    {
        if (kind != AbilityKind.KnotBinder || linkLines == null)
        {
            return;
        }

        int links = FindLinks();
        for (int i = 0; i < linkLines.Length; i++)
        {
            LineRenderer line = linkLines[i];
            if (line == null)
            {
                continue;
            }

            bool visibleTarget = i < links && (enemy.levelGenerator.Darkness == null
                || enemy.levelGenerator.Darkness.IsVisible(candidates[i].transform.position));
            line.enabled = visibleTarget;
            if (!visibleTarget)
            {
                continue;
            }

            line.SetPosition(0, enemy.transform.position + Vector3.forward * 0.02f);
            line.SetPosition(1, candidates[i].transform.position + Vector3.forward * 0.02f);
        }
    }

    public override float ModifyDamage(Brick target, Animal projectile,
        Vector2 impactPoint, float baseDamage)
    {
        if (target == null || target.IsDefeated || projectile == null || baseDamage <= 0f)
        {
            return baseDamage;
        }

        switch (kind)
        {
            case AbilityKind.BellRinger:
                pendingBellVolley = projectile.ComboVolleyId > 0
                    ? projectile.ComboVolleyId : Player.LastCombatVolleyId;
                return baseDamage;
            case AbilityKind.KnotBinder:
                return RedirectToLinks(projectile, baseDamage);
            case AbilityKind.ThreeSealIdol:
                if (sealsRemaining > 0)
                {
                    sealsRemaining--;
                    if (sealRenderers != null && sealsRemaining < sealRenderers.Length
                        && sealRenderers[sealsRemaining] != null)
                    {
                        sealRenderers[sealsRemaining].enabled = false;
                    }

                    return 0f;
                }
                return baseDamage * exposedIdolDamageMultiplier;
            case AbilityKind.Impostor:
                if (!splitDone && !splitScheduled)
                {
                    splitScheduled = true;
                    StartCoroutine(SplitAfterHit());
                }
                return baseDamage;
            default:
                return baseDamage;
        }
    }

    // Run before the usual hit pipeline so a swallowed shot cannot burn, poison,
    // stun, attach a rider, or trigger any other projectile effect.
    public bool TryConsumeProjectile(Animal projectile)
    {
        if (kind != AbilityKind.VolleyEater || enemy == null || enemy.IsDefeated
            || projectile == null)
        {
            return false;
        }

        int volley = projectile.ComboVolleyId > 0
            ? projectile.ComboVolleyId : Player.LastCombatVolleyId;
        if (swallowedVolley == int.MinValue)
        {
            swallowedVolley = volley;
        }

        if (volley != swallowedVolley)
        {
            return false;
        }

        if (swallowedProjectiles.Add(projectile))
        {
            enemy.Heal(enemy.MaxHp * swallowedProjectileHealFraction);
        }

        if (projectile.gameObject.activeSelf)
        {
            projectile.gameObject.SetActive(false);
        }

        Destroy(projectile.gameObject);
        return true;
    }

    public bool ConsumeBellPulse(int completedVolleyId, List<Brick> targets)
    {
        if (kind != AbilityKind.BellRinger || pendingBellVolley == int.MinValue
            || pendingBellVolley != completedVolleyId || enemy == null || enemy.IsDefeated
            || enemy.levelGenerator == null)
        {
            return false;
        }

        pendingBellVolley = int.MinValue;
        LevelGenerator room = enemy.levelGenerator;
        foreach (GameObject occupant in room.EnemyCollection)
        {
            if (occupant != null && occupant.TryGetComponent(out Brick candidate)
                && candidate != enemy && !candidate.IsDefeated && !candidate.UsesBossHealth
                && candidate.levelGenerator == room)
            {
                targets.Add(candidate);
            }
        }

        return true;
    }

    private float RedirectToLinks(Animal projectile, float baseDamage)
    {
        int links = FindLinks();
        if (links == 0)
        {
            return baseDamage;
        }

        float share = baseDamage * redirectedDamagePerLink;
        for (int i = 0; i < links; i++)
        {
            candidates[i].TakeDamage(share, projectile.CombatSource);
        }

        return baseDamage - share * links;
    }

    private int FindLinks()
    {
        if (enemy == null || enemy.IsDefeated || enemy.levelGenerator == null)
        {
            return 0;
        }

        candidates.Clear();
        enemy.levelGenerator.CollectNearbyEnemies(enemy.transform.position, 4f,
            true, enemy, candidates);
        candidates.RemoveAll(candidate => candidate == null || candidate.IsDefeated
            || candidate.UsesBossHealth || candidate.levelGenerator != enemy.levelGenerator);
        candidates.Sort((a, b) =>
            ((Vector2)a.transform.position - (Vector2)enemy.transform.position).sqrMagnitude
                .CompareTo(((Vector2)b.transform.position - (Vector2)enemy.transform.position).sqrMagnitude));
        return Mathf.Min(2, candidates.Count);
    }

    private IEnumerator SplitAfterHit()
    {
        yield return null;
        splitScheduled = false;
        if (enemy == null || enemy.IsDefeated || enemy.levelGenerator == null
            || splitDone || enemy.hp > enemy.MaxHp * 0.5f || decoyPrefab == null)
        {
            yield break;
        }

        LevelGenerator room = enemy.levelGenerator;
        for (int i = livingDecoys.Count - 1; i >= 0; i--)
        {
            if (livingDecoys[i] == null || livingDecoys[i].IsDefeated)
            {
                livingDecoys.RemoveAt(i);
            }
        }

        for (int i = livingDecoys.Count; i < 2; i++)
        {
            if (!room.TrySummonNear(enemy, decoyPrefab, out Brick decoy))
            {
                break;
            }

            decoy.ConfigureSummonedVariant(0, Color.white);
            decoy.SetSummonedHealth(1f);
            livingDecoys.Add(decoy);
        }
        splitDone = livingDecoys.Count >= 2;
    }
}
