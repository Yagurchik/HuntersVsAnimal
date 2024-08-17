using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    private RaycastHit2D ray;
    private float angle;
    [SerializeField] private Vector2 minMaxAngle;

    [SerializeField] bool useRay;
    [SerializeField] bool useLine;
    [SerializeField] public bool useDots;

    [SerializeField] LineRenderer line;

    void Update()
    {
            ray = Physics2D.Raycast(transform.position, transform.up, 20f, layerMask);
            //Debug.DrawRay(transform.position, ray.point, Color.yellow);

            Vector2 reflactPos = Vector2.Reflect(new Vector3(ray.point.x, ray.point.y) - transform.position, ray.normal);
            Vector3 pos = Camera.main.WorldToScreenPoint(transform.position);
            Vector3 dir = Input.mousePosition - pos;

            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

            if (angle >= minMaxAngle.x && angle <= minMaxAngle.y)
            {
                if (useRay)
                {
                    Debug.DrawRay(transform.position, transform.up * ray.distance, Color.yellow);
                    Debug.DrawRay(ray.point, reflactPos.normalized * 2f, Color.green);
                }
                if (useLine)
                {
                    line.SetPosition(0,transform.position);
                    line.SetPosition(1, ray.point);
                    line.SetPosition(2, ray.point + reflactPos.normalized *2f);
                }
                if (useDots)
                {
                    Dots.instance.DrawDottedLine(transform.position, ray.point);
                    Dots.instance.DrawDottedLine(ray.point, ray.point + reflactPos.normalized * 2f);
                }
            }
            transform.rotation = Quaternion.AngleAxis(angle, transform.forward);
    }
}
