using Pantheon.Domain.Politics;

namespace Pantheon.Domain.Events;

/// <summary>A god granted or refused another's request (spec §41, ADR-025).</summary>
public sealed record RequestAnswered : WorldEvent
{
    /// <summary>The god that asked.</summary>
    public required int AskerGodId { get; init; }

    /// <summary>The god asked.</summary>
    public required int AskedGodId { get; init; }

    /// <summary>What was asked.</summary>
    public required AskKind Ask { get; init; }

    /// <summary>The god a war support was against, if any.</summary>
    public required int? TargetGodId { get; init; }

    /// <summary>Whether it was granted.</summary>
    public required bool Granted { get; init; }

    /// <summary>Whether the asked god owed the asker a favour.</summary>
    public required bool Owed { get; init; }
}
