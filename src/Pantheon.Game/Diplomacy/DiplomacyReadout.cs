using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Game.Diplomacy;

/// <summary>One other god in the diplomacy panel (spec §57).</summary>
public sealed record DiplomacyEntry
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>Name, personality and how the world sees it.</summary>
    public required string Title { get; init; }

    /// <summary>Your status with it.</summary>
    public required DiplomaticStatus Status { get; init; }

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

            var personality = DivineText.Personality(god.Personality);
            entries.Add(new DiplomacyEntry
            {
                GodId = god.Id,
                Title = (personality.Length > 0 ? $"{god.Name} the {personality}" : god.Name) + Traits(god),
                Status = DiplomacyModel.StatusOf(world, me, god.Id),
                Attitude = DiplomacyText.Attitude(view),
                Explanation = DiplomacyText.Explain(view, world),
                Grievances = DiplomacyText.Grievances(DiplomacyModel.FindRelation(world, me, god.Id), world, god.Name),
                Ties = DiplomacyText.Ties(world, god.Id, me),
                RefugeePolicy = god.RefugeePolicy,
                Creatures = CreaturesOf(session, god),
                Proposal = DiplomacyAuthority.FindProposal(world, god.Id, me)?.Kind,
                Available = available,
                Politics = Politics.PoliticsReadout.ForGod(session, god.Id, choice ?? Politics.TreatyChoice.Default),
            });
        }

        return entries;
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
