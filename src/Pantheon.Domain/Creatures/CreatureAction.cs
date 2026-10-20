namespace Pantheon.Domain.Creatures;

/// <summary>What a Creature can do (spec §25, §111).</summary>
public enum CreatureAction
{
    /// <summary>Roams the land nearby.</summary>
    Wander,

    /// <summary>Lies down and sleeps.</summary>
    Rest,

    /// <summary>Eats wild plants and game where it stands.</summary>
    Forage,

    /// <summary>Eats from a settlement's stores: theft, from the villagers' side.</summary>
    EatFromStores,

    /// <summary>Gathers wild food around a settlement and gives it to the people.</summary>
    Feed,

    /// <summary>Works on the house a settlement is building.</summary>
    HelpBuild,

    /// <summary>Tends a settlement's fields: tills, weeds and waters them, and brings in a harvest (ADR-063).</summary>
    Farm,

    /// <summary>Uproots trees in the forest by a settlement and brings the timber to its stores (ADR-063).</summary>
    GatherWood,

    /// <summary>Plays with the villagers.</summary>
    Play,

    /// <summary>Attacks the villagers.</summary>
    Attack,

    /// <summary>Leaves a settlement to its fate and wanders off.</summary>
    Ignore,

    /// <summary>Walks where its god told it to go, and waits there.</summary>
    GoTo,

    /// <summary>Fights another Creature (spec §27–28, ADR-017).</summary>
    Fight,

    /// <summary>Plays with another Creature, or goes to find it to play.</summary>
    Befriend,

    /// <summary>Runs away from another Creature.</summary>
    Flee,
}
