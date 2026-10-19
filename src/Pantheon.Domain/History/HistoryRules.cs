using System.Collections.Generic;

namespace Pantheon.Domain.History;

/// <summary>What is significant enough for the world's history (spec §92, ADR-020), loaded from <c>game/Data/history.json</c>.</summary>
public sealed record HistoryRules
{
    /// <summary>Simulated seconds in a year of the world's history (spec §50's "Year 31").</summary>
    public required float SecondsPerYear { get; init; }

    /// <summary>Most records kept; the oldest are forgotten beyond it.</summary>
    public required int Capacity { get; init; }

    /// <summary>Dead from which an earthquake in a settlement is history.</summary>
    public required int EarthquakeDeaths { get; init; }

    /// <summary>Houses lost from which a fire or an earthquake in a settlement is history.</summary>
    public required int HousesLost { get; init; }

    /// <summary>People from which a group leaving a settlement is an exodus.</summary>
    public required int ExodusPeople { get; init; }

    /// <summary>People from which a settlement turning to a god is history.</summary>
    public required int ConversionPopulation { get; init; }

    /// <summary>Simulated seconds before the same kind of moment is remembered again for the same settlement or pair.</summary>
    public required float RepeatSeconds { get; init; }

    /// <summary>Creature levels that are history when reached.</summary>
    public required IReadOnlyList<int> LegendLevels { get; init; }

    /// <summary>People from which a schism is history (ADR-021).</summary>
    public required int SchismPeople { get; init; }
}
