using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransition : MonoBehaviour
{
    public GameObject PausePanel, LosePanel, WinPanel;
    public int level;
    private Player player;
    
    public void PauseButtonPressed()
    {
        PausePanel.SetActive(true);
        GameManager.Instance.isPaused = true;
        Time.timeScale = 0f;
    }
    public void ContinueButtonPressed()
    {
        PausePanel.SetActive(false);
        GameManager.Instance.isPaused = false;
        Time.timeScale = 1f;
    }
    public void ChangeScene(int scene)
    {
        SceneManager.LoadScene(scene);
    }
    public void LosePressed()
    {
        GameManager.Instance.isPaused = true;
        LosePanel.SetActive(true);
        Time.timeScale = 0f;
    }
    public void WinPressed()
    {
        GameManager.Instance.isPaused = true;
        WinPanel.SetActive(true);
        Time.timeScale = 0f;
    }
}