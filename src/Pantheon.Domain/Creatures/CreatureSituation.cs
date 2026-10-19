namespace Pantheon.Domain.Creatures;

/// <summary>The situation in which a Creature chooses an action. Its learned preferences are kept per situation and action (spec §25).</summary>
public enum CreatureSituation
{
    /// <summary>Nothing presses.</summary>
    Idle,

    /// <summary>The Creature is hungry.</summary>
    Hungry,

    /// <summary>The Creature is tired.</summary>
    Tired,

    /// <summary>The Creature is bored.</summary>
    Bored,

    /// <summary>A settlement nearby is hungry or ill.</summary>
    VillageInNeed,

    /// <summary>A settlement nearby is building a house.</summary>
    VillageBuilding,

    /// <summary>A settlement nearby is doing well.</summary>
    VillageThriving,

    /// <summary>Its god told it where to go.</summary>
    Commanded,

    /// <summary>Another Creature is near, whose god is not at war with or hostile to its own (ADR-017).</summary>
    MeetsCreature,

    /// <summary>Another Creature is near, whose god is at war with or hostile to its own.</summary>
    MeetsEnemyCreature,
}
