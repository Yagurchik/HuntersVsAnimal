using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Player : MonoBehaviour
{
    public GameObject bullet;
    public UnityEvent OnShootEvent;
    [SerializeField] private float projectileForce = 10f;
    private Magazine magazine;
    public int magazineBullet;
    Coroutine coroutine;

    public static List<Animal> animalsCollect = new List<Animal>();
    public static bool FirstAnimalOut = false;
    private static bool shootingPlayer = true;

    [SerializeField] private BallSpawner ballSpawner;

    [SerializeField] private Collider2D touchAreaCollider;
    public void Awake()
    {
        magazine = new Magazine(magazineBullet);
    }
    void Update()
    {
        if (GameManager.Instance.isPaused)
        {
            return; 
        }
        if (GameManager.Instance.stepPlayer == true)
        {
            magazine.Reload();
            FirstAnimalOut = false;
            shootingPlayer = true;
            GameManager.Instance.stepPlayer = false;
            Debug.Log("Ход игрока");
        }
        if (bullet != null && shootingPlayer == true)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                Vector2 touchPos = Camera.main.ScreenToWorldPoint(touch.position);
                RaycastHit2D hit = Physics2D.Raycast(touchPos, Vector2.zero);
                if (hit.collider != null && hit.collider == touchAreaCollider)
                {
                    ballSpawner.useDots = true;
                    if (touch.phase == TouchPhase.Ended)
                    {
                        ballSpawner.useDots = false;
                        Shoot(touch);
                    }
                }
                else
                {
                    ballSpawner.useDots = false;
                }
            }
        }
    }
    private void Shoot(Touch touch)
    {
        if (magazine.HasAmmo())
        {
            coroutine = StartCoroutine(ShootCoroutine(touch));
        }
        else
        {
            Debug.Log("Нет патронов!");
        }
        OnShootEvent?.Invoke();
    }
    private IEnumerator ShootCoroutine(Touch touch)
    {
        while (magazine.HasAmmo())
        {
            Vector3 touchPos = Camera.main.ScreenToWorldPoint(touch.position);
            touchPos.z = 0;
            Vector3 direction = (touchPos - transform.position).normalized;
            GameObject projectile = Instantiate(bullet, transform.position, Quaternion.identity);
            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            rb.velocity = direction * projectileForce;
            magazine.UseAmmo();
            yield return new WaitForSeconds(0.1f);
        }
    }
    public void DropDownBalls()
    {
        for(int i = 0; i < animalsCollect.Count; i++)
        {
            animalsCollect[i].DropDown();
        }
    }
}