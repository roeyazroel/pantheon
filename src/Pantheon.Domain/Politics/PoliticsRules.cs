namespace Pantheon.Domain.Politics;

/// <summary>Tuning of treaties, favours and the Divine Council (spec §38–42, §54, ADR-025), in <c>diplomacy.json</c>'s <c>Politics</c>.</summary>
public sealed record PoliticsRules
{
    /// <summary>The longest a treaty may be signed for, in seconds (0 holds until broken).</summary>
    public required float MaxTreatySeconds { get; init; }

    /// <summary>The most Divine Power a tribute may pay a minute.</summary>
    public required float MaxTribute { get; init; }

    /// <summary>Chance a second that a secret treaty comes to light (spec §43: no secret lasts for ever).</summary>
    public required float SecretLeakPerSecond { get; init; }

    /// <summary>Trust the betrayed lose in a god that breaks a treaty, as a share of an alliance's betrayal.</summary>
    public required float TreatyBetrayalShare { get; init; }

    /// <summary>The most Divine Power one god may ask of another.</summary>
    public required float MaxPowerAsk { get; init; }

    /// <summary>The most favours one god may owe another.</summary>
    public required float MaxFavours { get; init; }

    /// <summary>Seconds a god has to keep a promise of assistance.</summary>
    public required float AssistSeconds { get; init; }

    /// <summary>Trust lost in a god that breaks a promise or refuses a favour it owes.</summary>
    public required float BrokenPromiseTrustLoss { get; init; }

    /// <summary>Seconds before the first regular council.</summary>
    public required float FirstCouncilSeconds { get; init; }

    /// <summary>Seconds between regular councils.</summary>
    public required float CouncilIntervalSeconds { get; init; }

    /// <summary>Least seconds between the close of a council and the next summoned by an event.</summary>
    public required float CouncilCooldownSeconds { get; init; }

    /// <summary>Seconds a council's vote stays open.</summary>
    public required float VoteSeconds { get; init; }

    /// <summary>Seconds a decree stays in force.</summary>
    public required float DecreeSeconds { get; init; }

    /// <summary>Trust every god loses in a condemned god.</summary>
    public required float CondemnTrustLoss { get; init; }

    /// <summary>Doubt a condemned god's followers gain (spec §40: mortal opinion).</summary>
    public required float CondemnDoubt { get; init; }

    /// <summary>Trust every other god loses in a god that defies a decree.</summary>
    public required float DefianceTrustLoss { get; init; }

    /// <summary>Factor on the cost of help where disaster struck lately, under a coordinated response.</summary>
    public required float CoordinatedCostFactor { get; init; }

    /// <summary>Seconds after a disaster that a settlement counts as struck, for a coordinated response.</summary>
    public required float StruckSeconds { get; init; }

    /// <summary>Share of an ally's followers, where a god leads, that turn to it when it absorbs the ally (spec §54).</summary>
    public required float AbsorbShare { get; init; }
}
