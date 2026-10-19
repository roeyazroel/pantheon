using System;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Politics;

/// <summary>The diplomacy authority's checks of treaties, requests and their answers (spec §38, §41, §73, ADR-025).</summary>
internal static class PoliticsChecks
{
    /// <summary>Whether a god may propose a treaty on these terms to another.</summary>
    /// <param name="rules">Politics rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="me">The proposing god.</param>
    /// <param name="other">The god asked.</param>
    /// <param name="terms">The terms.</param>
    /// <returns>Why not, or <see cref="DiplomacyRejection.None"/>.</returns>
    public static DiplomacyRejection Treaty(PoliticsRules rules, WorldState world, int me, int other, TreatyTerms? terms)
    {
        if (terms is null || !float.IsFinite(terms.DurationSeconds) || terms.DurationSeconds < 0f || terms.DurationSeconds > rules.MaxTreatySeconds
            || (terms.DurationSeconds == 0f && terms.Kind == TreatyKind.Tribute))
        {
            return DiplomacyRejection.InvalidTerms;
        }

        var aimed = terms.Kind is TreatyKind.JointWar or TreatyKind.DefensivePact;
        var target = terms.TargetGodId;
        if ((terms.Kind == TreatyKind.JointWar && target is null) || (!aimed && target is not null)
            || (target is { } t && (t == me || t == other || GodStanding.Find(world, t) is null)))
        {
            return DiplomacyRejection.InvalidTerms;
        }

        var tribute = terms.Kind == TreatyKind.Tribute;
        if (tribute != (terms.PayerGodId is not null) || (tribute && ((terms.PayerGodId != me && terms.PayerGodId != other) || !(terms.Amount > 0f) || terms.Amount > rules.MaxTribute)))
        {
            return DiplomacyRejection.InvalidTerms;
        }

        if (DiplomacyModel.AreAtWar(world, me, other) && terms.Kind is not (TreatyKind.NonAggression or TreatyKind.Tribute))
        {
            return DiplomacyRejection.WrongStatus;
        }

        return world.Diplomacy.Treaties.Exists(t2 => t2.Kind == terms.Kind && t2.Binds(me) && t2.Binds(other) && t2.TargetGodId == target)
            ? DiplomacyRejection.WrongStatus
            : DiplomacyRejection.None;
    }

    /// <summary>Whether a god may ask this of another.</summary>
    /// <param name="rules">Politics rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="me">The asking god.</param>
    /// <param name="other">The god asked.</param>
    /// <param name="ask">What is asked.</param>
    /// <returns>Why not, or <see cref="DiplomacyRejection.None"/>.</returns>
    public static DiplomacyRejection Ask(PoliticsRules rules, WorldState world, int me, int other, RequestAsk? ask)
    {
        if (ask is null)
        {
            return DiplomacyRejection.InvalidTerms;
        }

        return ask.Kind switch
        {
            AskKind.DivinePower => ask.Amount > 0f && ask.Amount <= rules.MaxPowerAsk ? DiplomacyRejection.None : DiplomacyRejection.InvalidTerms,
            AskKind.WarSupport => ask.TargetGodId is { } target && target != me && target != other && GodStanding.Find(world, target) is not null
                ? DiplomacyRejection.None : DiplomacyRejection.InvalidTerms,
            AskKind.Backing or AskKind.Territory or AskKind.Assistance => DiplomacyRejection.None,
        };
    }

    /// <summary>Whether a god may accept a proposal now.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="proposal">The proposal.</param>
    /// <param name="me">The god answering.</param>
    /// <param name="settled">Whether the pair's status has held long enough to change.</param>
    /// <returns>Why not, or <see cref="DiplomacyRejection.None"/>.</returns>
    public static DiplomacyRejection Acceptance(DiplomacyRules rules, WorldState world, DiplomaticProposal proposal, int me, Func<int, int, bool> settled)
    {
        return proposal.Kind switch
        {
            ProposalKind.Alliance => DiplomacyRejection.None,
            ProposalKind.Peace => TreatyModel.ForbidsPeace(world, me, proposal.FromGodId) ? DiplomacyRejection.TreatyForbids : DiplomacyRejection.None,
            ProposalKind.Treaty => Treaty(rules.Politics, world, proposal.FromGodId, me, proposal.Terms),
            ProposalKind.Request => Grantable(world, proposal, me, settled),
        };
    }

    /// <summary>Whether a god can grant a request now.</summary>
    private static DiplomacyRejection Grantable(WorldState world, DiplomaticProposal proposal, int me, Func<int, int, bool> settled)
    {
        if (proposal.Ask is not { } ask)
        {
            return DiplomacyRejection.InvalidTerms;
        }

        return ask.Kind switch
        {
            AskKind.DivinePower => GodStanding.Find(world, me) is { } god && god.DivinePower >= ask.Amount ? DiplomacyRejection.None : DiplomacyRejection.CannotGrant,
            AskKind.WarSupport => ask.TargetGodId is not { } target || GodStanding.Find(world, target) is null ? DiplomacyRejection.InvalidTerms
                : DiplomacyModel.AreAtWar(world, me, target) ? DiplomacyRejection.None
                : TreatyModel.ForbidsWar(world, me, target) ? DiplomacyRejection.TreatyForbids
                : !settled(me, target) ? DiplomacyRejection.TooSoon
                : DiplomacyRejection.None,
            AskKind.Backing or AskKind.Territory or AskKind.Assistance => DiplomacyRejection.None,
        };
    }
}
