namespace Pantheon.Domain.Diplomacy;

/// <summary>An act one god remembers of another, with how much it weighs; old memories fade but betrayal lingers.</summary>
public sealed class DiplomaticMemory
{
    /// <summary>Tick of the act.</summary>
    public required long Tick { get; init; }

    /// <summary>The act.</summary>
    public required DiplomaticAct Act { get; init; }

    /// <summary>The settlement it concerned, if any.</summary>
    public required int? SettlementId { get; init; }

    /// <summary>How much the act still weighs (0..1); it fades over time.</summary>
    public required float Weight { get; set; }
}
