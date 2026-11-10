using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Settings : MonoBehaviour
{
    [SerializeField] public int _enemySet;
    [SerializeField] public int _enemyAddWaves;

    public void Configure(int enemyCount, int additionalWaves)
    {
        _enemySet = Mathf.Max(0, enemyCount);
        _enemyAddWaves = Mathf.Max(0, additionalWaves);
    }
}
