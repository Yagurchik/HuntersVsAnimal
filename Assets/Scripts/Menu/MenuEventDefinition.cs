using UnityEngine;

[CreateAssetMenu(menuName = "Forest Animal War/UI/Event", fileName = "MenuEvent")]
public class MenuEventDefinition : ScriptableObject
{
    [Tooltip("Permanent unique save ID. Do not change it after release.")]
    public string saveId;
    public string displayName;
    [TextArea(2, 5)] public string description;
    [Min(1)] public int requiredVictories = 1;
    [Min(1)] public int acornReward = 5;
}
