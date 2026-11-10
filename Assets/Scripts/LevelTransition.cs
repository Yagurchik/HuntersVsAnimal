using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelTransition : MonoBehaviour
{
    public GameObject PausePanel, LosePanel, WinPanel;
    [Header("Survival result — editable Lose panel label")]
    [SerializeField] private Text survivalResultText;
    public int level;
    public Text SurvivalResultText => survivalResultText;
    private Player player;
    private void Awake()
    {
        ConfigurePanelLayers();
        if (PausePanel != null) PausePanel.SetActive(false);
        if (LosePanel != null) LosePanel.SetActive(false);
        if (WinPanel != null) WinPanel.SetActive(false);
        if (survivalResultText != null) survivalResultText.gameObject.SetActive(false);
    }

    public void ConfigurePanelLayers()
    {
        foreach (GameObject panel in new[] { PausePanel, LosePanel, WinPanel })
        {
            if (panel == null) continue;
            Canvas parent = panel.transform.parent != null ? panel.transform.parent.GetComponentInParent<Canvas>(true) : null;
            Canvas canvas = panel.GetComponent<Canvas>();
            if (canvas == null) canvas = panel.AddComponent<Canvas>();
            Canvas root = parent != null ? parent.rootCanvas : canvas;
            // Handle both nested legacy panels and standalone panels placed in the scene.
            root.renderMode = RenderMode.ScreenSpaceOverlay;
            root.sortingLayerID = 0;
            root.sortingOrder = Mathf.Max(100, root.sortingOrder);
            root.transform.localScale = Vector3.one;
            canvas.sortingLayerID = 0;
            canvas.overrideSorting = true;
            canvas.sortingOrder = Mathf.Max(300, canvas.sortingOrder);
            if (panel.GetComponent<GraphicRaycaster>() == null) panel.AddComponent<GraphicRaycaster>();
        }
    }
    public void PauseButtonPressed()
    {
        PausePanel.SetActive(true);
        ConfigurePanelLayers();
        GameManager.Instance.isPaused = true;
        Time.timeScale = 0f;
    }
    public void ContinueButtonPressed()
    {
        PausePanel.SetActive(false);
        GameManager.Instance.isPaused = false;
        Time.timeScale = 1f;
    }
    public void NextLevelButtonPressed()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isPaused = false;
        }

        LevelProgression progression = LevelProgression.Instance;
        if (progression != null && progression.AdvanceLevel())
        {
            GameRunMode.PrepareCampaign();
            LevelProgression.PrepareLevel(progression.CurrentLevel);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        SceneManager.LoadScene(0);
    }
    public void ChangeScene(int scene)
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.isPaused = false;
        if (scene == 1)
        {
            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                GameRunMode.PrepareCampaign();
                LevelProgression.PrepareSelectedLevel();
            }
            else if (LevelProgression.Instance != null)
            {
                GameRunMode.PrepareReplay();
                if (!GameRunMode.IsSurvival)
                    LevelProgression.PrepareLevel(LevelProgression.Instance.CurrentLevel);
            }
        }

        SceneManager.LoadScene(scene);
    }
    public void LosePressed()
    {
        GameManager.Instance.isPaused = true;
        if (survivalResultText != null)
        {
            bool survival = GameManager.Instance.IsSurvivalMode;
            survivalResultText.gameObject.SetActive(survival);
            if (survival)
            {
                survivalResultText.text = "ВЫ ПРОДЕРЖАЛИСЬ: " + GameManager.Instance.TurnNumber
                    + " ХОДОВ\nЛУЧШИЙ РЕЗУЛЬТАТ: " + GameRunMode.BestTurnsForCurrentMode;
            }
        }
        LosePanel.SetActive(true);
        ConfigurePanelLayers();
        Time.timeScale = 0f;
    }
    public void WinPressed()
    {
        GameManager.Instance.isPaused = true;
        WinPanel.SetActive(true);
        ConfigurePanelLayers();
        Time.timeScale = 0f;
    }
}
