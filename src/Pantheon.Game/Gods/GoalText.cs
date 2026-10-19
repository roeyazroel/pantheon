using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;

namespace Pantheon.Game.Gods;

/// <summary>The gods' goals and meetings in words (ADR-036). Pure and culture-invariant.</summary>
public static class GoalText
{
    /// <summary>How two gods met, to follow their names.</summary>
    /// <param name="way">How.</param>
    /// <param name="place">The village where, if any.</param>
    /// <returns>For example "as neighbours at Brightwater".</returns>
    public static string Meeting(MeetingWay way, string? place) => way switch
    {
        MeetingWay.Neighbours => place is null ? "as neighbours" : "as neighbours at " + place,
        MeetingWay.SharedVillage => place is null ? "followed in one village" : "both followed in " + place,
        MeetingWay.Road => place is null ? "along a road" : "along the road to " + place,
        MeetingWay.Creatures => "when their Creatures met",
        MeetingWay.Deeds => "by their deeds",
        MeetingWay.Council => "in the Divine Council",
        MeetingWay.Diplomacy => "through their envoys",
    };

    /// <summary>A goal in a few words, from the god's side.</summary>
    /// <param name="goal">The goal.</param>
    /// <param name="world">World state, for names.</param>
    /// <param name="localGodId">The player's god, named "you".</param>
    /// <returns>For example "convert Brightwater" or "weaken you".</returns>
    public static string Goal(GodGoal goal, WorldState world, int? localGodId)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(world);
        var place = world.Settlements.Find(s => s.Id == goal.SettlementId)?.Name ?? "its home";
        var other = goal.GodId is { } id && id == localGodId ? "you" : world.Gods.Find(g => g.Id == goal.GodId)?.Name ?? "a god";
        return goal.Kind switch
        {
            GodGoalKind.ProtectHome => "protect " + place,
            GodGoalKind.Convert => "convert " + place,
            GodGoalKind.Befriend => "befriend " + other,
            GodGoalKind.Weaken => "weaken " + other,
            GodGoalKind.Prepare => "prepare its people for hard times",
            GodGoalKind.SlayBeast => "slay " + (world.GreatEvents.Beast?.Name ?? "the beast"),
            GodGoalKind.Recover => "win back its doubters",
        };
    }

    /// <summary>All of a god's goals in a sentence's words.</summary>
    /// <param name="god">The god.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The player's god.</param>
    /// <returns>For example "convert Brightwater and weaken you"; empty when it has none.</returns>
    public static string Goals(GodState god, WorldState world, int? localGodId)
    {
        ArgumentNullException.ThrowIfNull(god);
        var words = god.Goals.ConvertAll(g => Goal(g, world, localGodId));
        return words.Count switch
        {
            0 => string.Empty,
            1 => words[0],
            _ => string.Join(", ", words.GetRange(0, words.Count - 1)) + " and " + words[^1],
        };
    }

    /// <summary>The gods a god knows, by name, or how many it has yet to meet.</summary>
    /// <param name="god">The god.</param>
    /// <param name="world">World state.</param>
    /// <returns>For example "knows Vael, Seren; 1 not yet met".</returns>
    public static string Known(GodState god, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(world);
        var known = new List<string>();
        var unmet = 0;
        foreach (var other in world.Gods)
        {
            if (other.Id == god.Id)
            {
                continue;
            }

            if (god.KnownGods.Contains(other.Id))
            {
                known.Add(other.Name);
            }
            else
            {
                unmet++;
            }
        }

        var line = known.Count == 0 ? "knows no other god" : "knows " + string.Join(", ", known);
        return unmet > 0 ? line + string.Format(CultureInfo.InvariantCulture, "; {0} not yet met", unmet) : line;
    }
}
