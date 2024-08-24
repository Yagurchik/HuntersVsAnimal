using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoseMoment : MonoBehaviour
{
    private Collider2D _collider;

    private void Start()
    {
        Debug.Log("Game Over");
        _collider = GetComponent<Collider2D>();
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        Debug.Log("Game Over");
        if (other.CompareTag("Enemy"))
        {
            GameManager.Instance.LoseGame();
        }
    }
}