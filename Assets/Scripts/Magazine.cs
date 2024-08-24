using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Magazine
{
    public int maxAmmo;
    public int currentAmmo;

    public Magazine(int maxAmmo)
    {
        this.maxAmmo = maxAmmo;
        this.currentAmmo = maxAmmo;
    }
    public bool HasAmmo()
    {
        return currentAmmo > 0;
    }

    public void UseAmmo()
    {
        if (currentAmmo > 0)
        {
            currentAmmo--;
        }
    }
    public void Reload()
    {
        currentAmmo = maxAmmo;
    }

}
//public class BulletPool : MonoBehaviour
//{
//    [SerializeField] private GameObject _bulletPrefab;

//    private Queue<Animal> _bulletPool = new Queue<Animal>();

//    public static BulletPool Instance { get; private set; }

//    private void Awake()
//    {
//        Instance = this;
//    }
//    public Animal GetBullet()
//    {
//        if (_bulletPool.Count > 0)
//        {
//            Animal bullet = _bulletPool.Dequeue();
//            bullet.gameObject.SetActive(true);
//            return bullet;
//        }
//        else
//        {
//            Animal newBullet = Instantiate(_bulletPrefab).GetComponent<Animal>();
//            return newBullet;
//        }
//    }

//    public void ReturnBullet(Animal bullet)
//    {
//        bullet.gameObject.SetActive(false);
//        _bulletPool.Enqueue(bullet);
//    }
//}
