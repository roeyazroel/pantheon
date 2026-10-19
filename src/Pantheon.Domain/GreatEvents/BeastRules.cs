using System.Collections.Generic;
using Pantheon.Domain.Belief;

namespace Pantheon.Domain.GreatEvents;

/// <summary>The ancient beast (ADR-035): when it wakes, how it ravages, and how gods and their Creatures fight it.</summary>
public sealed record BeastRules
{
    /// <summary>No beast.</summary>
    public static BeastRules None { get; } = new()
    {
        Names = [], FirstSleepSeconds = 0f, WakePerHour = 0f, WakeMagnitude = float.MaxValue, WakeRange = 0f, Health = 1f, RegenPerSecond = 0f,
        Speed = 0f, AwakeSeconds = 0f, RavageSeconds = 0f, StrikeSeconds = 1f, StrikeHousing = 0f, StrikeDeaths = 0f, FightRange = 0f,
        CreatureHarm = 0f, Bite = 0f, FleeHealth = 0f, SmiteHarm = 0f, FireHarmPerSecond = 0f, CallRange = 0f,
        Renown = new BeliefImpulse { Faith = 0f, Love = 0f, Fear = 0f, Doubt = 0f },
    };

    /// <summary>Whether a world has a beast.</summary>
    public bool Enabled => Names.Count > 0;

    /// <summary>Names it may take, one drawn from the seed.</summary>
    public required IReadOnlyList<string> Names { get; init; }

    /// <summary>Seconds into a world before it may wake by itself.</summary>
    public required float FirstSleepSeconds { get; init; }

    /// <summary>Times an hour it wakes by itself, after its first sleep.</summary>
    public required float WakePerHour { get; init; }

    /// <summary>The magnitude of an earthquake near its lair that wakes it.</summary>
    public required float WakeMagnitude { get; init; }

    /// <summary>How near its lair, in metres, a quake or an eruption must be to wake it.</summary>
    public required float WakeRange { get; init; }

    /// <summary>Its health when whole.</summary>
    public required float Health { get; init; }

    /// <summary>Health it heals per second while nothing harms it.</summary>
    public required float RegenPerSecond { get; init; }

    /// <summary>Its speed in metres per second.</summary>
    public required float Speed { get; init; }

    /// <summary>Seconds it stays awake before it goes back to sleep, if it lives.</summary>
    public required float AwakeSeconds { get; init; }

    /// <summary>Seconds it ravages a village before it moves on.</summary>
    public required float RavageSeconds { get; init; }

    /// <summary>Seconds between its blows on the village it ravages.</summary>
    public required float StrikeSeconds { get; init; }

    /// <summary>Share of the houses a blow destroys, before protection.</summary>
    public required float StrikeHousing { get; init; }

    /// <summary>Share of the people a blow kills, before protection.</summary>
    public required float StrikeDeaths { get; init; }

    /// <summary>How near it, in metres, a Creature fights it.</summary>
    public required float FightRange { get; init; }

    /// <summary>Harm a Creature does it per second per unit of its power.</summary>
    public required float CreatureHarm { get; init; }

    /// <summary>Health a Creature fighting it loses per second.</summary>
    public required float Bite { get; init; }

    /// <summary>Health at which a Creature flies from it, beaten and banished.</summary>
    public required float FleeHealth { get; init; }

    /// <summary>Harm a smiting miracle does it per unit of its smiting.</summary>
    public required float SmiteHarm { get; init; }

    /// <summary>Harm per second of the fire it stands in, at full fire.</summary>
    public required float FireHarmPerSecond { get; init; }

    /// <summary>How far, in metres, an AI god's Creature comes to fight it when it ravages one of the god's villages.</summary>
    public required float CallRange { get; init; }

    /// <summary>What each god's followers feel when it falls, by the god's share of the harm.</summary>
    public required BeliefImpulse Renown { get; init; }
}

/// <summary>The magical anomaly (ADR-035): a surge of power at a seeded place, now and then.</summary>
public sealed record AnomalyRules
{
    /// <summary>No anomaly.</summary>
    public static AnomalyRules None { get; } = new()
    {
        FirstMinSeconds = 0f, FirstMaxSeconds = 0f, IntervalMinSeconds = 0f, IntervalMaxSeconds = 0f, Seconds = 0f, Radius = 0f, PowerPerSecond = 0f, CostFactor = 1f,
    };

    /// <summary>Whether anomalies come.</summary>
    public bool Enabled => Seconds > 0f && IntervalMaxSeconds > 0f;

    /// <summary>The soonest the first comes, in seconds.</summary>
    public required float FirstMinSeconds { get; init; }

    /// <summary>The latest the first comes.</summary>
    public required float FirstMaxSeconds { get; init; }

    /// <summary>The shortest time between them.</summary>
    public required float IntervalMinSeconds { get; init; }

    /// <summary>The longest time between them.</summary>
    public required float IntervalMaxSeconds { get; init; }

    /// <summary>How long one lasts.</summary>
    public required float Seconds { get; init; }

    /// <summary>Its reach in metres.</summary>
    public required float Radius { get; init; }

    /// <summary>Divine Power a god draws per second at full influence at its heart.</summary>
    public required float PowerPerSecond { get; init; }

    /// <summary>The factor on the cost of a miracle cast within its reach.</summary>
    public required float CostFactor { get; init; }
}
