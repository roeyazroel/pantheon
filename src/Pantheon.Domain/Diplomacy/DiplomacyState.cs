using System.Collections.Generic;

namespace Pantheon.Domain.Diplomacy;

/// <summary>The relations between all gods (spec §37–41, ADR-012).</summary>
public sealed class DiplomacyState
{
    /// <summary>How each god sees each other god: one entry per ordered pair.</summary>
    public required List<GodRelation> Relations { get; init; }

    /// <summary>The status of each pair of gods: one entry per unordered pair.</summary>
    public required List<GodStance> Stances { get; init; }

    /// <summary>Proposals waiting for an answer.</summary>
    public required List<DiplomaticProposal> Proposals { get; init; }
}
