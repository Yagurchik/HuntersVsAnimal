using UnityEngine;
using UnityEngine.UI;

public class MenuLevelCard : MonoBehaviour
{
    [Range(1, LevelProgression.LevelsPerLocation)] public int levelNumber = 1;
    public MenuHubController hub;
    public Image background;
    public Text numberText;
    public Text stateText;
    public Color selectedColor = new Color(0.78f, 0.54f, 0.19f);
    public Color availableColor = new Color(0.16f, 0.3f, 0.26f);
    public Color lockedColor = new Color(0.1f, 0.16f, 0.15f);

    public void SelectLevel()
    {
        if (!Application.isPlaying || hub == null)
        {
            return;
        }

        hub.SelectCampaignLevel(levelNumber);
    }

    public void Refresh() => Refresh(hub != null && hub.SelectedLocationIndex > 0
        ? hub.SelectedLocationIndex
        : LevelProgression.SelectedMenuLocation);

    public void Refresh(int locationIndex)
    {
        int globalLevel = LevelProgression.GlobalLevelFor(locationIndex, levelNumber);
        bool available = GameRunMode.DeveloperModeEnabled
            || globalLevel <= LevelProgression.HighestUnlockedLevel;
        bool selected = globalLevel == LevelProgression.SelectedMenuLevel;
        if (background != null)
        {
            background.color = selected ? selectedColor : available ? availableColor : lockedColor;
        }

        if (numberText != null)
        {
            numberText.text = levelNumber.ToString("00");
        }

        if (stateText != null)
        {
            stateText.text = selected ? "ВЫБРАН" : available ? "ОТКРЫТ" : "ЗАКРЫТ";
        }
    }
}
