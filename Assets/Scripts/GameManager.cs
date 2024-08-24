using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public List<Animal> animals;
    public bool stepPlayer = false;
    public bool stepGame = false;
    public bool isPaused = false;
    public int stage = 1;
    public int maxStage;
    [SerializeField] private GameObject secondStage;
    [SerializeField] private GameObject secondStageButton;
    public float losePos = -4;
    public List<LevelGenerator> rooms = new List<LevelGenerator>();

    private LevelTransition levelTransition;

    public void LoseGame()
    {
        stepGame = false;
        stepPlayer = false;
        levelTransition.LosePressed();
    }
    public void WinGame()
    {
        stepGame = false;
        stepPlayer = false;
        levelTransition.WinPressed();
    }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        levelTransition = FindObjectOfType<LevelTransition>();
    }
    private void Start()
    {
        Time.timeScale = 1.0f;
        stepPlayer = true;
    }
    public void NextStage()
    {
        stage ++;
        secondStage.SetActive(true);
        secondStageButton.SetActive(true);
    }
}