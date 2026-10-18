using System.Collections.Generic;

namespace Pantheon.Domain.Diplomacy;

/// <summary>
/// How one god sees another (spec §37, §90): directional, so a god may respect, fear and distrust
/// a rival who sees it quite differently. Every value lies in 0..1.
/// </summary>
public sealed class GodRelation
{
    /// <summary>The god whose view this is.</summary>
    public required int ObserverGodId { get; init; }

    /// <summary>The god it regards.</summary>
    public required int SubjectGodId { get; init; }

    /// <summary>How far it trusts the other to keep its word (0.5 is neutral).</summary>
    public required float Trust { get; set; }

    /// <summary>How much it fears the other's power.</summary>
    public required float Fear { get; set; }

    /// <summary>How much it respects the other's standing.</summary>
    public required float Respect { get; set; }

    /// <summary>How much it sees the other as a competitor.</summary>
    public required float Rivalry { get; set; }

    /// <summary>How much it owes the other for help.</summary>
    public required float Gratitude { get; set; }

    /// <summary>How much it suspects the other's intentions.</summary>
    public required float Suspicion { get; set; }

    /// <summary>What it remembers the other did, oldest first.</summary>
    public required List<DiplomaticMemory> Memories { get; init; }
}
