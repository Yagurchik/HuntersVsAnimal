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
