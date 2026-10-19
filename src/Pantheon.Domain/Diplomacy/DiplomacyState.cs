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

    /// <summary>Treaties in force (spec §38–39, ADR-025).</summary>
    public required List<Politics.Treaty> Treaties { get; init; }

    /// <summary>The id the next treaty receives.</summary>
    public required int NextTreatyId { get; set; }

    /// <summary>Favours owed (spec §41).</summary>
    public required List<Politics.Favour> Favours { get; init; }

    /// <summary>Promises not yet kept.</summary>
    public required List<Politics.Obligation> Obligations { get; init; }

    /// <summary>The Divine Council (spec §42).</summary>
    public required Politics.CouncilState Council { get; init; }

    /// <summary>Relations before any god has met another: no views, statuses, proposals, treaties, favours or promises, and a council yet to meet.</summary>
    /// <returns>The state.</returns>
    public static DiplomacyState Empty() => new()
    {
        Relations = [],
        Stances = [],
        Proposals = [],
        Treaties = [],
        NextTreatyId = 1,
        Favours = [],
        Obligations = [],
        Council = Politics.CouncilState.Founded(),
    };
}
