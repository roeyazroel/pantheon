using System.Collections.Generic;
using Pantheon.Domain.Belief;

namespace Pantheon.Domain.GreatEvents;

/// <summary>Great events (spec §52, ADR-034): the plague, eclipses, reformations, and the watch for world wars, great droughts and great migrations. <see cref="None"/> has none.</summary>
public sealed record GreatEventRules
{
    /// <summary>No great events.</summary>
    public static GreatEventRules None { get; } = new()
    {
        Plague = PlagueRules.None, Eclipse = EclipseRules.None, Reformation = ReformationRules.None,
        WorldWarShare = 2f, WorldWarMinGods = int.MaxValue, WarFearPerSecond = 0f, DroughtSoil = -1f, DroughtSeconds = 0f, MigrationShare = 2f,
        Names = [],
    };

    /// <summary>The ancient beast (ADR-035); none when absent.</summary>
    public BeastRules Beast { get; init; } = BeastRules.None;

    /// <summary>The magical anomaly (ADR-035); none when absent.</summary>
    public AnomalyRules Anomaly { get; init; } = AnomalyRules.None;

    /// <summary>The plague.</summary>
    public required PlagueRules Plague { get; init; }

    /// <summary>Eclipses.</summary>
    public required EclipseRules Eclipse { get; init; }

    /// <summary>Reformations.</summary>
    public required ReformationRules Reformation { get; init; }

    /// <summary>The share of the gods at war at once that makes a world war.</summary>
    public required float WorldWarShare { get; init; }

    /// <summary>The fewest gods at war at once that make a world war.</summary>
    public required int WorldWarMinGods { get; init; }

    /// <summary>Fear every believer gains per second while a world war lasts.</summary>
    public required float WarFearPerSecond { get; init; }

    /// <summary>The land's mean soil water at or below which a dry spell becomes a great drought.</summary>
    public required float DroughtSoil { get; init; }

    /// <summary>Seconds the land must stay that dry.</summary>
    public required float DroughtSeconds { get; init; }

    /// <summary>The share of the island's people on the road at once that makes a great migration.</summary>
    public required float MigrationShare { get; init; }

    /// <summary>Names for great events, by kind: a kind's name is picked from its list by the event's id.</summary>
    public required IReadOnlyList<GreatEventNames> Names { get; init; }
}

/// <summary>The names a kind of great event may take (ADR-034).</summary>
public sealed record GreatEventNames
{
    /// <summary>The kind.</summary>
    public required GreatEventKind Kind { get; init; }

    /// <summary>Its names.</summary>
    public required IReadOnlyList<string> Names { get; init; }
}

/// <summary>How a plague breaks out, spreads, harms and ends (ADR-034).</summary>
public sealed record PlagueRules
{
    /// <summary>No plague.</summary>
    public static PlagueRules None { get; } = new()
    {
        OutbreakPerHour = 0f, Crowding = 1f, Transmission = 0f, RecoveryPerSecond = 0f, ImmunityLossPerSecond = 0f, HealthLossPerSecond = 0f,
        ContactRange = 0f, ContactPerSecond = 0f, RoadPerSecond = 0f, HospitalShield = 0f, MedicinePerLevel = 0f, HealCure = 0f, EventVillages = int.MaxValue,
    };

    /// <summary>Outbreaks an hour in a village at its worst: crowded, sick and polluted, with no immunity.</summary>
    public required float OutbreakPerHour { get; init; }

    /// <summary>Crowding (people over housing) from which a village can break out at all.</summary>
    public required float Crowding { get; init; }

    /// <summary>Share of the healthy the sick infect per second, at full sickness.</summary>
    public required float Transmission { get; init; }

    /// <summary>Share of the sick who recover per second, immune.</summary>
    public required float RecoveryPerSecond { get; init; }

    /// <summary>Share of its immunity a village loses per second.</summary>
    public required float ImmunityLossPerSecond { get; init; }

    /// <summary>Health lost per second at full sickness.</summary>
    public required float HealthLossPerSecond { get; init; }

    /// <summary>The distance in metres within which a village can catch the plague from a neighbour.</summary>
    public required float ContactRange { get; init; }

    /// <summary>The chance per second, at full sickness next door, that a neighbour catches it.</summary>
    public required float ContactPerSecond { get; init; }

    /// <summary>The same along an open road, whatever its length.</summary>
    public required float RoadPerSecond { get; init; }

    /// <summary>Share of the spread a hospital stops.</summary>
    public required float HospitalShield { get; init; }

    /// <summary>Share of the spread each level of medicine stops.</summary>
    public required float MedicinePerLevel { get; init; }

    /// <summary>Share of the sickness a healing miracle cures per unit of its healing.</summary>
    public required float HealCure { get; init; }

    /// <summary>Villages sick at once that make the plague a great event.</summary>
    public required int EventVillages { get; init; }
}

/// <summary>When the sun goes dark and what it does (ADR-034).</summary>
public sealed record EclipseRules
{
    /// <summary>No eclipses.</summary>
    public static EclipseRules None { get; } = new()
    {
        FirstMinSeconds = 0f, FirstMaxSeconds = 0f, IntervalMinSeconds = 0f, IntervalMaxSeconds = 0f, Seconds = 0f,
        Dread = new BeliefImpulse { Faith = 0f, Love = 0f, Fear = 0f, Doubt = 0f }, Awe = 1f,
    };

    /// <summary>Whether the sun ever goes dark.</summary>
    public bool Enabled => Seconds > 0f && IntervalMaxSeconds > 0f;

    /// <summary>The soonest the first eclipse comes, in seconds.</summary>
    public required float FirstMinSeconds { get; init; }

    /// <summary>The latest the first eclipse comes.</summary>
    public required float FirstMaxSeconds { get; init; }

    /// <summary>The shortest time between eclipses.</summary>
    public required float IntervalMinSeconds { get; init; }

    /// <summary>The longest time between eclipses.</summary>
    public required float IntervalMaxSeconds { get; init; }

    /// <summary>How long the sun stays dark.</summary>
    public required float Seconds { get; init; }

    /// <summary>What every believer feels when the sun goes dark.</summary>
    public required BeliefImpulse Dread { get; init; }

    /// <summary>How much more a miracle worked in the dark moves those who see it, as a factor.</summary>
    public required float Awe { get; init; }
}

/// <summary>When a doubting faith reforms itself (ADR-034).</summary>
public sealed record ReformationRules
{
    /// <summary>No reformations.</summary>
    public static ReformationRules None { get; } = new()
    {
        Doubt = 2f, Learning = 2f, MinVillages = int.MaxValue, CooldownSeconds = 0f, DoubtKept = 1f, Faith = 0f, Leaving = 0f,
    };

    /// <summary>The mean doubt of a god's followers at which its faith reforms.</summary>
    public required float Doubt { get; init; }

    /// <summary>The mean learning of the villages they live in that it takes.</summary>
    public required float Learning { get; init; }

    /// <summary>The fewest villages that follow the god.</summary>
    public required int MinVillages { get; init; }

    /// <summary>Seconds before the same faith reforms again.</summary>
    public required float CooldownSeconds { get; init; }

    /// <summary>Share of their doubt the followers keep after it.</summary>
    public required float DoubtKept { get; init; }

    /// <summary>Faith the followers gain.</summary>
    public required float Faith { get; init; }

    /// <summary>Share of the doubting followers who leave the god rather than reform.</summary>
    public required float Leaving { get; init; }
}
