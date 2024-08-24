using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Brick : MonoBehaviour
{
    [SerializeField] public float hp;
    [SerializeField] public float dmg;
    [SerializeField] private Image hpbar;

    //[SerializeField] private TextMeshProUGUI text;
    private float maxHp;
    public LevelGenerator levelGenerator;
    private void Awake()
    {
        maxHp = hp;
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Animal"))
        {
            Animal animal = collision.gameObject.GetComponent<Animal>();
            if (hp > 0)
            {
                hp -= animal.dmg;
                UpdateHealthUI();
            }
            if (hp <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
    public void Step()
    {
        transform.position += Vector3.down;

            if (transform.position.y <= GameManager.Instance.losePos)
            {
                GameManager.Instance.LoseGame();
            }
    }
    private void OnDisable()
    {
        levelGenerator.DestroyEnemy(gameObject);
    }
    private void UpdateHealthUI()
    {
        hpbar.fillAmount = hp / maxHp;
        //text.text = $"HP: {hp}";
    }
}