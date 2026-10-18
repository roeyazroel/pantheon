using Pantheon.Domain.Weather;

namespace Pantheon.Domain.Events;

/// <summary>The island entered a new weather spell.</summary>
public sealed record WeatherSpellChanged : WorldEvent
{
    /// <summary>The spell before.</summary>
    public required WeatherSpell Previous { get; init; }

    /// <summary>The new spell.</summary>
    public required WeatherSpell Spell { get; init; }

    /// <summary>Tick at which the new spell ends.</summary>
    public required long EndsAtTick { get; init; }
}
