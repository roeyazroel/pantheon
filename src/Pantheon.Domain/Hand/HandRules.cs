using Pantheon.Domain.Belief;

namespace Pantheon.Domain.Hand;

/// <summary>
/// What the Divine Hand can do and what people make of it (spec §55, ADR-030), from
/// <c>divine_rules.json</c>'s <c>Hand</c>. Without the section the hand lifts nothing.
/// </summary>
public sealed record HandRules
{
    /// <summary>Whether gods have hands at all.</summary>
    public bool Enabled { get; init; }

    /// <summary>The god's influence the point needs, as for a miracle.</summary>
    public float MinInfluence { get; init; } = 0.05f;

    /// <summary>How near a settlement or the Creature the hand must reach to lift it, in metres.</summary>
    public float Reach { get; init; } = 60f;

    /// <summary>The most people the hand lifts at once.</summary>
    public int VillagersPerGrab { get; init; } = 10;

    /// <summary>The largest share of a settlement's people the hand lifts at once.</summary>
    public float MaxShare { get; init; } = 0.25f;

    /// <summary>Land at least this green has trees to pull; barer land gives rocks.</summary>
    public float TreeVegetation { get; init; } = 0.6f;

    /// <summary>Vegetation a tree takes from where it is pulled and gives where it is planted.</summary>
    public float TreeCover { get; init; } = 0.05f;

    /// <summary>How far a thrown rock wrecks, in metres; a tree or a body reaches as far.</summary>
    public float ThrowRadius { get; init; } = 25f;

    /// <summary>Share of the houses within reach a thrown rock destroys, at the centre.</summary>
    public float ThrowHousingLoss { get; init; } = 0.15f;

    /// <summary>Share of the people within reach a thrown rock kills, at the centre.</summary>
    public float ThrowDeaths { get; init; } = 0.04f;

    /// <summary>How much a thrown tree does of what a rock does.</summary>
    public float TreeThrowFactor { get; init; } = 0.6f;

    /// <summary>Harm a thrown rock does to the land it strikes (ADR-028).</summary>
    public float ThrowLandHarm { get; init; } = 0.3f;

    /// <summary>Share of its health a thrown Creature loses.</summary>
    public float ThrownCreatureHealth { get; init; } = 0.15f;

    /// <summary>How far people see what the hand does, in metres.</summary>
    public float WitnessRadius { get; init; } = 300f;

    /// <summary>What villagers lifted out of danger and set down safe, and those who saw it, feel.</summary>
    public BeliefImpulse Rescue { get; init; } = new() { Faith = 0.05f, Love = 0.15f, Fear = 0f, Doubt = -0.05f };

    /// <summary>What villagers lifted from a safe home, and those left behind, feel.</summary>
    public BeliefImpulse Abduction { get; init; } = new() { Faith = 0f, Love = -0.05f, Fear = 0.08f, Doubt = 0.05f };

    /// <summary>What a village struck by something thrown, and those who saw it, feel.</summary>
    public BeliefImpulse Smiting { get; init; } = new() { Faith = 0.02f, Love = -0.1f, Fear = 0.2f, Doubt = 0f };

    /// <summary>What a village sees when a tree is planted beside it.</summary>
    public BeliefImpulse Planting { get; init; } = new() { Faith = 0.01f, Love = 0.03f, Fear = 0f, Doubt = 0f };

    /// <summary>No hand.</summary>
    public static HandRules None { get; } = new();
}
