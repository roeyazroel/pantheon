using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Settlements;
using Pantheon.Game.Politics;
using Pantheon.Simulation.Conflict;
using Pantheon.Simulation.Politics;

namespace Pantheon.Game.Conflict;

/// <summary>A settlement's holy wars and what the player may do there through mortals, for the Settlements panel (spec §45–46, ADR-026). Pure.</summary>
public static class SettlementConflict
{
    /// <summary>Lines about the holy wars the settlement wages or suffers.</summary>
    /// <param name="session">The session.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The lines; none at peace.</returns>
    public static IReadOnlyList<string> Lines(GameSession session, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settlement);
        var world = session.World;
        var lines = new List<string>();
        if (settlement.Society.HolyWar is { } war)
        {
            var defender = world.Settlements.Find(s => s.Id == war.DefenderSettlementId)?.Name ?? "a lost village";
            lines.Add(ConflictText.Format(
                "Wages holy war on {0} for {1} ({2}): {3} killed, {4} converted.",
                defender, ConflictText.GodName(world, war.GodId), Stance(war.Stance), war.Killed, war.Converted));
        }

        foreach (var attacker in world.Settlements.Where(s => s.Society.HolyWar?.DefenderSettlementId == settlement.Id))
        {
            lines.Add(ConflictText.Format("Under holy war from {0}, for {1}.", attacker.Name, ConflictText.GodName(world, attacker.Society.HolyWar!.GodId)));
        }

        return lines;
    }

    /// <summary>The player's buttons: its word to its people's holy war, and the proxy acts it could do against the god that leads here.</summary>
    /// <param name="session">The session.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The actions.</returns>
    public static IReadOnlyList<PoliticsAction> Actions(GameSession session, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settlement);
        var world = session.World;
        var me = session.LocalGodId ?? 0;
        var actions = new List<PoliticsAction>();
        if (settlement.Society.HolyWar is { } war && war.GodId == me)
        {
            foreach (var stance in Enum.GetValues<HolyWarStance>().Where(s => s != war.Stance))
            {
                actions.Add(new PoliticsAction(ConflictText.Stance(stance) + " the holy war", HolyWar: new HolyWarCommand(me, settlement.Id, stance)));
            }
        }

        if (TreatyModel.Leader(settlement) is { } leader && leader != me)
        {
            foreach (var kind in Enum.GetValues<ProxyKind>())
            {
                var command = new ProxyCommand(me, settlement.Id, leader, kind);
                var cost = session.Inspect(a => a.Conflict?.Cost(kind) ?? 0f);
                if (session.Inspect(a => a.Conflict?.Check(world, command, me)) == ConflictRejection.None)
                {
                    actions.Add(new PoliticsAction(ConflictText.Format("{0} against {1} ({2:0})", Capitalize(ConflictText.Proxy(kind)), ConflictText.GodName(world, leader), cost), Proxy: command));
                }
            }
        }

        return actions;
    }

    /// <summary>A stance in a sentence.</summary>
    private static string Stance(HolyWarStance stance) => stance switch
    {
        HolyWarStance.Ignore => "left to itself",
        HolyWarStance.Encourage => "urged on",
        HolyWarStance.Discourage => "held back",
    };

    /// <summary>The first letter in upper case.</summary>
    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
