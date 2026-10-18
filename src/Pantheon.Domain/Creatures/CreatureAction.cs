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

    /// <summary>Plays with the villagers.</summary>
    Play,

    /// <summary>Attacks the villagers.</summary>
    Attack,

    /// <summary>Leaves a settlement to its fate and wanders off.</summary>
    Ignore,

    /// <summary>Walks where its god told it to go, and waits there.</summary>
    GoTo,
}
