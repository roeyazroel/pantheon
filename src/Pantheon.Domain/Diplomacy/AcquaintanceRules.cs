namespace Pantheon.Domain.Diplomacy;

/// <summary>
/// How gods come to know of each other (spec §11's KnownGods, ADR-036). With <see cref="None"/>
/// every god knows every other from the start.
/// </summary>
public sealed record AcquaintanceRules
{
    /// <summary>Every god knows every other; nothing waits on meeting.</summary>
    public static AcquaintanceRules None { get; } = new() { MeetFirst = false, NeighbourMetres = 0f, SharedAdherence = 1f };

    /// <summary>Whether gods must meet before they ally, war, sign treaties or ask favours of each other.</summary>
    public required bool MeetFirst { get; init; }

    /// <summary>Villages of two gods this close, in metres, bring them to meet: their peoples trade and talk.</summary>
    public required float NeighbourMetres { get; init; }

    /// <summary>The least share of a village's people following each of two gods for them to meet there.</summary>
    public required float SharedAdherence { get; init; }
}
