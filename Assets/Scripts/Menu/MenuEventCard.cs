using UnityEngine;
using UnityEngine.UI;

public class MenuEventCard : MonoBehaviour
{
    public MenuEventDefinition definition;
    public MenuHubController hub;
    public Text titleText;
    public Text descriptionText;
    public Text progressText;
    public Text rewardText;
    public Text actionText;
    public Button claimButton;
    public Image progressFill;

    public void Claim()
    {
        if (!Application.isPlaying || definition == null || hub == null) return;
        if (MetaProgression.TryClaimEvent(definition))
            hub.ShowInformation("Награда получена", "+" + definition.acornReward + " желудей\n\n" + definition.displayName);
        Refresh();
    }

    public void Refresh()
    {
        if (definition == null) return;
        bool claimed = MetaProgression.IsEventClaimed(definition.saveId);
        int target = Mathf.Max(1, definition.requiredVictories);
        int progress = Mathf.Min(MetaProgression.TotalVictories, target);
        if (titleText != null) titleText.text = definition.displayName;
        if (descriptionText != null) descriptionText.text = definition.description;
        if (progressText != null) progressText.text = "ПОБЕДЫ: " + progress + " / " + target;
        if (rewardText != null) rewardText.text = "+" + definition.acornReward + " ЖЕЛУДЕЙ";
        if (progressFill != null) progressFill.fillAmount = progress / (float)target;
        if (actionText != null) actionText.text = claimed ? "ПОЛУЧЕНО" : progress >= target ? "ЗАБРАТЬ" : "В ПРОЦЕССЕ";
        if (claimButton != null) claimButton.interactable = !claimed && progress >= target;
    }
}
