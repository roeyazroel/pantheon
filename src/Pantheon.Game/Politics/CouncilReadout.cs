using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Politics;

namespace Pantheon.Game.Politics;

/// <summary>What the council panel shows (spec §42, §57, ADR-025).</summary>
/// <param name="Session">The motion in session and its time left; empty when the council is not sitting.</param>
/// <param name="Votes">The votes so far, or the tally of a secret ballot.</param>
/// <param name="Note">The player's vote, and any promise of backing it owes.</param>
/// <param name="Ballot">The vote buttons; none when the council is not sitting.</param>
/// <param name="Decrees">The decrees in force.</param>
/// <param name="Waiting">The tabled motions and the next regular session.</param>
/// <param name="Motions">The motions the player could table now.</param>
public sealed record CouncilModel(string Session, string Votes, string Note, IReadOnlyList<PoliticsAction> Ballot, IReadOnlyList<string> Decrees, string Waiting, IReadOnlyList<PoliticsAction> Motions);

/// <summary>Builds the council panel from the session. Pure.</summary>
public static class CouncilReadout
{
    /// <summary>The panel's model.</summary>
    /// <param name="session">The session.</param>
    /// <param name="secretBallot">Whether the player's motions ask for a secret ballot (panel state).</param>
    /// <param name="focusSettlementId">The settlement the player looks at, for neutral territory; <c>null</c> for none.</param>
    /// <returns>The model.</returns>
    public static CouncilModel Build(GameSession session, bool secretBallot, int? focusSettlementId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        var me = session.LocalGodId ?? 0;
        var council = world.Diplomacy.Council;
        var decrees = council.Decrees.Select(d => PoliticsText.Capitalize(PoliticsText.Decree(d.Motion, world)) + " (" + Left(world, d.EndsTick) + " left)").ToList();
        var waiting = string.Join("; ", council.Tabled.Select(m => PoliticsText.Motion(m, world) + (m.ProposerGodId is { } by ? " (" + PoliticsText.GodName(world, by) + ")" : string.Empty)));
        var next = council.NextSessionTick >= 0 ? "Next regular session in " + Left(world, council.NextSessionTick) : "The council has not met yet";
        var waitingLine = waiting.Length == 0 ? next + "; nothing tabled." : next + ". Tabled: " + waiting + ".";
        var motions = Motions(session, me, secretBallot, focusSettlementId);
        if (council.Session is not { } open)
        {
            return new CouncilModel(string.Empty, string.Empty, "The council is not sitting.", [], decrees, waitingLine, motions);
        }

        var by = open.Motion.ProposerGodId is { } proposer ? ", asked by " + PoliticsText.GodName(world, proposer) : string.Empty;
        var head = PoliticsText.Format("In session {0}: {1}{2}{3}. {4} left.", PoliticsText.Trigger(open.Trigger), PoliticsText.Motion(open.Motion, world), by,
            open.Motion.Secret ? " (secret ballot)" : string.Empty, Left(world, open.ClosesTick));
        var votes = open.Motion.Secret
            ? PoliticsText.Format("{0} of {1} gods have voted.", open.Votes.Count, world.Gods.Count)
            : open.Votes.Count == 0 ? "No god has voted yet." : string.Join(", ", open.Votes.Select(v => PoliticsText.GodName(world, v.GodId) + " " + PoliticsText.Vote(v.Choice))) + ".";
        var mine = open.Votes.Find(v => v.GodId == me)?.Choice;
        var note = (mine is { } choice ? "You vote " + PoliticsText.Vote(choice) + "." : "You have not voted.") + Owed(world, me);
        var ballot = Enum.GetValues<VoteChoice>().Where(c => c != mine)
            .Select(c => new PoliticsAction(c switch { VoteChoice.Yes => "Vote for", VoteChoice.No => "Vote against", VoteChoice.Abstain => "Abstain" }, Council: new CouncilCommand(me, null, c)))
            .ToList();
        return new CouncilModel(head, votes, note, ballot, decrees, waitingLine, motions);
    }

    /// <summary>The motions the player could table now.</summary>
    private static List<PoliticsAction> Motions(GameSession session, int me, bool secret, int? focus)
    {
        var world = session.World;
        var motions = new List<PoliticsAction>();
        void Table(CouncilMotion motion)
        {
            var command = new CouncilCommand(me, motion, null);
            if (session.Inspect(a => a.Council?.Check(world, command, me)) == CouncilRejection.None)
            {
                motions.Add(new PoliticsAction(PoliticsText.Motion(motion, world), Council: command));
            }
        }

        foreach (var god in world.Gods.Where(g => g.Id != me))
        {
            Table(new CouncilMotion { Kind = CouncilMotionKind.Condemn, TargetGodId = god.Id, Secret = secret });
            Table(new CouncilMotion { Kind = CouncilMotionKind.PantheonWar, TargetGodId = god.Id, Secret = secret });
        }

        foreach (var site in world.HolySites)
        {
            Table(new CouncilMotion { Kind = CouncilMotionKind.ProtectSacredLand, SiteId = site.Id, Secret = secret });
        }

        foreach (var practice in Enum.GetValues<Practice>())
        {
            Table(new CouncilMotion { Kind = CouncilMotionKind.ProhibitPractice, Practice = practice, Secret = secret });
        }

        Table(new CouncilMotion { Kind = CouncilMotionKind.CoordinateDisasterResponse, Secret = secret });
        if (focus is { } settlement)
        {
            Table(new CouncilMotion { Kind = CouncilMotionKind.NeutralTerritory, SettlementId = settlement, Secret = secret });
        }

        return motions;
    }

    /// <summary>The vote the player promised another god, if any.</summary>
    private static string Owed(WorldState world, int me)
    {
        var promise = world.Diplomacy.Obligations.Find(o => o.DebtorGodId == me && o.Kind == ObligationKind.Back);
        return promise is null ? string.Empty : " You promised " + PoliticsText.GodName(world, promise.CreditorGodId) + " your vote: vote as it does.";
    }

    /// <summary>Time from now to a tick as a clock.</summary>
    private static string Left(WorldState world, long tick) => PoliticsText.Clock(Math.Max(0, tick - world.Tick) / (float)SimulationTime.TicksPerSecond);
}
