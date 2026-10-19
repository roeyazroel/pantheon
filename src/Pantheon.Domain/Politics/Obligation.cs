namespace Pantheon.Domain.Politics;

/// <summary>What a god promised another when it granted a request.</summary>
public enum ObligationKind
{
    /// <summary>To help the creditor's people in need before the deadline.</summary>
    Assist,

    /// <summary>To vote as the creditor votes in the council session open now or next.</summary>
    Back,
}

/// <summary>A promise one god owes another until it is kept or the deadline passes (spec §40: a broken promise has consequences).</summary>
public sealed class Obligation
{
    /// <summary>The god that promised.</summary>
    public required int DebtorGodId { get; init; }

    /// <summary>The god promised.</summary>
    public required int CreditorGodId { get; init; }

    /// <summary>What was promised.</summary>
    public required ObligationKind Kind { get; init; }

    /// <summary>Tick of the promise.</summary>
    public required long SinceTick { get; init; }

    /// <summary>Tick by which it must be kept.</summary>
    public required long DueTick { get; init; }
}
