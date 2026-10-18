namespace Pantheon.Domain.Diplomacy;

/// <summary>The status between two gods (spec §113), shared by both. <see cref="GodA"/> has the lower id.</summary>
public sealed class GodStance
{
    /// <summary>The god with the lower id.</summary>
    public required int GodA { get; init; }

    /// <summary>The god with the higher id.</summary>
    public required int GodB { get; init; }

    /// <summary>Their status.</summary>
    public required DiplomaticStatus Status { get; set; }

    /// <summary>Tick at which the status began.</summary>
    public required long SinceTick { get; set; }

    /// <summary>Tick of the latest proposal between them, if any; proposals must be spaced (spec §59: no spam).</summary>
    public required long? LastProposalTick { get; set; }
}
