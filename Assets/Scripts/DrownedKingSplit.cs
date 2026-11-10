using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Brick))]
public sealed class DrownedKingSplit : MonoBehaviour, IEnemyDefeatHandler
{
    [SerializeField] private GameObject heirPrefab;
    private bool split;

    public bool TryHandleDefeat(Brick enemy)
    {
        if (split || enemy == null || enemy.levelGenerator == null)
        {
            return false;
        }

        split = true;
        GameObject prefab = heirPrefab != null ? heirPrefab
            : Resources.Load<GameObject>("Enemies/DrownedHeir");
        enemy.levelGenerator.SpawnDrownedHeirs(enemy, prefab);
        // This is a real defeat, unlike the zombie's temporary grave.
        return false;
    }
}
