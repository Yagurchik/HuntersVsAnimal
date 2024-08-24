using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stage : MonoBehaviour
{
    [SerializeField] private GameObject player;
    public Orientation orientation;

    private void OnEnable()
    {
        player.SetActive(true);
    }
    private void OnDisable()
    {
        player.SetActive(false);
    }
}
public enum Orientation
{
    left,
    up,
    right
}
