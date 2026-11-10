using System;
using UnityEngine;

public class ExperienceEconomy : MonoBehaviour
{
    public const int DefaultFirstUpgradeExperience = 30;
    public const int DefaultAdditionalExperiencePerUpgrade = 4;
    public const int DefaultMaximumUpgradeExperience = 90;

    [Header("Upgrade cost")]
    [SerializeField, Min(1), Tooltip("Experience required to earn the first upgrade in a run.")]
    private int firstUpgradeExperience = DefaultFirstUpgradeExperience;
    [SerializeField, Min(0), Tooltip("Extra experience added to the price after every upgrade earned through XP.")]
    private int additionalExperiencePerUpgrade = DefaultAdditionalExperiencePerUpgrade;
    [SerializeField, Min(1), Tooltip("Maximum XP price. Free line-clear upgrades do not raise this price.")]
    private int maximumUpgradeExperience = DefaultMaximumUpgradeExperience;

    private int currentExperience;
    private int earnedUpgrades;
    private int experienceUpgrades;

    public event Action Changed;

    public int CurrentExperience => currentExperience;
    public int EarnedUpgrades => earnedUpgrades;
    public int ExperienceForNextUpgrade => Mathf.Clamp(
        firstUpgradeExperience + experienceUpgrades * additionalExperiencePerUpgrade,
        1, Mathf.Max(1, maximumUpgradeExperience));

    public void GrantUpgradeLevels(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        earnedUpgrades += amount;
        Changed?.Invoke();
    }

    public int AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        currentExperience += amount;
        int newUpgrades = 0;

        while (currentExperience >= ExperienceForNextUpgrade)
        {
            int requiredExperience = ExperienceForNextUpgrade;
            currentExperience -= requiredExperience;
            experienceUpgrades++;
            earnedUpgrades++;
            newUpgrades++;
        }

        Changed?.Invoke();
        return newUpgrades;
    }

}
