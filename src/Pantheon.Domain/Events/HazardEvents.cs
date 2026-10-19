using Pantheon.Domain.World;

namespace Pantheon.Domain.Events;

/// <summary>An earthquake near the coast raised a wave that flooded the land (ADR-029).</summary>
public sealed record TsunamiStruck : WorldEvent
{
    /// <summary>Where the wave came ashore.</summary>
    public required GroundPosition Shore { get; init; }

    /// <summary>The region where it lies deepest.</summary>
    public required int RegionId { get; init; }
}

/// <summary>A slope gave way and buried the land below it (ADR-029).</summary>
public sealed record LandslideStruck : WorldEvent
{
    /// <summary>The region that slid.</summary>
    public required int RegionId { get; init; }

    /// <summary>The region below it that it buried.</summary>
    public required int ToRegionId { get; init; }
}

/// <summary>The volcano woke, erupted or fell asleep (ADR-029).</summary>
public sealed record VolcanoChanged : WorldEvent
{
    /// <summary>The region of its crater.</summary>
    public required int RegionId { get; init; }

    /// <summary>What it does now.</summary>
    public required Disasters.VolcanoPhase Phase { get; init; }
}

/// <summary>A thunderstorm spun a tornado down (ADR-029).</summary>
public sealed record TornadoTouchedDown : WorldEvent
{
    /// <summary>The tornado.</summary>
    public required int TornadoId { get; init; }

    /// <summary>Where it touched down.</summary>
    public required GroundPosition Position { get; init; }
}

/// <summary>A meteor struck the land (ADR-029).</summary>
public sealed record MeteorStruck : WorldEvent
{
    /// <summary>Where it struck.</summary>
    public required GroundPosition Position { get; init; }
}

/// <summary>Blight took hold of a village's fields, or left them (ADR-029).</summary>
public sealed record BlightChanged : WorldEvent
{
    /// <summary>The village.</summary>
    public required int SettlementId { get; init; }

    /// <summary><c>true</c> when it took hold, <c>false</c> when it left.</summary>
    public required bool Started { get; init; }

    /// <summary>The mean blight of its fields now (0..1).</summary>
    public required float Blight { get; init; }
}

/// <summary>A locust swarm reached a village (ADR-029).</summary>
public sealed record SwarmArrived : WorldEvent
{
    /// <summary>The village.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The swarm.</summary>
    public required int SwarmId { get; init; }
}
