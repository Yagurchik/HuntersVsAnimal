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

    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 5f;
    //private LineRenderer lineRenderer;
    public void Awake()
    {
        magazine = new Magazine(magazineBullet);
        //lineRenderer = gameObject.AddComponent<LineRenderer>();
        //lineRenderer.positionCount = 5;
        //lineRenderer.loop = true;
        //lineRenderer.startWidth = 0.1f;
        //lineRenderer.endWidth = 0.1f;
        //lineRenderer.useWorldSpace = true;
        //DrawBoundary();
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
                Vector3 touchPos = Camera.main.ScreenToWorldPoint(touch.position);
                touchPos.z = 0;
                if (touchPos.y >= minY && touchPos.y <= maxY)
                {
                    ballSpawner.useDots = true;
                    if (touchPos.y < minY && touchPos.y > maxY)
                    {
                        ballSpawner.useDots = false;
                    }
                    if (touch.phase == TouchPhase.Ended)
                    {
                        ballSpawner.useDots = false;
                        Shoot(touch);
                    }
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
    public void DropDownBalls()
    {
        for(int i = 0; i < animalsCollect.Count; i++)
        {
            animalsCollect[i].DropDown();
        }
    }
    //private void DrawBoundary()
    //{
    //    Vector3[] boundaryPoints = new Vector3[5];
    //    boundaryPoints[0] = new Vector3(0, minY, 0);
    //    boundaryPoints[1] = new Vector3(0, minY, 0);
    //    boundaryPoints[2] = new Vector3(0, maxY, 0);
    //    boundaryPoints[3] = new Vector3(0, maxY, 0);
    //    boundaryPoints[4] = new Vector3(0, minY, 0);

    //    lineRenderer.SetPositions(boundaryPoints);
    //}
}