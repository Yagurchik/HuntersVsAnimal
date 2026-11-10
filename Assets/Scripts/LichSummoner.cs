using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Brick))]
[DisallowMultipleComponent]
public sealed class LichSummoner : MonoBehaviour
{
    [SerializeField, Tooltip("Ordinary skeleton prefab. Falls back to the room's standard enemy.")]
    private GameObject skeletonPrefab;
    [SerializeField, Range(1, 3)] private int minimumSummons = 1;
    [SerializeField, Range(1, 3)] private int maximumSummons = 3;
    [SerializeField, Min(1), Tooltip("Maximum number of this Lich's living summons at once.")]
    private int maximumLivingSummons = 3;
    [SerializeField, Tooltip("Color multiplier for skeletons created by this Lich.")]
    private Color summonedSkeletonTint = new Color(0.78f, 1f, 0.72f, 1f);
    private readonly List<Brick> ownedSummons = new List<Brick>();
    private Brick enemy;
    private int lastSummonTurn = int.MinValue;
    public int LastSummonCount { get; private set; }
    public Color SummonedSkeletonTint => summonedSkeletonTint;
    public int MaximumLivingSummons => Mathf.Max(1, maximumLivingSummons);
    public int LivingSummonCount
    {
        get
        {
            RemoveDestroyedSummons();
            return ownedSummons.Count;
        }
    }

    private void Awake() => enemy = GetComponent<Brick>();

    public void CompleteEnemyTurn(int turnNumber)
    {
        if (enemy == null)
        {
            enemy = GetComponent<Brick>();
        }

        if (lastSummonTurn == turnNumber || enemy.IsDefeated || !isActiveAndEnabled
            || enemy.IsStepping || enemy.levelGenerator == null || enemy.levelGenerator.IsLost)
        {
            return;
        }

        lastSummonTurn = turnNumber;
        LastSummonCount = 0;
        LevelGenerator room = enemy.levelGenerator;
        GameObject prefab = skeletonPrefab != null ? skeletonPrefab : room.brickPrefab;
        if (prefab == null || !prefab.TryGetComponent(out Brick skeleton)
            || skeleton.Footprint != Vector2Int.one || prefab.GetComponent<LichSummoner>() != null)
        {
            return;
        }

        int availableSlots = MaximumLivingSummons - LivingSummonCount;
        if (availableSlots <= 0)
        {
            return;
        }

        int min = Mathf.Clamp(minimumSummons, 1, 3);
        int count = Mathf.Min(availableSlots,
            Random.Range(min, Mathf.Clamp(maximumSummons, min, 3) + 1));
        for (int i = 0; i < count; i++)
        {
            if (!room.TrySummonNear(enemy, prefab, out Brick summoned))
            {
                break;
            }

            summoned.ConfigureSummonedVariant(0, summonedSkeletonTint);
            ownedSummons.Add(summoned);
            LastSummonCount++;
        }
    }

    private void RemoveDestroyedSummons()
    {
        for (int i = ownedSummons.Count - 1; i >= 0; i--)
        {
            if (ownedSummons[i] == null)
            {
                ownedSummons.RemoveAt(i);
            }
        }
    }

    private void OnValidate()
    {
        minimumSummons = Mathf.Clamp(minimumSummons, 1, 3);
        maximumSummons = Mathf.Clamp(maximumSummons, minimumSummons, 3);
        maximumLivingSummons = Mathf.Max(1, maximumLivingSummons);
        summonedSkeletonTint = new Color(Mathf.Clamp01(summonedSkeletonTint.r),
            Mathf.Clamp01(summonedSkeletonTint.g), Mathf.Clamp01(summonedSkeletonTint.b),
            Mathf.Clamp01(summonedSkeletonTint.a));
    }
}
