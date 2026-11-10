using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoseMoment : MonoBehaviour
{
    private Collider2D _collider;

    private void Start()
    {
        _collider = GetComponent<Collider2D>();
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") && other.TryGetComponent(out Brick brick)
            && brick.levelGenerator != null && GameManager.Instance != null
            && !brick.IsStepping && brick.LowestCellY <= brick.levelGenerator.DefenseBoundaryY
            && !Player.HasActiveProjectiles)
        {
            brick.levelGenerator.RequestBreach(brick);
        }
    }
}
