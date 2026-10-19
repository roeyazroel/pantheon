using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Politics;

/// <summary>
/// One politics step (spec §38–42, ADR-025): treaties run out, tributes are paid (or defaulted,
/// which breaks them), secret treaties may come to light, promises of assistance are kept or
/// broken, and the Divine Council sits.
/// </summary>
public sealed class PoliticsSystem : ISimulationSystem
{
    /// <summary>The stream secret treaties leak from, derived from the seed and the tick.</summary>
    public const string LeakStream = "politics.leaks";

    private readonly DiplomacyRules _rules;
    private readonly IActRecorder _ledger;
    private readonly DiplomacyAuthority _authority;
    private readonly CouncilChamber _chamber;

    /// <summary>Creates the system.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="ledger">Where acts are recorded.</param>
    /// <param name="authority">The diplomacy authority, for treaties and wars.</param>
    public PoliticsSystem(DiplomacyRules rules, IActRecorder ledger, DiplomacyAuthority authority)
    {
        DiplomacyRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(authority);
        _rules = rules;
        _ledger = ledger;
        _authority = authority;
        _chamber = new CouncilChamber(rules, ledger, authority);
    }

    /// <summary>The council's chamber, for debug tools that summon it.</summary>
    public CouncilChamber Chamber => _chamber;

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var seconds = _rules.StepIntervalTicks * SimulationTime.SecondsPerTick;
        Treaties(world, seconds, context.Events);
        Leaks(world, seconds, context.Events);
        Promises(world, context.Events);
        _chamber.Step(world, context.Events);
    }

    /// <summary>Treaties past their term end; tributes are paid, and a payer that cannot pay breaks its treaty.</summary>
    private void Treaties(WorldState world, float seconds, List<WorldEvent> events)
    {
        foreach (var treaty in world.Diplomacy.Treaties.ToArray())
        {
            if (treaty.EndsTick is { } end && world.Tick >= end)
            {
                _authority.Treaties.End(world, treaty, null, events);
                continue;
            }

            if (treaty.Kind != TreatyKind.Tribute || treaty.PayerGodId is not { } payerId)
            {
                continue;
            }

            var payer = GodStanding.Find(world, payerId);
            var payee = GodStanding.Find(world, treaty.PartnerOf(payerId));
            var due = treaty.Amount / 60f * seconds;
            if (payer is null || payee is null || payer.DivinePower < due)
            {
                _authority.Treaties.End(world, treaty, payerId, events);
                continue;
            }

            payer.DivinePower -= due;
            payee.DivinePower += due;
        }
    }

    /// <summary>Each secret treaty may come to light, at a rate a second (spec §43).</summary>
    private void Leaks(WorldState world, float seconds, List<WorldEvent> events)
    {
        if (!world.Diplomacy.Treaties.Exists(t => t.Secret))
        {
            return;
        }

        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, LeakStream, world.Tick));
        var chance = _rules.Politics.SecretLeakPerSecond * seconds;
        foreach (var treaty in world.Diplomacy.Treaties.FindAll(t => t.Secret))
        {
            if (random.NextFloat() < chance)
            {
                _authority.Treaties.Reveal(world, treaty, events);
            }
        }
    }

    /// <summary>A promise of assistance is kept once the debtor helps the creditor's people, and broken when its time runs out.</summary>
    private void Promises(WorldState world, List<WorldEvent> events)
    {
        foreach (var promise in world.Diplomacy.Obligations.ToArray())
        {
            var kept = promise.Kind == ObligationKind.Assist && Helped(world, promise);
            if (!kept && world.Tick < promise.DueTick)
            {
                continue;
            }

            world.Diplomacy.Obligations.Remove(promise);
            _ledger.Record(world, promise.CreditorGodId, promise.DebtorGodId, kept ? DiplomaticAct.KeptWord : DiplomaticAct.BrokePromise, null);
            events.Add(new ObligationEnded { Tick = world.Tick, DebtorGodId = promise.DebtorGodId, CreditorGodId = promise.CreditorGodId, Kind = promise.Kind, Kept = kept });
        }
    }

    /// <summary>Whether the creditor remembers the debtor helping its people since the promise.</summary>
    private static bool Helped(WorldState world, Obligation promise) =>
        DiplomacyModel.FindRelation(world, promise.CreditorGodId, promise.DebtorGodId) is { } view
        && view.Memories.Exists(m => m.Tick >= promise.SinceTick && m.Act is DiplomaticAct.Helped or DiplomaticAct.Rescued);
}
