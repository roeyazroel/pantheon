using System;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Politics;

/// <summary>Favours owed between gods (spec §41, ADR-025).</summary>
public static class FavourBook
{
    /// <summary>The favour one god owes another, if any.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creditorGodId">The god owed.</param>
    /// <param name="debtorGodId">The god that owes.</param>
    /// <returns>The favour, or <c>null</c>.</returns>
    public static Favour? Find(WorldState world, int creditorGodId, int debtorGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Favours.Find(f => f.CreditorGodId == creditorGodId && f.DebtorGodId == debtorGodId);
    }

    /// <summary>A god owes another a favour more for major help, up to the cap.</summary>
    /// <param name="rules">Politics rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="creditorGodId">The god that helped.</param>
    /// <param name="debtorGodId">The god whose followers it saved.</param>
    /// <param name="settlementId">The settlement saved.</param>
    public static void Owe(PoliticsRules rules, WorldState world, int creditorGodId, int debtorGodId, int? settlementId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (creditorGodId == debtorGodId)
        {
            return;
        }

        if (Find(world, creditorGodId, debtorGodId) is { } favour)
        {
            favour.Weight = Math.Min(rules.MaxFavours, favour.Weight + 1f);
            (favour.SinceTick, favour.SettlementId) = (world.Tick, settlementId);
            return;
        }

        world.Diplomacy.Favours.Add(new Favour { CreditorGodId = creditorGodId, DebtorGodId = debtorGodId, Weight = 1f, SinceTick = world.Tick, SettlementId = settlementId });
    }

    /// <summary>One favour is settled, repaid or refused; the favour is gone when nothing more is owed.</summary>
    /// <param name="world">World state.</param>
    /// <param name="favour">The favour.</param>
    public static void Settle(WorldState world, Favour favour)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(favour);
        favour.Weight -= 1f;
        if (favour.Weight < 0.5f)
        {
            world.Diplomacy.Favours.Remove(favour);
        }
    }
}
