using System.Collections.Generic;
using Pantheon.Domain.Belief;

namespace Pantheon.Domain.Notables;

/// <summary>
/// Prophets and elders (spec §10, §80, ADR-032), from <c>divine_rules.json</c>'s <c>Notables</c>.
/// Without the section no one rises.
/// </summary>
public sealed record NotableRules
{
    /// <summary>The names notables are given.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>Chance per hour that a devout village calls a prophet of its god.</summary>
    public float ProphetsPerHour { get; init; }

    /// <summary>The share of a village that must follow its god, and their faith, for it to call a prophet or be given one.</summary>
    public float Devotion { get; init; } = 0.6f;

    /// <summary>The share of a village that must follow a god for the god to anoint a prophet there.</summary>
    public float AnointAdherence { get; init; } = 0.4f;

    /// <summary>Most prophets a god has at once.</summary>
    public int MaxProphetsPerGod { get; init; } = 2;

    /// <summary>A prophet's shortest and longest life, in seconds.</summary>
    public float ProphetMinSeconds { get; init; } = 900f;

    /// <summary>A prophet's longest life, in seconds.</summary>
    public float ProphetMaxSeconds { get; init; } = 1800f;

    /// <summary>How fast a prophet walks, in metres per second.</summary>
    public float ProphetSpeed { get; init; } = 4f;

    /// <summary>How far a prophet goes to preach, in metres.</summary>
    public float ProphetRange { get; init; } = 1500f;

    /// <summary>Seconds a prophet preaches in a village.</summary>
    public float PreachSeconds { get; init; } = 40f;

    /// <summary>Share of the village not yet theirs that a sermon wins: from the godless whole, and half as much from rivals.</summary>
    public float PreachConversion { get; init; }

    /// <summary>Faith a sermon gives the god among the village's followers.</summary>
    public float PreachFaith { get; init; }

    /// <summary>Influence a prophet carries for their god (spec §10).</summary>
    public float ProphetInfluence { get; init; }

    /// <summary>How far a prophet's influence reaches, in metres.</summary>
    public float ProphetReach { get; init; } = 250f;

    /// <summary>Chance a second, per unit of danger to the village they are in, that a notable dies.</summary>
    public float Mortality { get; init; }

    /// <summary>What the people within <see cref="MartyrRadius"/> feel for the god of a prophet who dies in danger.</summary>
    public BeliefImpulse Martyrdom { get; init; } = new() { Faith = 0f, Love = 0f, Fear = 0f, Doubt = 0f };

    /// <summary>How far a martyrdom is felt, in metres.</summary>
    public float MartyrRadius { get; init; } = 400f;

    /// <summary>People a village needs to have an elder.</summary>
    public int ElderPopulation { get; init; } = 60;

    /// <summary>An elder's shortest life, in seconds.</summary>
    public float ElderMinSeconds { get; init; } = 1200f;

    /// <summary>An elder's longest life, in seconds.</summary>
    public float ElderMaxSeconds { get; init; } = 2400f;

    /// <summary>Faith an elder gives, and doubt an elder takes away, among their god's followers in their village each second.</summary>
    public float ElderDevotionPerSecond { get; init; }

    /// <summary>The village's resentment of the elder's god at which the elder turns from it.</summary>
    public float ElderTurnResentment { get; init; } = 0.5f;

    /// <summary>Doubt the village feels in a god its elder turns from.</summary>
    public float TurnDoubt { get; init; }

    /// <summary>Most memories a notable keeps.</summary>
    public int MemoryCapacity { get; init; } = 12;

    /// <summary>No prophets or elders.</summary>
    public static NotableRules None { get; } = new();
}
