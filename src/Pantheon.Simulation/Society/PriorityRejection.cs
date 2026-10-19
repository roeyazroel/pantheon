namespace Pantheon.Simulation.Society;

/// <summary>Why a god may not set a settlement's priority (ADR-023).</summary>
public enum PriorityRejection
{
    /// <summary>It may.</summary>
    None,

    /// <summary>No such god.</summary>
    UnknownGod,

    /// <summary>No such living settlement.</summary>
    UnknownSettlement,

    /// <summary>The issuer may not act for that god.</summary>
    NotAuthorized,

    /// <summary>Too few of the settlement's people follow the god.</summary>
    TooFewFollowers,

    /// <summary>The priority was set too recently.</summary>
    TooSoon,

    /// <summary>The settlement already puts it first.</summary>
    Unchanged,
}
