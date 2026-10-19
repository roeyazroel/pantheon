using System.Collections.Generic;

namespace Pantheon.Domain.Creatures;

/// <summary>
/// Creatures' callings and the attributes that grow with level (spec §23–24, ADR-030), from
/// <c>creatures.json</c>'s <c>Callings</c>. Without the section no Creature takes up a path, and its
/// intelligence and resilience stay at its species' values. Divine affinity grows by
/// <c>DivineRules.CreatureAffinityPerLevel</c>, since it widens the god's influence.
/// </summary>
public sealed record CallingRules
{
    /// <summary>The levels at which a Creature takes up a path, in order: one path each, so no Creature walks them all.</summary>
    public IReadOnlyList<int> Levels { get; init; } = [];

    /// <summary>The paths each deed leads to, and how far.</summary>
    public IReadOnlyList<CallingDeed> Deeds { get; init; } = [];

    /// <summary>How much further a rewarded deed leads, on top of the deed itself.</summary>
    public float RewardCalling { get; init; } = 1f;

    /// <summary>How far a fight won leads towards <see cref="CreatureSpecialisation.Warrior"/>.</summary>
    public float VictoryCalling { get; init; } = 1.5f;

    /// <summary>How far sparing a beaten foe leads towards <see cref="CreatureSpecialisation.Guardian"/>.</summary>
    public float MercyCalling { get; init; } = 1f;

    /// <summary>Intelligence gained per level above the first: it learns faster.</summary>
    public float IntelligencePerLevel { get; init; }

    /// <summary>Resilience gained per level above the first: it takes less harm.</summary>
    public float ResiliencePerLevel { get; init; }

    /// <summary>How far a Guardian's and a Commander's care reaches, in metres.</summary>
    public float GuardRadius { get; init; } = 250f;

    /// <summary>Share of a disaster's harm a Guardian spares its god's villages near it.</summary>
    public float GuardianShield { get; init; }

    /// <summary>Extra share of combat power a Warrior has.</summary>
    public float WarriorPower { get; init; }

    /// <summary>Extra share of the people a Destroyer's attack kills.</summary>
    public float DestroyerAttack { get; init; }

    /// <summary>Health a Healer's deed in a village gives its people.</summary>
    public float HealerHealth { get; init; }

    /// <summary>Harm and blight a Nature Keeper's deed heals in its region and those beside it.</summary>
    public float KeeperHealing { get; init; }

    /// <summary>Share of a village's godless people a Missionary's deed wins to its god.</summary>
    public float MissionaryConversion { get; init; }

    /// <summary>Doubt a Trickster's deed sows in a rival god among the village's people.</summary>
    public float TricksterDoubt { get; init; }

    /// <summary>Defence a Commander gives its god's villages near it, against Creatures' attacks.</summary>
    public float CommanderDefence { get; init; }

    /// <summary>No callings, and attributes that do not grow.</summary>
    public static CallingRules None { get; } = new();
}

/// <summary>A deed and the path it leads to.</summary>
public sealed record CallingDeed
{
    /// <summary>The deed.</summary>
    public required CreatureAction Action { get; init; }

    /// <summary>The path.</summary>
    public required CreatureSpecialisation Specialisation { get; init; }

    /// <summary>How far each deed leads.</summary>
    public required float Weight { get; init; }
}
