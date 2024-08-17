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
    public void Awake()
    {
        magazine = new Magazine(magazineBullet);
    }
    void Update()
    {
        if (GameManager.Instance.stepPlayer == true)
        {
            magazine.Reload();
            FirstAnimalOut = false;
            shootingPlayer = true;
            GameManager.Instance.stepPlayer = false;
            Debug.Log("Ход игрока");
        }
        //if (bullet != null && shootingPlayer == true)
        //{
        //    if (Input.GetMouseButtonDown(0) && Input.mousePosition.y > transform.position.y)
        //    {
        //        shootingPlayer = false;
        //        Shoot();
        //    }
        //}
        if (bullet != null && shootingPlayer == true)
        {
                if (Input.touchCount > 0)
                {
                    ballSpawner.useDots = true;
                    Touch touch = Input.GetTouch(0);

                    if (touch.phase == TouchPhase.Ended)
                    {
                    ballSpawner.useDots = false;
                    Vector3 touchPos = Camera.main.ScreenToWorldPoint(touch.position);
                        touchPos.z = 0;
                        Shoot(touch);
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
            Debug.Log("Нет патронов! ");
        }
        OnShootEvent?.Invoke();
    }
    //private IEnumerator ShootCoroutine()
    //{
    //    while (magazine.HasAmmo())
    //    {
    //        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    //        mousePos.z = 0;
    //        Vector3 direction = (mousePos - transform.position).normalized;
    //        GameObject projectile = Instantiate(bullet, transform.position, Quaternion.identity);
    //        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
    //        rb.velocity = direction * projectileForce; 
    //        magazine.UseAmmo();
    //        yield return new WaitForSeconds(0.1f);
    //    }
    //    StopCoroutine(coroutine);
    //}
    private IEnumerator ShootCoroutine(Touch touch)
    {
        while (magazine.HasAmmo())
        {
            Vector3 touchPos = Camera.main.ScreenToWorldPoint(touch.position);
            touchPos.z = 0; // Убедитесь, что координата Z равна 0
            Vector3 direction = (touchPos - transform.position).normalized;
            GameObject projectile = Instantiate(bullet, transform.position, Quaternion.identity);
            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            rb.velocity = direction * projectileForce;
            magazine.UseAmmo();
            yield return new WaitForSeconds(0.1f);
        }
        StopCoroutine(coroutine);
    }
}