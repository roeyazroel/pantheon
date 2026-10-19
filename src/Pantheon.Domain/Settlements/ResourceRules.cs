namespace Pantheon.Domain.Settlements;

/// <summary>
/// Wood and stone (spec §5, §29, §93, ADR-033): how villages gather them from the land around, how
/// the land's timber grows back and burns, and what a house takes. <see cref="None"/> gathers
/// nothing and builds houses without wood.
/// </summary>
public sealed record ResourceRules
{
    /// <summary>No gathering: houses take no wood, and the land's timber neither grows nor burns.</summary>
    public static ResourceRules None { get; } = new()
    {
        GatherShare = 0f, GatherRadius = 0, WoodPerGathererPerStep = 0f, StonePerGathererPerStep = 0f, WoodPerHousing = 0f,
        StonePerHousing = 0f, MinWoodStore = 0f, MinStoneStore = 0f, WoodPerHouse = 0f, TimberPerWood = 0f, StonePerStone = 0f,
        TimberRegrowthPerSecond = 0f, TimberBurnPerSecond = 0f, ClearedWildlife = 0f, ClearedLandslide = 0f,
    };

    /// <summary>Whether villages gather at all.</summary>
    public bool Enabled => GatherShare > 0f;

    /// <summary>The most of the workforce that gathers, when both stores are empty; fewer as they fill.</summary>
    public required float GatherShare { get; init; }

    /// <summary>The land gathered from, in regions around the village's own.</summary>
    public required int GatherRadius { get; init; }

    /// <summary>Wood one gatherer brings in per step from fully wooded land.</summary>
    public required float WoodPerGathererPerStep { get; init; }

    /// <summary>Stone one gatherer brings in per step from land full of stone.</summary>
    public required float StonePerGathererPerStep { get; init; }

    /// <summary>Wood a village wants in store per place of housing.</summary>
    public required float WoodPerHousing { get; init; }

    /// <summary>Stone a village wants in store per place of housing.</summary>
    public required float StonePerHousing { get; init; }

    /// <summary>Wood a village wants in store at least, however small.</summary>
    public required float MinWoodStore { get; init; }

    /// <summary>Stone a village wants in store at least, however small.</summary>
    public required float MinStoneStore { get; init; }

    /// <summary>Wood a new house takes when it is begun (a ruin rebuilt takes its share of the work as much).</summary>
    public required float WoodPerHouse { get; init; }

    /// <summary>Timber the land loses per unit of wood cut, shared among the regions cut.</summary>
    public required float TimberPerWood { get; init; }

    /// <summary>Stone the land loses per unit quarried, shared among the regions quarried.</summary>
    public required float StonePerStone { get; init; }

    /// <summary>Share of the gap to its forest that the land's timber grows back per second.</summary>
    public required float TimberRegrowthPerSecond { get; init; }

    /// <summary>Timber burnt per second per unit of fire.</summary>
    public required float TimberBurnPerSecond { get; init; }

    /// <summary>Share of its wildlife a fully cleared forest no longer holds.</summary>
    public required float ClearedWildlife { get; init; }

    /// <summary>Extra landslide chance on a fully cleared slope, as a factor (0 for none).</summary>
    public required float ClearedLandslide { get; init; }
}
