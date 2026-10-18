namespace Pantheon.Domain.Events;

/// <summary>
/// Something that happened in the world (spec §91). Systems and commands raise events; alerts,
/// effects and, later, history read them. Events are transient: they are not saved, and the world
/// state already holds their consequences.
/// </summary>
public abstract record WorldEvent
{
    /// <summary>Tick during which the event happened.</summary>
    public required long Tick { get; init; }
}
