using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Divine;
using Pantheon.Game.Politics;
using Pantheon.Simulation.Gods;

namespace Pantheon.Game.Minds.Agents;

/// <summary>An act a god's mind chose (ADR-072): a tool, what it is aimed at, and a detail.</summary>
/// <param name="Tool">The tool's id, for example <c>cast</c> or <c>declare_war</c>.</param>
/// <param name="Target">A village, god, Creature or holy site, by name; empty when the tool needs none.</param>
/// <param name="Detail">A miracle's id, a treaty's or an aim's kind; empty when the tool needs none.</param>
public sealed record GodAct(string Tool, string Target, string Detail);

/// <summary>
/// What a god's mind may do (ADR-072): everything a player may do with a god, through the same
/// authorities. It casts miracles, sends its Creature, sets it on a rival or to work, makes and breaks
/// alliances, wars, peace and treaties, answers proposals, and sets its own aims. An act the rules refuse
/// is not done, and the god is told why on its next turn.
/// </summary>
public static class GodTools
{
    /// <summary>Every tool's id.</summary>
    public static IReadOnlyList<string> Ids { get; } =
    [
        "cast", "send_creature", "creature_fight", "creature_work", "offer_alliance", "request_peace", "declare_war", "break_alliance",
        "accept", "decline", "propose_treaty", "aim",
    ];

    /// <summary>The treaties a god may propose, by id.</summary>
    public static IReadOnlyDictionary<string, TreatyKind> Treaties { get; } = new Dictionary<string, TreatyKind>(StringComparer.Ordinal)
    {
        ["non_aggression"] = TreatyKind.NonAggression,
        ["mutual_defense"] = TreatyKind.MutualDefense,
        ["shared_influence"] = TreatyKind.SharedInfluence,
        ["religious_access"] = TreatyKind.ReligiousAccess,
        ["creature_peace"] = TreatyKind.CreatureNonAggression,
        ["disaster_aid"] = TreatyKind.DisasterAssistance,
        ["territory"] = TreatyKind.TerritorialAgreement,
        ["migration"] = TreatyKind.MigrationAgreement,
        ["tribute"] = TreatyKind.Tribute,
        ["defensive_pact"] = TreatyKind.DefensivePact,
        ["joint_war"] = TreatyKind.JointWar,
    };

    /// <summary>The aims a god may set itself, by id.</summary>
    public static IReadOnlyDictionary<string, GodGoalKind> Aims { get; } = new Dictionary<string, GodGoalKind>(StringComparer.Ordinal)
    {
        ["protect_home"] = GodGoalKind.ProtectHome,
        ["convert"] = GodGoalKind.Convert,
        ["befriend"] = GodGoalKind.Befriend,
        ["weaken"] = GodGoalKind.Weaken,
        ["prepare"] = GodGoalKind.Prepare,
        ["slay_beast"] = GodGoalKind.SlayBeast,
        ["recover"] = GodGoalKind.Recover,
    };

    /// <summary>Does a god's acts in order, its aims together; tells what came of each.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The god.</param>
    /// <param name="acts">The acts.</param>
    /// <param name="rules">The agents' rules: how many acts, and how its aims weigh and hold.</param>
    /// <returns>What came of each act, in words.</returns>
    public static List<string> Do(GameSession session, GodState god, IReadOnlyList<GodAct> acts, AgentRules rules)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(acts);
        ArgumentNullException.ThrowIfNull(rules);
        var results = new List<string>();
        var aims = new List<(GodAct Act, GodAim Aim)>();
        foreach (var act in acts.Take(rules.MaxActions))
        {
            var tool = act.Tool.Trim().ToLowerInvariant();
            if (tool == "aim")
            {
                if (Aim(session.World, god, act) is { } aim)
                {
                    aims.Add((act, aim));
                }
                else
                {
                    results.Add(Said(act, "not done: no such aim, or no such village or god"));
                }

                continue;
            }

            var (_, text) = One(session, god, act with { Tool = tool });
            results.Add(Said(act, text));
        }

        if (aims.Count > 0)
        {
            results.AddRange(SetAims(session, god, aims, rules));
        }

        return results;
    }

    /// <summary>Does one act other than an aim.</summary>
    private static (bool Done, string Text) One(GameSession session, GodState god, GodAct act)
    {
        var world = session.World;
        return act.Tool switch
        {
            "cast" => Cast(session, god, act),
            "send_creature" => Creature(session, god, CreatureCommandKind.GoTo, Village(world, act.Target)?.Position ?? HolySite(world, act.Target)),
            "creature_fight" => Creature(session, god, CreatureCommandKind.Fight, Rival(world, god, act.Target)?.Position),
            "creature_work" => Creature(session, god, CreatureCommandKind.Work, Village(world, act.Target)?.Position),
            "offer_alliance" => Diplomacy(session, god, act.Target, DiplomaticCommandKind.OfferAlliance),
            "request_peace" => Diplomacy(session, god, act.Target, DiplomaticCommandKind.RequestPeace),
            "declare_war" => Diplomacy(session, god, act.Target, DiplomaticCommandKind.DeclareWar),
            "break_alliance" => Diplomacy(session, god, act.Target, DiplomaticCommandKind.BreakAlliance),
            "accept" => Diplomacy(session, god, act.Target, DiplomaticCommandKind.Accept),
            "decline" => Diplomacy(session, god, act.Target, DiplomaticCommandKind.Decline),
            "propose_treaty" => Treaty(session, god, act),
            _ => (false, "not done: there is no such act"),
        };
    }

    /// <summary>Casts a miracle on a village, through the miracle authority.</summary>
    private static (bool, string) Cast(GameSession session, GodState god, GodAct act)
    {
        if (Village(session.World, act.Target) is not { } village)
        {
            return (false, "not done: there is no such village");
        }

        var id = act.Detail.Trim().ToLowerInvariant();
        var check = session.CastMiracleAs(god.Id, id, village.Position);
        return check.IsAccepted
            ? (true, Format("done, for {0:0} power", check.Cost))
            : (false, "refused: " + DivineText.DescribeCheck(check, id, god.DivinePower, SimulationTime.TicksPerSecond));
    }

    /// <summary>Commands the god's Creature, through the Creature authority.</summary>
    private static (bool, string) Creature(GameSession session, GodState god, CreatureCommandKind kind, GroundPosition? target)
    {
        if (target is not { } place)
        {
            return (false, "not done: there is no such place or Creature");
        }

        var name = session.World.Creatures.Find(c => c.Id == god.CreatureId)?.Name ?? "your Creature";
        var result = session.CommandCreatureAs(god.Id, kind, place);
        return result.IsAccepted ? (true, "done") : (false, "refused: " + CreatureText.DescribeCommand(result, kind, name));
    }

    /// <summary>A diplomatic command to a god, through the diplomacy authority.</summary>
    private static (bool, string) Diplomacy(GameSession session, GodState god, string target, DiplomaticCommandKind kind)
    {
        if (Other(session.World, god, target) is not { } other)
        {
            return (false, "not done: there is no such god");
        }

        var aim = kind == DiplomaticCommandKind.DeclareWar && session.Engine.Gods?.Ai.ProfileOf(god) is { } profile
            ? Simulation.Conflict.WarAims.For(session.World, god, other.Id, profile)
            : (Domain.Conflict.WarAim?)null;
        var outcome = PoliticsCommands.DiplomacyAs(session, god.Id, new DiplomaticCommand(god.Id, other.Id, kind, Aim: aim));
        return outcome.Accepted ? (true, "done") : (false, "refused: " + outcome.Text);
    }

    /// <summary>Proposes a treaty on plain terms: ten minutes, a target god for a pact or a joint war, tribute of 20 power a minute paid by the other.</summary>
    private static (bool, string) Treaty(GameSession session, GodState god, GodAct act)
    {
        var world = session.World;
        if (Other(world, god, act.Target) is not { } other)
        {
            return (false, "not done: there is no such god");
        }

        var parts = act.Detail.Split(':', 2, StringSplitOptions.TrimEntries);
        if (!Treaties.TryGetValue(parts[0].ToLowerInvariant(), out var kind))
        {
            return (false, "not done: there is no such treaty");
        }

        var against = parts.Length > 1 ? Other(world, god, parts[1]) : null;
        if (kind is TreatyKind.DefensivePact or TreatyKind.JointWar && against is null)
        {
            return (false, "not done: name the god it is against, as joint_war:Name");
        }

        var terms = kind == TreatyKind.Tribute
            ? new TreatyTerms { Kind = kind, DurationSeconds = 600f, PayerGodId = other.Id, Amount = 20f, Secret = false }
            : new TreatyTerms { Kind = kind, DurationSeconds = 600f, TargetGodId = against?.Id, Secret = false };
        var outcome = PoliticsCommands.DiplomacyAs(session, god.Id, new DiplomaticCommand(god.Id, other.Id, DiplomaticCommandKind.ProposeTreaty, Terms: terms));
        return outcome.Accepted ? (true, "proposed") : (false, "refused: " + outcome.Text);
    }

    /// <summary>Sets the god's aims together, through the planner's judgement.</summary>
    private static IEnumerable<string> SetAims(GameSession session, GodState god, List<(GodAct Act, GodAim Aim)> aims, AgentRules rules)
    {
        if (session.Engine.Gods?.Ai is not { } ai || ai.ProfileOf(god) is not { } profile)
        {
            return aims.Select(a => Said(a.Act, "not done: this god makes no plans"));
        }

        var hold = (long)Math.Round(rules.AimHoldSeconds * SimulationTime.TicksPerSecond);
        var taken = session.Command((_, events) => GodAims.Set(session.World, god, ai.Planner, profile, aims.Select(a => a.Aim).ToList(), rules.AimWeight, hold, events));
        return aims.Select((a, i) => Said(a.Act, taken[i] ? "taken" : "not taken: it makes no sense now, or you hold one of its kind"));
    }

    /// <summary>An aim from an act: its kind, and the village or god it names.</summary>
    private static GodAim? Aim(WorldState world, GodState god, GodAct act)
    {
        if (!Aims.TryGetValue(act.Detail.Trim().ToLowerInvariant(), out var kind))
        {
            return null;
        }

        return kind switch
        {
            GodGoalKind.ProtectHome or GodGoalKind.Convert => Village(world, act.Target) is { } village ? new GodAim(kind, village.Id, null) : null,
            GodGoalKind.Befriend or GodGoalKind.Weaken => Other(world, god, act.Target) is { } other ? new GodAim(kind, null, other.Id) : null,
            GodGoalKind.Prepare or GodGoalKind.SlayBeast or GodGoalKind.Recover => new GodAim(kind, null, null),
        };
    }

    /// <summary>A living village by name.</summary>
    internal static Domain.Settlements.SettlementState? Village(WorldState world, string name) =>
        world.Settlements.Find(s => s.Population > 0 && Named(s.Name, name));

    /// <summary>Another god by name.</summary>
    internal static GodState? Other(WorldState world, GodState god, string name) =>
        world.Gods.Find(g => g.Id != god.Id && Named(g.Name, name));

    /// <summary>A rival's Creature by name.</summary>
    private static CreatureState? Rival(WorldState world, GodState god, string name) =>
        world.Creatures.Find(c => c.GodId != god.Id && Named(c.Name, name));

    /// <summary>A holy site's place by name.</summary>
    private static GroundPosition? HolySite(WorldState world, string name) =>
        world.HolySites.Find(h => Named(h.Name, name))?.Position;

    /// <summary>Whether a name is the one meant, whatever its case and a leading "the".</summary>
    private static bool Named(string name, string meant)
    {
        var wanted = meant.Trim();
        if (wanted.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            wanted = wanted[4..];
        }

        return wanted.Length > 0 && string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What came of an act, in a line.</summary>
    private static string Said(GodAct act, string result)
    {
        var target = act.Target.Trim().Length > 0 ? " " + act.Target.Trim() : string.Empty;
        var detail = act.Detail.Trim().Length > 0 ? " (" + act.Detail.Trim() + ")" : string.Empty;
        return Format("{0}{1}{2}: {3}", act.Tool.Trim(), target, detail, result);
    }

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.InvariantCulture, format, values);
}
