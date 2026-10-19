namespace Pantheon.Domain.Roads;

/// <summary>
/// How villages build roads to their neighbours and what roads do (spec §29, ADR-033). <see cref="None"/>
/// builds none.
/// </summary>
public sealed record RoadRules
{
    /// <summary>No roads.</summary>
    public static RoadRules None { get; } = new()
    {
        MinPopulation = int.MaxValue, Range = 0f, WorkShare = 0f, MetresPerWorkerPerSecond = 0f, StonePerMetre = 0f, Speed = 1f,
        Width = 0f, TradeWealthPerSecond = 0f, FoodReliefPerSecond = 0f, HungerSteps = 0f, WearPerSecond = 0f, RepairPerSecond = 0f,
        CutWear = 1f, DecayPerSecond = 0f,
    };

    /// <summary>Whether villages build roads at all.</summary>
    public bool Enabled => Range > 0f && WorkShare > 0f;

    /// <summary>People a village needs before it builds roads.</summary>
    public required int MinPopulation { get; init; }

    /// <summary>The farthest neighbour, in metres, a village builds a road to.</summary>
    public required float Range { get; init; }

    /// <summary>Share of the workforce that works on the village's road.</summary>
    public required float WorkShare { get; init; }

    /// <summary>Metres of road one worker builds per second.</summary>
    public required float MetresPerWorkerPerSecond { get; init; }

    /// <summary>Stone each metre takes from the builders' stores.</summary>
    public required float StonePerMetre { get; init; }

    /// <summary>Speed of walkers on a whole road, as a factor.</summary>
    public required float Speed { get; init; }

    /// <summary>How near the road, in metres, a walker is on it.</summary>
    public required float Width { get; init; }

    /// <summary>Wealth a whole road brings each of its villages per second.</summary>
    public required float TradeWealthPerSecond { get; init; }

    /// <summary>Food a whole road carries per second from a village with food to spare to a hungry one.</summary>
    public required float FoodReliefPerSecond { get; init; }

    /// <summary>A village is hungry with fewer steps of food than this in store, and has food to spare with three times as many.</summary>
    public required float HungerSteps { get; init; }

    /// <summary>Wear per second from the worst of flood, fire and rubble on the way, at full strength.</summary>
    public required float WearPerSecond { get; init; }

    /// <summary>Wear its villages mend per second.</summary>
    public required float RepairPerSecond { get; init; }

    /// <summary>Wear at which a road is cut.</summary>
    public required float CutWear { get; init; }

    /// <summary>Wear per second of a road whose village is gone; it vanishes at full wear.</summary>
    public required float DecayPerSecond { get; init; }
}
