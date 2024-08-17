using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public class Animal : MonoBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] public float dmg;
    [SerializeField] float hp;
    private GameObject player;
    private Vector3 newPos;

    private static bool speedIncreased = false;
    private static bool onColEnter = false;
    private float timer = 0f;


    private void Awake()
    {
        player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player not found!");
        }
    }

    private void Start()
    {
        Player.animalsCollect.Add(this);
    }

    public virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy") && !speedIncreased)
        {
            onColEnter = true;
            timer = 0f;
            Brick brick = collision.gameObject.GetComponent<Brick>();
            if (hp > 0)
            {
                hp -= brick.dmg;
            }
            if (hp <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
    void Update()
    {
        if (onColEnter)
        {
            timer += Time.deltaTime;
            if (timer >= 4f)
            {
                Time.timeScale = 2.0f;
                onColEnter = false;
            }
        }
        if (transform.position.y < player.transform.position.y)
        {
            if (Player.FirstAnimalOut == true)
            {
                Destroy(gameObject);
            }
            else
            {
                newPos = player.transform.position;
                newPos.x = transform.position.x;
                player.transform.position = newPos;
                Destroy(gameObject);
            }

        }
    }
    private void OnDestroy()
    {
        if(Player.animalsCollect.Count == 1)
        {
            Time.timeScale = 1f;
            GameManager.Instance.stepGame = true;
        }
        Player.FirstAnimalOut = true;
        Player.animalsCollect.Remove(this);
    }

}
