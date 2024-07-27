using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] GameObject bullet;
    [SerializeField] private float projectileForce = 10f;

    // Update is called once per frame
    void Update()
    {
        if (bullet != null)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Shoot();
            }
        }
}
    private void Shoot()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 direction = (mousePos - transform.position).normalized;
        

        GameObject projectile = Instantiate(bullet, transform.position, Quaternion.identity);
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        rb.AddForce(direction * projectileForce, ForceMode2D.Impulse);
    }
}
