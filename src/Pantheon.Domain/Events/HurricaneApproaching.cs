namespace Pantheon.Domain.Events;

/// <summary>A hurricane has formed at the edge of the world and blows towards the island (spec §59's example, ADR-041).</summary>
public sealed record HurricaneApproaching : WorldEvent
{
    /// <summary>The weather cell.</summary>
    public required int CellId { get; init; }

    /// <summary>Where it is, in metres east.</summary>
    public required float X { get; init; }

    /// <summary>Where it is, in metres south.</summary>
    public required float Z { get; init; }
}
