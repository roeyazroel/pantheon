using System;
using System.Collections.Generic;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on treaties, favours, promises and the Divine Council (part of <see cref="SaveIntegrity"/>, ADR-025).</summary>
internal static class PoliticsIntegrity
{
    /// <summary>Records every inconsistency of the world's politics.</summary>
    /// <param name="world">The world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        var diplomacy = world.Diplomacy;
        if (diplomacy.Treaties is null || diplomacy.Favours is null || diplomacy.Obligations is null || diplomacy.Council is null)
        {
            errors.Add("Diplomacy has no treaties, favours, promises or council.");
            return;
        }

        ValidateTreaties(diplomacy.Treaties, diplomacy.NextTreatyId, godIds, errors);
        foreach (var favour in diplomacy.Favours)
        {
            if (favour is null || !godIds.Contains(favour.CreditorGodId) || !godIds.Contains(favour.DebtorGodId) || favour.CreditorGodId == favour.DebtorGodId
                || !(favour.Weight > 0f) || !float.IsFinite(favour.Weight))
            {
                errors.Add("A favour names a missing god, a god owing itself, or an amount that is not positive.");
            }
        }

        foreach (var promise in diplomacy.Obligations)
        {
            if (promise is null || !godIds.Contains(promise.CreditorGodId) || !godIds.Contains(promise.DebtorGodId) || !Enum.IsDefined(promise.Kind) || promise.DueTick < promise.SinceTick)
            {
                errors.Add("A promise names a missing god, an unknown kind, or a deadline before it was made.");
            }
        }

        ValidateCouncil(world, diplomacy.Council, godIds, errors);
    }

    /// <summary>Checks treaty ids, kinds, parties and terms.</summary>
    private static void ValidateTreaties(List<Treaty> treaties, int nextId, HashSet<int> godIds, List<string> errors)
    {
        var ids = new HashSet<int>();
        foreach (var treaty in treaties)
        {
            if (treaty is null)
            {
                errors.Add("Treaty list contains null.");
                continue;
            }

            if (!ids.Add(treaty.Id) || treaty.Id >= nextId || treaty.Id < 1)
            {
                errors.Add($"Treaty {treaty.Id} is repeated or not below the next id {nextId}.");
            }

            if (!Enum.IsDefined(treaty.Kind) || !godIds.Contains(treaty.FirstGodId) || !godIds.Contains(treaty.SecondGodId) || treaty.FirstGodId == treaty.SecondGodId
                || (treaty.TargetGodId is { } target && !godIds.Contains(target)))
            {
                errors.Add($"Treaty {treaty.Id} has an unknown kind, a missing god or one god twice.");
            }

            var tribute = treaty.Kind == TreatyKind.Tribute;
            if (tribute != (treaty.PayerGodId is not null) || (tribute && !treaty.Binds(treaty.PayerGodId!.Value)) || !(treaty.Amount >= 0f) || !float.IsFinite(treaty.Amount))
            {
                errors.Add($"Treaty {treaty.Id} has a payer or amount that does not fit its kind.");
            }

            if (treaty.EndsTick is { } end && end < treaty.SignedTick)
            {
                errors.Add($"Treaty {treaty.Id} ends before it was signed.");
            }
        }
    }

    /// <summary>Checks the council's session, motions and decrees.</summary>
    private static void ValidateCouncil(WorldState world, CouncilState council, HashSet<int> godIds, List<string> errors)
    {
        if (council.Tabled is null || council.Decrees is null)
        {
            errors.Add("The council has no tabled motions or decrees.");
            return;
        }

        foreach (var motion in council.Tabled)
        {
            ValidateMotion(world, motion, godIds, errors);
        }

        foreach (var decree in council.Decrees)
        {
            if (decree is null || decree.EndsTick < decree.PassedTick)
            {
                errors.Add("A decree ends before it passed.");
                continue;
            }

            ValidateMotion(world, decree.Motion, godIds, errors);
        }

        if (council.Session is not { } session)
        {
            return;
        }

        ValidateMotion(world, session.Motion, godIds, errors);
        var voters = new HashSet<int>();
        if (!Enum.IsDefined(session.Trigger) || session.ClosesTick < session.OpenedTick || session.Votes is null
            || session.Votes.Exists(v => v is null || !godIds.Contains(v.GodId) || !voters.Add(v.GodId) || !Enum.IsDefined(v.Choice)))
        {
            errors.Add("The council's session has an unknown trigger, closes before it opened, or a vote by a missing god, twice or unknown.");
        }
    }

    /// <summary>Checks a motion names a known kind and known gods, sites and settlements.</summary>
    private static void ValidateMotion(WorldState world, CouncilMotion? motion, HashSet<int> godIds, List<string> errors)
    {
        if (motion is null || !Enum.IsDefined(motion.Kind) || (motion.ProposerGodId is { } proposer && !godIds.Contains(proposer))
            || (motion.TargetGodId is { } target && !godIds.Contains(target))
            || (motion.SiteId is { } site && !world.HolySites.Exists(s => s.Id == site))
            || (motion.SettlementId is { } settlement && !world.Settlements.Exists(s => s.Id == settlement))
            || (motion.Practice is { } practice && !Enum.IsDefined(practice)))
        {
            errors.Add("A council motion has an unknown kind or names a missing god, site, settlement or practice.");
        }
    }
}
