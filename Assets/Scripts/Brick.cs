using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Brick : MonoBehaviour
{
    [SerializeField] int hp;
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hp > 0)
        {
            hp -= 1;
        } 
        if (hp <= 0)
        {
            Destroy(gameObject);
        }
    }
}
