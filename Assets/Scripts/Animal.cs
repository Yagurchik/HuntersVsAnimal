using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
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

    private Collider2D objectCollider;
    private Rigidbody2D _rb;
    private void Awake()
    {
        player = GameObject.FindWithTag("Player");
        objectCollider = GetComponent<Collider2D>();
        _rb = GetComponent<Rigidbody2D>();
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
            if (timer >= 3f)
            {
                Time.timeScale = 3.0f;
                onColEnter = false;
            }
        }
        if (transform.position.y < player.transform.position.y - 0.5)
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
    public void DropDown()
    {
        objectCollider.enabled = false;
        Player.FirstAnimalOut = true;
        Vector3 direction = (player.transform.position - transform.position).normalized;
        _rb.AddForce(direction * speed * 200, ForceMode2D.Force);
    }
}
