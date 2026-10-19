namespace Pantheon.Domain.Politics;

/// <summary>What a god proposes when it offers a treaty (spec §38: treaties may have durations and conditions).</summary>
public sealed record TreatyTerms
{
    /// <summary>The kind of treaty.</summary>
    public required TreatyKind Kind { get; init; }

    /// <summary>The god a joint war is against, or a defensive pact guards against (<c>null</c>: any aggressor).</summary>
    public int? TargetGodId { get; init; }

    /// <summary>The god that pays a tribute; <c>null</c> for other treaties.</summary>
    public int? PayerGodId { get; init; }

    /// <summary>Divine Power a tribute pays every minute; 0 for other treaties.</summary>
    public float Amount { get; init; }

    /// <summary>How long the treaty holds, in seconds; 0 until it is broken.</summary>
    public required float DurationSeconds { get; init; }

    /// <summary>Whether only the two gods know of it (spec §39).</summary>
    public required bool Secret { get; init; }
}
