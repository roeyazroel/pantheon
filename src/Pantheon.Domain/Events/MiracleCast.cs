using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Events;

/// <summary>A god cast a miracle.</summary>
public sealed record MiracleCast : WorldEvent
{
    /// <summary>The casting god.</summary>
    public required int GodId { get; init; }

    /// <summary>The miracle.</summary>
    public required string MiracleId { get; init; }

    /// <summary>Where it was aimed.</summary>
    public required GroundPosition Target { get; init; }

    /// <summary>Divine Power it cost.</summary>
    public required float Cost { get; init; }

    /// <summary>Settlements it reached.</summary>
    public required IReadOnlyList<int> AffectedSettlementIds { get; init; }

    /// <summary>People it killed.</summary>
    public required int Deaths { get; init; }

    /// <summary>Whether it was cast in secret and not traced: others see a miracle, not who worked it (ADR-026).</summary>
    public bool Veiled { get; init; }
}
