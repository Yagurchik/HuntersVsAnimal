using System;
using UnityEngine;

public enum ArtifactStat
{
    DamagePct,
    FlatDamage,
    ProjectileHealthPct,
    ProjectileSpeedPct,
    BarricadeHealthFlat,
    DefenseMitigationPct,
    PassiveGoldPct,
    PassiveAcornsPct,
    RoomGoldPct,
    VictoryAcornsPct,
    FireflyBurnPct,
    FireDamagePct,
    PoisonDamagePct,
    HedgehogDamagePct,
    HoneyBadgerDamagePct,
    BoarDamagePct,
    RabbitDamagePct,
    BeaverDamagePct,
    FrogDamagePct,
    SquirrelDamagePct,
    FireflyLightRadiusBonus,
    HedgehogExplosionRadiusBonus,
    HoneyBadgerJumpRadiusBonus,
    FrogJumpRadiusBonus,
    FrogChorusBonusPerFrog,
    BeaverConversionBonus,
    SquirrelEarlyChanceBonus,
    SquirrelExplosionRadiusBonus,
    CriticalChance,
    LifeStealPct,
    StunChance,
    LightningChance,

    // Generic on-hit effects. Earlier values keep their serialized indices.
    SplashChance,
    SplashDamagePct,
    SplashRadiusBonus,
    ChainStrikeChance,
    ChainStrikeDamagePct,
    ChainStrikeRadiusBonus,
    EchoChance,
    EchoDamagePct,
    PoisonOnHitPct,
    DefenseConversionPct,
    SeedPlantChance,
    SeedEarlyChance,
    SeedDamagePct,
    SeedRadiusBonus
}

[Serializable] public struct ArtifactBonus
{
    public ArtifactStat Stat;
    public float Value;
}
[Serializable] public struct ArtifactIngredient
{
    public string Id;
    [Min(1)] public int Amount;
}

// Authored assets hold the complete shop catalogue, including icons and recipes.
[CreateAssetMenu(menuName = "Forest Animal War/Artifact")]
public sealed class MetaArtifactDefinition : ScriptableObject
{
    public string Id;
    public string Name;
    [TextArea(3, 8)] public string Description;
    public string Mark;
    public string Category;
    [Min(1)] public int GoldCost;
    public bool IsAssembled;
    public Sprite Icon;
    public ArtifactIngredient[] Ingredients = Array.Empty<ArtifactIngredient>();
    public ArtifactBonus[] Bonuses = Array.Empty<ArtifactBonus>();
}
