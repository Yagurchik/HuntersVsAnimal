using UnityEngine;

[RequireComponent(typeof(Brick)), DisallowMultipleComponent]
public sealed class BossEnemyTurnController : MonoBehaviour
{
    [SerializeField, Range(1, 4)] private int movementCellsPerTurn = 2;
    [SerializeField, Min(1)] private int summonEveryTurns = 3;
    [SerializeField] private bool summonOnFirstTurn = true;
    [SerializeField] private GameObject skeletonPrefab;
    [SerializeField] private Color summonedSkeletonTint = Color.white;
    private Brick enemy;
    private int lastProcessedTurn = int.MinValue;
    private int turnsSinceSummon;

    public int MovementCellsPerTurn => Mathf.Clamp(movementCellsPerTurn, 1, 4);
    public int LastSummonCount { get; private set; }

    private void Awake()
    {
        enemy = GetComponent<Brick>();
        turnsSinceSummon = summonOnFirstTurn ? Mathf.Max(0, summonEveryTurns - 1) : 0;
    }

    public void CompleteEnemyTurn(int turnNumber)
    {
        if (enemy == null) enemy = GetComponent<Brick>();
        if (lastProcessedTurn == turnNumber || enemy.IsDefeated || enemy.levelGenerator == null
            || enemy.levelGenerator.IsLost) return;
        lastProcessedTurn = turnNumber;
        LastSummonCount = 0;
        turnsSinceSummon++;
        if (turnsSinceSummon < Mathf.Max(1, summonEveryTurns)) return;
        turnsSinceSummon = 0;

        LevelGenerator room = enemy.levelGenerator;
        GameObject prefab = skeletonPrefab != null ? skeletonPrefab : room.brickPrefab;
        LastSummonCount = room.SummonSkeletonLineAhead(enemy, prefab, summonedSkeletonTint);
    }
}
