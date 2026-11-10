using System;
using UnityEngine;

// Rebuild once after a purchase/equipment change, rather than reading saves on every bite.
public static class ArtifactEquipmentBonuses
{
    private static readonly float[] values = new float[Enum.GetValues(typeof(ArtifactStat)).Length];
    private static bool dirty = true;
    public static void Invalidate() => dirty = true;

    public static float Get(ArtifactStat stat)
    {
        if (dirty)
        {
            Array.Clear(values, 0, values.Length);
            for (int slot = 0; slot < MetaProgression.EquipmentSlotCount; slot++)
            {
                var item = MetaProgression.GetArtifactDefinition(MetaProgression.GetEquippedArtifact(slot));
                if (item == null) continue;
                foreach (var bonus in item.Bonuses) values[(int)bonus.Stat] += bonus.Value;
            }
            dirty = false;
        }
        return values[(int)stat];
    }
    public static float Multiplier(ArtifactStat stat) => 1f + Get(stat);
    public static float DefenseDamageMultiplier => 1f - Mathf.Clamp(Get(ArtifactStat.DefenseMitigationPct), 0f, 0.6f);
    public static float CriticalChance => Mathf.Clamp(Get(ArtifactStat.CriticalChance), 0f, 0.5f);
    public static float StunChance => Mathf.Clamp(Get(ArtifactStat.StunChance), 0f, 0.25f);
    public static float LightningChance => Mathf.Clamp(Get(ArtifactStat.LightningChance), 0f, 0.3f);

    public static float SplashChance => Mathf.Clamp(Get(ArtifactStat.SplashChance), 0f, 0.5f);
    public static float ChainStrikeChance => Mathf.Clamp(Get(ArtifactStat.ChainStrikeChance), 0f, 0.4f);
    public static float EchoChance => Mathf.Clamp(Get(ArtifactStat.EchoChance), 0f, 0.4f);
    public static float SeedPlantChance => Mathf.Clamp(Get(ArtifactStat.SeedPlantChance), 0f, 0.25f);
    public static float SeedEarlyChance => Mathf.Clamp(Get(ArtifactStat.SeedEarlyChance), 0f, 0.5f);
    public static float SplashRadius => 1f + Get(ArtifactStat.SplashRadiusBonus);
    public static float ChainStrikeRadius => 2f + Get(ArtifactStat.ChainStrikeRadiusBonus);
    public static float Fraction(ArtifactStat stat) => Mathf.Clamp01(Get(stat));
}
