using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraBehaviour : MonoBehaviour
{
    [SerializeField] private Transform stage2_1;
    [SerializeField] private Transform stage2_2;
    [SerializeField] private Transform stage2_3;
    private Vector3 stage1;
    private void Start()
    {
        stage1 = transform.position;
    }
    public void Stage1()
    {
        transform.position = stage1;
        transform.rotation = Quaternion.Euler(0,0,0);
    }
    public void Stage2_1()
    {
        transform.position = stage2_1.position;
        transform.rotation = Quaternion.Euler(0, 0, -90);
    }
    public void Stage2_2()
    {
        transform.position = stage2_2.position;
        transform.rotation = Quaternion.Euler(0, 0, 0);
    }
    public void Stage2_3()
    {
        transform.position = stage2_3.position;
        transform.rotation = Quaternion.Euler(0, 0, 90);
    }
}
