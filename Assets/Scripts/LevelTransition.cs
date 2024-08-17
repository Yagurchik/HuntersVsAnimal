using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransition : MonoBehaviour
{
    public GameObject PausePanel, LosePanel, WinPanel;
    public int level;
    
    public void PauseButtonPressed()
    {
        PausePanel.SetActive(true);
        Time.timeScale = 0f;
    }
    public void ContinueButtonPressed()
    {
        PausePanel.SetActive(false);
        Time.timeScale = 1f;
    }
    public void ChangeScene(int scene)
    {
        SceneManager.LoadScene(scene);
    }
    public void LosePressed()
    {
        LosePanel.SetActive(true);
        Time.timeScale = 0f;
    }
    public void WinPressed()
    {
        WinPanel.SetActive(true);
        Time.timeScale = 0f;
    }
}