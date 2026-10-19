using System.Collections.Generic;

namespace Pantheon.Domain.Politics;

/// <summary>One god's vote.</summary>
public sealed class CouncilVote
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>Its choice.</summary>
    public required VoteChoice Choice { get; set; }
}

/// <summary>A council in session.</summary>
public sealed class CouncilSession
{
    /// <summary>The motion.</summary>
    public required CouncilMotion Motion { get; init; }

    /// <summary>Why it was summoned.</summary>
    public required CouncilTrigger Trigger { get; init; }

    /// <summary>Tick it opened.</summary>
    public required long OpenedTick { get; init; }

    /// <summary>Tick the vote closes.</summary>
    public required long ClosesTick { get; init; }

    /// <summary>The votes cast so far.</summary>
    public required List<CouncilVote> Votes { get; init; }
}

/// <summary>A motion the council passed, in force until it ends.</summary>
public sealed class CouncilDecree
{
    /// <summary>The motion.</summary>
    public required CouncilMotion Motion { get; init; }

    /// <summary>Tick it passed.</summary>
    public required long PassedTick { get; init; }

    /// <summary>Tick it ends.</summary>
    public required long EndsTick { get; init; }
}

/// <summary>The Divine Council (spec §42, ADR-025).</summary>
public sealed class CouncilState
{
    /// <summary>Tick of the next regular session; negative until the politics step schedules the first.</summary>
    public required long NextSessionTick { get; set; }

    /// <summary>Tick the last session closed, so triggered sessions are spaced.</summary>
    public required long LastClosedTick { get; set; }

    /// <summary>The session open now, if any.</summary>
    public required CouncilSession? Session { get; set; }

    /// <summary>Motions gods have tabled for coming sessions, oldest first.</summary>
    public required List<CouncilMotion> Tabled { get; init; }

    /// <summary>Decrees in force.</summary>
    public required List<CouncilDecree> Decrees { get; init; }

    /// <summary>A council that has not met yet, its first session not yet scheduled.</summary>
    /// <returns>The state.</returns>
    public static CouncilState Founded() => new()
    {
        NextSessionTick = -1,
        LastClosedTick = 0,
        Session = null,
        Tabled = [],
        Decrees = [],
    };
}
