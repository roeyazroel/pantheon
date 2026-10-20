using System.Collections.Generic;

namespace Pantheon.Game.Hud;

/// <summary>Everything the player HUD shows for one frame (spec §57), built by <see cref="HudReadout"/>.</summary>
public sealed record HudModel
{
    /// <summary>Whether the local player has a god; without one the HUD shows only <see cref="GodLine"/>.</summary>
    public required bool HasGod { get; init; }

    /// <summary>God name and rank.</summary>
    public required string GodLine { get; init; }

    /// <summary>Divine Power, rounded.</summary>
    public required string PowerText { get; init; }

    /// <summary>Divine Power raised per simulated second.</summary>
    public required string IncomeText { get; init; }

    /// <summary>Followers and the next rank.</summary>
    public required string FollowersText { get; init; }

    /// <summary>The miracle bar.</summary>
    public required IReadOnlyList<MiracleSlot> Miracles { get; init; }

    /// <summary>What casting the selected miracle at the cursor would do, or <c>null</c> when none is selected.</summary>
    public string? TargetHint { get; init; }

    /// <summary>Whether the selected miracle can be cast at the cursor.</summary>
    public bool TargetValid { get; init; }

    /// <summary>Radius in metres of the selected miracle, for the targeting ring; 0 when none is selected.</summary>
    public float TargetRadius { get; init; }

    /// <summary>One line per rival god: name, personality, followers and share of the world's belief.</summary>
    public IReadOnlyList<string> Rivals { get; init; } = [];

    /// <summary>The race for victory (spec §115): who holds how much of the world's belief and how long until a win.</summary>
    public string? Contest { get; init; }

    /// <summary>Whether the contest line is good news for the player.</summary>
    public bool ContestFavoursPlayer { get; init; }

    /// <summary>The weather and dangers when they matter (a dry or wet spell; drought, storms, fire or earthquakes among your people), or <c>null</c>.</summary>
    public string? Weather { get; init; }

    /// <summary>Whether <see cref="Weather"/> is a warning: a dry spell, or drought, storm, fire or an earthquake among your people.</summary>
    public bool WeatherWarning { get; init; }

    /// <summary>The Creature's card, or <c>null</c> when the god has no Creature.</summary>
    public CreatureCard? Creature { get; init; }

    /// <summary>The god's domains and holy sites (ADR-021), for example "Storm, Death - keeps the Sacred Peak of Elar".</summary>
    public string? Domains { get; init; }

    /// <summary>Whether the god may take up a domain now: the HUD invites it to (G).</summary>
    public bool DomainToChoose { get; init; }

    /// <summary>The god's name, for the HUD's plaque (ADR-059).</summary>
    public string GodName { get; init; } = string.Empty;

    /// <summary>The god's rank in words.</summary>
    public string RankName { get; init; } = string.Empty;

    /// <summary>Divine Power.</summary>
    public float Power { get; init; }

    /// <summary>Divine Power raised per simulated second.</summary>
    public float Income { get; init; }

    /// <summary>The god's followers.</summary>
    public int Followers { get; init; }

    /// <summary>The next rank in words, or <c>null</c> at the highest.</summary>
    public string? NextRankName { get; init; }

    /// <summary>The followers the next rank needs; 0 at the highest.</summary>
    public int NextRankFollowers { get; init; }

    /// <summary>The share of all belief the player holds, with its allies when they would win together (0..1).</summary>
    public float BeliefShare { get; init; }

    /// <summary>The share of belief that wins by Divine Ascendancy, or 0 when the world cannot be won that way.</summary>
    public float WinningShare { get; init; }

    /// <summary>The season and year in a few words, such as "Spring, year 1", or <c>null</c> without seasons.</summary>
    public string? Calendar { get; init; }

    /// <summary>Whether the simulation is paused.</summary>
    public bool Paused { get; init; }

    /// <summary>Whether the player's miracles are cast veiled (ADR-026), for the miracle bar's Veil toggle (ADR-068).</summary>
    public bool Veiled { get; init; }

    /// <summary>The speed in use, as an index into <see cref="SessionCommands.SpeedPresets"/>.</summary>
    public int SpeedPreset { get; init; }

    /// <summary>The other gods, one chip each.</summary>
    public IReadOnlyList<RivalChip> RivalChips { get; init; } = [];
}

/// <summary>One rival god on the HUD (ADR-059).</summary>
/// <param name="GodId">The god.</param>
/// <param name="Name">Its name.</param>
/// <param name="Detail">Its player's name or personality, and its following as far as the player knows it.</param>
/// <param name="Status">How it stands with the player, or <c>null</c> before they have met.</param>
public sealed record RivalChip(int GodId, string Name, string Detail, Pantheon.Domain.Diplomacy.DiplomaticStatus? Status);
