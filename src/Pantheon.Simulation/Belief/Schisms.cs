using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Belief;

/// <summary>
/// Schisms (spec §34, ADR-021), part of the belief step. Where the people who follow a settlement's
/// main god come to doubt it deeply, a share of them break away to keep its Old Faith: they worship
/// no god while it lasts and no other god can convert them. They return when the god is loved and
/// trusted there again, and otherwise fall slowly into unbelief.
/// </summary>
public static class Schisms
{
    // An Old Faith smaller than this share of the people has ended.
    private const float EndedShare = 0.005f;

    /// <summary>One step of a settlement's Old Faith, or a new schism.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement, after its beliefs have drifted this step.</param>
    /// <param name="events">Receives <see cref="SchismStarted"/> and <see cref="SchismEnded"/>.</param>
    public static void Step(DivineRules rules, WorldState world, SettlementState settlement, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(events);
        var schism = rules.Schism;
        if (settlement.OldFaith is { } old)
        {
            if (old.Share > 0f)
            {
                Evolve(schism, world, settlement, old, events);
                return;
            }

            if (world.Tick < old.SinceTick + (long)MathF.Round(schism.CooldownSeconds * SimulationTime.TicksPerSecond))
            {
                return;
            }

            settlement.OldFaith = null;
        }

        Split(schism, world, settlement, events);
    }

    /// <summary>The share of a settlement's people who keep an Old Faith now (0 without one).</summary>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The share (0..1).</returns>
    public static float OldFaithShare(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.OldFaith?.Share ?? 0f;
    }

    /// <summary>Whether a god's followers in a settlement are under a Sanctuary now (ADR-021).</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="godId">The god.</param>
    /// <param name="tick">The current tick.</param>
    /// <returns><c>true</c> while the Sanctuary holds.</returns>
    public static bool IsSanctuary(SettlementState settlement, int godId, long tick)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.SanctuaryGodId == godId && tick < settlement.SanctuaryUntilTick;
    }

    /// <summary>Splits off the doubting followers of the settlement's main god, when they are doubtful enough.</summary>
    private static void Split(SchismRules schism, WorldState world, SettlementState settlement, List<WorldEvent> events)
    {
        GodBelief? main = null;
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence >= schism.MinimumAdherence && (main is null || belief.Adherence > main.Adherence))
            {
                main = belief;
            }
        }

        if (main is null || settlement.Population < schism.MinimumPopulation || main.Doubt < schism.Doubt || IsSanctuary(settlement, main.GodId, world.Tick))
        {
            return;
        }

        var share = main.Adherence * schism.Share;
        main.Adherence -= share;
        main.Doubt *= 1f - schism.DoubtRelief;
        settlement.OldFaith = new OldFaith { GodId = main.GodId, Share = share, Returned = 0f, Faded = 0f, SinceTick = world.Tick };
        events.Add(new SchismStarted { Tick = world.Tick, SettlementId = settlement.Id, GodId = main.GodId, People = (int)MathF.Round(share * settlement.Population) });
    }

    /// <summary>Lets an Old Faith return to its god or fade, and ends it once it is small enough.</summary>
    private static void Evolve(SchismRules schism, WorldState world, SettlementState settlement, OldFaith old, List<WorldEvent> events)
    {
        var belief = BeliefModel.Find(settlement, old.GodId);
        if (belief is not null && belief.Love >= schism.ReconcileLove && belief.Doubt <= schism.ReconcileDoubt)
        {
            // The returning people come out of the Old Faith's own share, so the shares never pass 1.
            var back = old.Share * schism.ReturnPerStep;
            belief.Adherence = SimMath.Clamp01(belief.Adherence + back);
            old.Share -= back;
            old.Returned += back;
        }
        else
        {
            var fade = old.Share * schism.FadePerStep;
            old.Share -= fade;
            old.Faded += fade;
        }

        if (old.Share >= EndedShare)
        {
            return;
        }

        // The last few go the way most have gone.
        if (old.Returned > old.Faded && belief is not null)
        {
            belief.Adherence = SimMath.Clamp01(belief.Adherence + old.Share);
            old.Returned += old.Share;
        }
        else
        {
            old.Faded += old.Share;
        }

        old.Share = 0f;
        events.Add(new SchismEnded { Tick = world.Tick, SettlementId = settlement.Id, GodId = old.GodId, Reconciled = old.Returned > old.Faded });
    }
}
