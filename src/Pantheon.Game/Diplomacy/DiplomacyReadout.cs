using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Game.Diplomacy;

/// <summary>One other god in the diplomacy panel (spec §57).</summary>
public sealed record DiplomacyEntry
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>Name, epithet or personality, and how the world sees it.</summary>
    public required string Title { get; init; }

    /// <summary>Who it is (ADR-065): its history and nature; empty for a god without a persona.</summary>
    public string Lore { get; init; } = string.Empty;

    /// <summary>Your status with it.</summary>
    public required DiplomaticStatus Status { get; init; }

    /// <summary>Whether you have met it (ADR-036); until then you cannot deal with it.</summary>
    public bool Met { get; init; } = true;

    /// <summary>How it feels about you.</summary>
    public required string Attitude { get; init; }

    /// <summary>Why, from what it remembers; empty when it remembers nothing.</summary>
    public required string Explanation { get; init; }

    /// <summary>What your god holds against it; empty when nothing.</summary>
    public required string Grievances { get; init; }

    /// <summary>Its alliances and wars with the other gods (ADR-015), for example "Allied with Seren; at war with Kaio"; empty when none.</summary>
    public required string Ties { get; init; }

    /// <summary>What it does with migrants arriving where it is followed (ADR-016).</summary>
    public required Domain.Migration.RefugeePolicy RefugeePolicy { get; init; }

    /// <summary>How your Creature and its Creature regard each other (ADR-017); empty when either has none.</summary>
    public required string Creatures { get; init; }

    /// <summary>What it has proposed to you, if anything.</summary>
    public ProposalKind? Proposal { get; init; }

    /// <summary>The commands you could give towards it now.</summary>
    public required IReadOnlyList<DiplomaticCommandKind> Available { get; init; }

    /// <summary>Treaties, favours, its proposal and what you could propose or ask (ADR-025).</summary>
    public required Politics.PoliticsEntry Politics { get; init; }

    /// <summary>The war between you, with its goal and score (ADR-026); empty when at peace.</summary>
    public required string War { get; init; }

    /// <summary>What you know of it (ADR-026): how well informed you are, and what that shows.</summary>
    public required string Intelligence { get; init; }
}

/// <summary>Builds the diplomacy panel from the session. Pure.</summary>
public static class DiplomacyReadout
{
    private static readonly DiplomaticCommandKind[] Moves =
    [
        DiplomaticCommandKind.OfferAlliance,
        DiplomaticCommandKind.BreakAlliance,
        DiplomaticCommandKind.DeclareWar,
        DiplomaticCommandKind.RequestPeace,
        DiplomaticCommandKind.Accept,
        DiplomaticCommandKind.Decline,
        DiplomaticCommandKind.DemandSurrender,
    ];

    /// <summary>One entry per other god.</summary>
    /// <param name="session">Game session.</param>
    /// <param name="choice">How the player will sign treaties (panel state); five minutes in public when <c>null</c>.</param>
    /// <returns>The entries; none without a local god.</returns>
    public static IReadOnlyList<DiplomacyEntry> Build(GameSession session, Politics.TreatyChoice? choice = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.LocalGodId is not { } me)
        {
            return [];
        }

        var world = session.World;
        var entries = new List<DiplomacyEntry>();
        foreach (var god in world.Gods)
        {
            if (god.Id == me)
            {
                continue;
            }

            var view = DiplomacyModel.FindRelation(world, god.Id, me);
            var available = new List<DiplomaticCommandKind>();
            foreach (var move in Moves)
            {
                if (session.CheckDiplomacy(god.Id, move) == DiplomacyRejection.None)
                {
                    available.Add(move);
                }
            }

            entries.Add(new DiplomacyEntry
            {
                GodId = god.Id,
                Title = Minds.PersonaText.Title(god) + Traits(god),
                Lore = god.Persona is { } persona ? persona.History + " " + Minds.PersonaText.Nature(god) : string.Empty,
                Status = DiplomacyModel.StatusOf(world, me, god.Id),
                Met = Acquaintance.Knows(session.Content.Diplomacy.Acquaintance, world, me, god.Id),
                Attitude = DiplomacyText.Attitude(view),
                Explanation = DiplomacyText.Explain(view, world),
                Grievances = DiplomacyText.Grievances(DiplomacyModel.FindRelation(world, me, god.Id), world, god.Name),
                Ties = DiplomacyText.Ties(world, god.Id, me),
                RefugeePolicy = god.RefugeePolicy,
                Creatures = CreaturesOf(session, god),
                Proposal = DiplomacyAuthority.FindProposal(world, god.Id, me)?.Kind,
                Available = available,
                Politics = Politics.PoliticsReadout.ForGod(session, god.Id, choice ?? Politics.TreatyChoice.Default),
                War = Simulation.Conflict.WarBook.Find(world, me, god.Id) is { } war ? Conflict.ConflictText.War(session.Content.Diplomacy.Conflict, world, war, me) : string.Empty,
                Intelligence = Intelligence(session, me, god),
            });
        }

        return entries;
    }

    /// <summary>
    /// What the player knows of a god (spec §43): well informed, it sees its Divine Power and, of an
    /// ally, whether it is weighing betrayal; partly informed, a rough count; in the dark, little.
    /// </summary>
    private static string Intelligence(GameSession session, int me, Domain.Gods.GodState god)
    {
        var intel = Simulation.Conflict.IntelModel.Level(session.Content.Diplomacy.Conflict, session.World, me, god.Id);
        var line = "You are " + Conflict.ConflictText.Intel(intel) + " of it";
        if (intel < 0.6f)
        {
            return line + ".";
        }

        line += Conflict.ConflictText.Format(": its Divine Power is {0:0}", god.DivinePower);
        var plotting = DiplomacyModel.AreAllied(session.World, me, god.Id)
            && session.PreviewGodDiplomacy(god).Any(c => c.Kind is DiplomaticCommandKind.BreakAlliance or DiplomaticCommandKind.Absorb && c.OtherGodId == me && c.Utility > 0f);
        var goals = Gods.GoalText.Goals(god, session.World, me);
        return line + (plotting ? "; it is weighing betrayal" : string.Empty) + (goals.Length > 0 ? "; it means to " + goals : string.Empty) + ".";
    }

    /// <summary>How the world sees the god (ADR-024), for example " (seen as cruel, vengeful)", or empty.</summary>
    private static string Traits(Domain.Gods.GodState god) =>
        god.Traits.Count == 0 ? string.Empty : " (seen as " + string.Join(", ", god.Traits.ConvertAll(Culture.CultureText.Name)) + ")";

    /// <summary>How the player's Creature and the god's Creature regard each other, or empty.</summary>
    private static string CreaturesOf(GameSession session, Domain.Gods.GodState god) =>
        session.LocalCreature is { } mine && god.CreatureId is { } id && session.World.Creatures.Find(c => c.Id == id) is { } theirs
            ? Creatures.CreatureBondText.Between(session.Content.Creatures.Bonds, mine, theirs)
            : string.Empty;
}
