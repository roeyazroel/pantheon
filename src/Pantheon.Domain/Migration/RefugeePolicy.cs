namespace Pantheon.Domain.Migration;

/// <summary>What a god does with migrants who arrive in a settlement it holds (spec §31, ADR-016).</summary>
public enum RefugeePolicy
{
    /// <summary>Take them in as they are; they are grateful.</summary>
    Welcome,

    /// <summary>Take them in only if they turn to this god; their old gods resent it.</summary>
    DemandConversion,

    /// <summary>Send them on; they and their gods resent it.</summary>
    TurnAway,
}
