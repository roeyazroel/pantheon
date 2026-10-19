using System.Collections.Generic;

namespace Pantheon.Domain.Villagers;

/// <summary>
/// Followed villagers (spec §80, ADR-043), from <c>settlement_rules.json</c>'s <c>Villagers</c>.
/// Without names no villager can be followed.
/// </summary>
public sealed record VillagerRules
{
    /// <summary>No followed villagers.</summary>
    public static VillagerRules None { get; } = new();

    /// <summary>Whether villagers can be followed.</summary>
    public bool Enabled => Names.Count > 0;

    /// <summary>The names villagers are given.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>Simulated seconds in a year of a villager's life.</summary>
    public float SecondsPerYear { get; init; } = 90f;

    /// <summary>The youngest age a followed villager has.</summary>
    public float MinAge { get; init; } = 16f;

    /// <summary>The oldest age a followed villager has.</summary>
    public float MaxAge { get; init; } = 60f;

    /// <summary>The shortest life.</summary>
    public float MinLifeYears { get; init; } = 58f;

    /// <summary>The longest life.</summary>
    public float MaxLifeYears { get; init; } = 86f;

    /// <summary>Walking speed, in metres per second, before roads.</summary>
    public float Speed { get; init; } = 1.4f;

    /// <summary>How far from its centre, in metres, a villager is still in their village.</summary>
    public float VillageRadius { get; init; } = 60f;

    /// <summary>How far from its centre a followed villager's home, work and shrine may lie.</summary>
    public float SpotRange { get; init; } = 150f;

    /// <summary>Hunger gained per second.</summary>
    public float HungerPerSecond { get; init; } = 0.0035f;

    /// <summary>Hunger eaten away per second while eating from the stores.</summary>
    public float EatPerSecond { get; init; } = 0.04f;

    /// <summary>Weariness gained per second of work; half as much walking.</summary>
    public float FatiguePerSecond { get; init; } = 0.003f;

    /// <summary>Weariness rested away per second.</summary>
    public float RestPerSecond { get; init; } = 0.012f;

    /// <summary>How much they want to work when fresh and fed.</summary>
    public float WorkDrive { get; init; } = 0.5f;

    /// <summary>How much they want to pray, by their faith.</summary>
    public float WorshipDrive { get; init; } = 0.6f;

    /// <summary>Seconds after praying before they want to pray as much again.</summary>
    public float WorshipGapSeconds { get; init; } = 180f;

    /// <summary>How much more they want to pray after a hardship they remember.</summary>
    public float HardshipWorship { get; init; } = 0.25f;

    /// <summary>How long, in seconds, a hardship drives them to pray.</summary>
    public float HardshipSeconds { get; init; } = 300f;

    /// <summary>Seconds of work.</summary>
    public float WorkSeconds { get; init; } = 60f;

    /// <summary>Seconds of a meal.</summary>
    public float EatSeconds { get; init; } = 15f;

    /// <summary>Seconds of rest.</summary>
    public float RestSeconds { get; init; } = 40f;

    /// <summary>Seconds of prayer.</summary>
    public float WorshipSeconds { get; init; } = 25f;

    /// <summary>Health lost per second while starving.</summary>
    public float StarvePerSecond { get; init; } = 0.004f;

    /// <summary>Health recovered per second while fed and well.</summary>
    public float HealPerSecond { get; init; } = 0.002f;

    /// <summary>Health lost per second by the share of the village sick with the plague.</summary>
    public float PlagueHarmPerSecond { get; init; } = 0.004f;

    /// <summary>The danger at which they flee.</summary>
    public float FleeDanger { get; init; } = 0.25f;

    /// <summary>The share of <see cref="FleeDanger"/> at which one who remembers danger flees.</summary>
    public float WaryFlee { get; init; } = 0.6f;

    /// <summary>The farthest village, in metres, they flee to.</summary>
    public float FleeRange { get; init; } = 800f;

    /// <summary>How far, in metres, they flee when no village is safe.</summary>
    public float FleeDistance { get; init; } = 80f;

    /// <summary>Chance of death per second by the danger where they are.</summary>
    public float Mortality { get; init; } = 0.01f;

    /// <summary>The share per second by which their faith follows their village's.</summary>
    public float FaithDrift { get; init; } = 0.01f;

    /// <summary>Faith gained per second of prayer.</summary>
    public float WorshipFaith { get; init; } = 0.004f;

    /// <summary>How far, in metres, they see a miracle.</summary>
    public float WitnessRadius { get; init; } = 150f;

    /// <summary>Faith their own god's miracle or prophet gives them, and half of which another god's takes away.</summary>
    public float WitnessFaith { get; init; } = 0.05f;

    /// <summary>The faith below which they turn to the god most of their village follows.</summary>
    public float ConvertFaith { get; init; } = 0.3f;

    /// <summary>Most memories they keep.</summary>
    public int MemoryCapacity { get; init; } = 24;
}
