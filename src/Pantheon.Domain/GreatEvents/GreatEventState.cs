using System.Collections.Generic;

namespace Pantheon.Domain.GreatEvents;

/// <summary>A kind of great event (spec §52's world events, ADR-034).</summary>
public enum GreatEventKind
{
    /// <summary>A plague that has reached several villages at once.</summary>
    Plague,

    /// <summary>The sun goes dark for a while.</summary>
    Eclipse,

    /// <summary>A god's doubting faith reforms itself.</summary>
    Reformation,

    /// <summary>Most of the gods are at war at once.</summary>
    WorldWar,

    /// <summary>The whole island dries out in a long dry spell.</summary>
    GreatDrought,

    /// <summary>Many of the island's people are on the road at once.</summary>
    GreatMigration,
}

/// <summary>A great event under way (ADR-034): what it is, its name, when it began and whom it concerns.</summary>
public sealed class GreatEventState
{
    /// <summary>Unique id, below <see cref="GreatEventsState.NextId"/>.</summary>
    public required int Id { get; init; }

    /// <summary>What it is.</summary>
    public required GreatEventKind Kind { get; init; }

    /// <summary>Its name, for example "the Grey Plague".</summary>
    public required string Name { get; init; }

    /// <summary>When it began.</summary>
    public required long StartTick { get; init; }

    /// <summary>When it ends, for one with a set length (an eclipse); <c>null</c> while it lasts as long as its cause.</summary>
    public long? EndTick { get; init; }

    /// <summary>The god it concerns (a reformation's), if any.</summary>
    public int? GodId { get; init; }

    /// <summary>The most villages (a plague's), gods (a war's) or people (a migration's) it has reached.</summary>
    public int Peak { get; set; }
}

/// <summary>The world's great events (ADR-034): those under way, the next id, and when the sun goes dark next.</summary>
public sealed class GreatEventsState
{
    /// <summary>The great events under way, by id.</summary>
    public List<GreatEventState> Events { get; init; } = [];

    /// <summary>Id the next great event will get.</summary>
    public int NextId { get; set; } = 1;

    /// <summary>When the next eclipse begins; 0 until the first step sets it from the seed.</summary>
    public long NextEclipseTick { get; set; }

    /// <summary>When each god last reformed its faith, by god id, so a faith reforms at most so often.</summary>
    public Dictionary<int, long> LastReformations { get; init; } = [];

    /// <summary>Since when the whole land has been dry enough for a great drought; <c>null</c> while it is not.</summary>
    public long? DrySinceTick { get; set; }
}
