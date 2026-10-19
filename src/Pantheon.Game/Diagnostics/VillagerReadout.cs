using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Game.Villagers;
using Pantheon.Simulation.Villagers;

namespace Pantheon.Game.Diagnostics;

/// <summary>The followed villagers' lines of the F1 debug panel (spec §80, ADR-043). Pure.</summary>
public static class VillagerReadout
{
    /// <summary>Adds every followed villager, and the needs, options, danger and memories of the player's.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        var world = snapshot.World;
        var rules = snapshot.SettlementRules.Villagers;
        foreach (var villager in world.Villagers)
        {
            lines.Add(Format("Villager of {0}: {1}: {2}; {3}", VillagerText.God(world, villager.FollowerGodId), VillagerText.Title(rules, villager, world), VillagerText.Doing(villager, world), VillagerText.Faith(villager, world)));
            if (villager.FollowerGodId != snapshot.LocalGodId)
            {
                continue;
            }

            var home = world.Settlements.Find(s => s.Id == villager.SettlementId);
            var (danger, cause) = VillagerModel.DangerTo(snapshot.SettlementRules, world, villager, home);
            lines.Add(Format("  needs: hunger {0:0.00}, fatigue {1:0.00}, health {2:0.00}; {3:0} s left; age {4:0.0} of {5:0.0}", villager.Hunger, villager.Fatigue, villager.Health,
                Math.Max(0, villager.TicksLeft) / snapshot.TicksPerSecond, VillagerModel.Age(rules, villager, world.Tick), villager.LifeYears));
            lines.Add("  options: " + string.Join(", ", VillagerMind.Options(rules, world, villager).Select(o => Format("{0} {1:0.00} ({2})", VillagerText.Verb(o.Activity), o.Score, o.Reason))));
            lines.Add(Format("  danger: {0:0.00} ({1}); flees at {2:0.00}{3}", danger, VillagerText.Danger(cause), VillagerModel.FleeAt(rules, villager), VillagerModel.Wary(villager) ? " (wary)" : string.Empty));
            lines.Add(Format("  remembers {0}: {1}", villager.Memories.Count, villager.Memories.Count == 0 ? "nothing" : VillagerText.Memory(villager.Memories[^1], villager, world)));
        }
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
