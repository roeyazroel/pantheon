namespace Pantheon.Domain.Disasters;

/// <summary>A tornado on the ground (ADR-029).</summary>
public sealed class Tornado
{
    /// <summary>Unique id among the world's hazards.</summary>
    public required int Id { get; init; }

    /// <summary>East-west position in metres.</summary>
    public required float X { get; set; }

    /// <summary>North-south position in metres.</summary>
    public required float Z { get; set; }

    /// <summary>East-west velocity in metres per second.</summary>
    public required float VelocityX { get; init; }

    /// <summary>North-south velocity in metres per second.</summary>
    public required float VelocityZ { get; init; }

    /// <summary>The tick it lifts.</summary>
    public required long EndsAtTick { get; init; }
}

/// <summary>A swarm of locusts on the wing (ADR-029).</summary>
public sealed class LocustSwarm
{
    /// <summary>Unique id among the world's hazards.</summary>
    public required int Id { get; init; }

    /// <summary>East-west position in metres.</summary>
    public required float X { get; set; }

    /// <summary>North-south position in metres.</summary>
    public required float Z { get; set; }

    /// <summary>How big it is (0..1); rain thins it.</summary>
    public required float Size { get; set; }

    /// <summary>The tick it dies away.</summary>
    public required long EndsAtTick { get; init; }
}

/// <summary>The island's volcano (ADR-029): its peak and what it is doing.</summary>
public sealed class Volcano
{
    /// <summary>The region of its crater.</summary>
    public required int RegionId { get; init; }

    /// <summary>What it is doing.</summary>
    public required VolcanoPhase Phase { get; set; }

    /// <summary>The tick its phase ends, or it can wake again after an eruption.</summary>
    public required long PhaseEndsTick { get; set; }

    /// <summary>The tick of its next tremor while it rumbles.</summary>
    public required long NextTremorTick { get; set; }
}

/// <summary>What a volcano is doing.</summary>
public enum VolcanoPhase
{
    /// <summary>Asleep.</summary>
    Dormant,

    /// <summary>Rumbling: tremors shake the land around it.</summary>
    Rumbling,

    /// <summary>Erupting: lava runs down its slope and ash falls around it.</summary>
    Erupting,
}

/// <summary>A mark a disaster leaves on the land (spec §64, ADR-029), shown until it fades.</summary>
public sealed class TerrainMark
{
    /// <summary>What left it.</summary>
    public required TerrainMarkKind Kind { get; init; }

    /// <summary>East-west centre in metres.</summary>
    public required float X { get; init; }

    /// <summary>North-south centre in metres.</summary>
    public required float Z { get; init; }

    /// <summary>Radius in metres.</summary>
    public required float Radius { get; init; }

    /// <summary>The tick it was made.</summary>
    public required long Tick { get; init; }
}

/// <summary>What left a mark on the land.</summary>
public enum TerrainMarkKind
{
    /// <summary>Cracks an earthquake opened.</summary>
    Crack,

    /// <summary>The scar of a landslide.</summary>
    Slide,

    /// <summary>A meteor's crater.</summary>
    Crater,
}
