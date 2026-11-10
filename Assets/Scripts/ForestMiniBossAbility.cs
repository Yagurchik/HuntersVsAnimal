using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Brick))]
[DisallowMultipleComponent]
public sealed class ForestMiniBossAbility : EnemyProjectileDamageModifier
{
    public enum AbilityKind { HiveHollow, GreenJudge }

    [SerializeField] private AbilityKind kind;
    [SerializeField] private GameObject beePrefab;
    [SerializeField] private SpriteRenderer faceRenderer;
    [SerializeField, Min(1)] private int hitsPerBeeRelease = 3;
    [SerializeField, Range(1, 4)] private int maximumLivingBees = 2;
    [SerializeField, Range(1f, 2f)] private float openEyeDamageMultiplier = 1.5f;

    private readonly List<Brick> livingBees = new List<Brick>(2);
    private Brick enemy;
    private int hitsSinceRelease;
    private int lastTurn = int.MinValue;
    private bool releasePending;
    private bool leftEyeOpen = true;

    public AbilityKind Kind => kind;
    public bool LeftEyeOpen => leftEyeOpen;

    private void Awake()
    {
        enemy = GetComponent<Brick>();
        if (faceRenderer == null) faceRenderer = GetComponentInChildren<SpriteRenderer>(true);
        UpdateFace();
    }

    public override float ModifyDamage(Brick target, Animal projectile,
        Vector2 impactPoint, float baseDamage)
    {
        if (target == null || target.IsDefeated || projectile == null || baseDamage <= 0f)
            return baseDamage;

        if (kind == AbilityKind.GreenJudge)
        {
            bool struckLeft = impactPoint.x < target.transform.position.x;
            return struckLeft == leftEyeOpen
                ? baseDamage * openEyeDamageMultiplier : baseDamage;
        }

        hitsSinceRelease++;
        if (hitsSinceRelease >= Mathf.Max(1, hitsPerBeeRelease) && !releasePending)
        {
            hitsSinceRelease = 0;
            releasePending = true;
            StartCoroutine(ReleaseBeesAfterHit());
        }
        return baseDamage;
    }

    public void CompleteEnemyTurn(int turnNumber)
    {
        if (kind != AbilityKind.GreenJudge || enemy == null || enemy.IsDefeated
            || lastTurn == turnNumber) return;
        lastTurn = turnNumber;
        leftEyeOpen = !leftEyeOpen;
        UpdateFace();
    }

    private IEnumerator ReleaseBeesAfterHit()
    {
        // Brick resolves HP and defeat after damage modifiers. Spawn only if
        // the third hit left the hive alive, and never into occupied cells.
        yield return null;
        releasePending = false;
        if (enemy == null || enemy.IsDefeated || enemy.levelGenerator == null
            || beePrefab == null) yield break;

        for (int i = livingBees.Count - 1; i >= 0; i--)
            if (livingBees[i] == null || livingBees[i].IsDefeated)
                livingBees.RemoveAt(i);

        LevelGenerator room = enemy.levelGenerator;
        int available = Mathf.Max(0, maximumLivingBees - livingBees.Count);
        for (int i = 0; i < Mathf.Min(2, available); i++)
        {
            if (!room.TrySummonNear(enemy, beePrefab, out Brick bee)) break;
            bee.ConfigureSummonedVariant(0, Color.white);
            bee.SetSummonedHealth(bee.MaxHp * 0.3f);
            livingBees.Add(bee);
        }
    }

    private void UpdateFace()
    {
        if (kind == AbilityKind.GreenJudge && faceRenderer != null)
            faceRenderer.flipX = !leftEyeOpen;
    }
}
