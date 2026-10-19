namespace Pantheon.Domain.Conflict;

/// <summary>What a god goes to war for (spec §44, ADR-026).</summary>
public enum WarGoal
{
    /// <summary>To win the enemy's villages: conversions count most.</summary>
    Conquest,

    /// <summary>To take a holy site the enemy keeps.</summary>
    HolySite,

    /// <summary>To make the enemy pay tribute.</summary>
    Tribute,

    /// <summary>To defeat the enemy's Creature.</summary>
    Humiliation,
}

/// <summary>A war's aim: its goal, and the holy site it is for.</summary>
/// <param name="Goal">The goal.</param>
/// <param name="SiteId">The holy site, for <see cref="WarGoal.HolySite"/>.</param>
public sealed record WarAim(WarGoal Goal, int? SiteId = null);
