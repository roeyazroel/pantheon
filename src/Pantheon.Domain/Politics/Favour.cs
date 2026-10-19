namespace Pantheon.Domain.Politics;

/// <summary>A divine favour one god owes another for major help (spec §41: "Major Divine Favor owed to God B").</summary>
public sealed class Favour
{
    /// <summary>The god owed.</summary>
    public required int CreditorGodId { get; init; }

    /// <summary>The god that owes.</summary>
    public required int DebtorGodId { get; init; }

    /// <summary>How much is owed: one for each major help, up to the rules' cap.</summary>
    public required float Weight { get; set; }

    /// <summary>Tick of the latest help.</summary>
    public required long SinceTick { get; set; }

    /// <summary>The settlement the latest help saved.</summary>
    public required int? SettlementId { get; set; }
}
