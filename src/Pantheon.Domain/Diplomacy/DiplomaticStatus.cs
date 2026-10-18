namespace Pantheon.Domain.Diplomacy;

/// <summary>Where two gods stand with each other (spec §113), from closest to most hostile.</summary>
public enum DiplomaticStatus
{
    /// <summary>Allied: they share influence and do not court each other's followers by force.</summary>
    Alliance,

    /// <summary>On good terms.</summary>
    Friendly,

    /// <summary>Neither friends nor foes.</summary>
    Neutral,

    /// <summary>On bad terms.</summary>
    Hostile,

    /// <summary>At war: their peoples resist each other and their Creatures fight.</summary>
    War,
}
