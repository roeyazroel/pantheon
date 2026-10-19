using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Politics;

/// <summary>
/// Signs, ends and reveals treaties (spec §38–40, ADR-025). Breaking a treaty is a betrayal: the
/// partner remembers it, every god that knew of the treaty trusts the breaker less, and the
/// world notes the deceit.
/// </summary>
public sealed class TreatyBook
{
    private readonly DiplomacyRules _rules;
    private readonly IActRecorder _ledger;

    /// <summary>Creates the book.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="ledger">Where acts are recorded.</param>
    public TreatyBook(DiplomacyRules rules, IActRecorder ledger)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        _rules = rules;
        _ledger = ledger;
    }

    /// <summary>Puts a treaty in force.</summary>
    /// <param name="world">World state.</param>
    /// <param name="proposer">The god that proposed it.</param>
    /// <param name="accepter">The god that accepted it.</param>
    /// <param name="terms">Its terms.</param>
    /// <param name="events">Receives <see cref="TreatySigned"/>.</param>
    /// <returns>The treaty.</returns>
    public Treaty Add(WorldState world, int proposer, int accepter, TreatyTerms terms, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(events);
        var diplomacy = world.Diplomacy;
        var treaty = new Treaty
        {
            Id = diplomacy.NextTreatyId++,
            Kind = terms.Kind,
            FirstGodId = proposer,
            SecondGodId = accepter,
            TargetGodId = terms.TargetGodId,
            PayerGodId = terms.PayerGodId,
            Amount = terms.Amount,
            Secret = terms.Secret,
            SignedTick = world.Tick,
            EndsTick = terms.DurationSeconds > 0f ? world.Tick + (long)MathF.Round(terms.DurationSeconds * SimulationTime.TicksPerSecond) : null,
        };
        diplomacy.Treaties.Add(treaty);
        _ledger.Record(world, proposer, accepter, DiplomaticAct.SignedTreaty, null);
        _ledger.Record(world, accepter, proposer, DiplomaticAct.SignedTreaty, null);
        Culture.ConductModel.Note(world, proposer, Culture.ConductModel.Act.Faithfulness, 0.5f);
        Culture.ConductModel.Note(world, accepter, Culture.ConductModel.Act.Faithfulness, 0.5f);
        events.Add(new TreatySigned { Tick = world.Tick, TreatyId = treaty.Id, Kind = treaty.Kind, FirstGodId = proposer, SecondGodId = accepter, TargetGodId = treaty.TargetGodId, Secret = treaty.Secret });
        return treaty;
    }

    /// <summary>
    /// Ends a treaty. When a god breaks it, the partner remembers the broken treaty, every other god
    /// that knew of it trusts the breaker less, and the breaker is seen as deceitful (spec §40).
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="treaty">The treaty.</param>
    /// <param name="breakerGodId">The god that broke it; <c>null</c> when it ran its term.</param>
    /// <param name="events">Receives <see cref="TreatyEnded"/>.</param>
    public void End(WorldState world, Treaty treaty, int? breakerGodId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(treaty);
        ArgumentNullException.ThrowIfNull(events);
        if (!world.Diplomacy.Treaties.Remove(treaty))
        {
            return;
        }

        if (breakerGodId is { } breaker)
        {
            _ledger.Record(world, treaty.PartnerOf(breaker), breaker, DiplomaticAct.BrokeTreaty, null);
            Culture.ConductModel.Note(world, breaker, Culture.ConductModel.Act.Deceit, 1f);
            if (!treaty.Secret)
            {
                var loss = _rules.BetrayalReputationLoss * _rules.Politics.TreatyBetrayalShare;
                foreach (var god in world.Gods)
                {
                    if (!treaty.Binds(god.Id))
                    {
                        var view = DiplomacyModel.EnsureRelation(_rules, world, god.Id, breaker);
                        view.Trust = SimMath.Clamp01(view.Trust - loss);
                    }
                }
            }
        }

        events.Add(new TreatyEnded
        {
            Tick = world.Tick, TreatyId = treaty.Id, Kind = treaty.Kind, FirstGodId = treaty.FirstGodId, SecondGodId = treaty.SecondGodId,
            BrokenByGodId = breakerGodId, Secret = treaty.Secret,
        });
    }

    /// <summary>
    /// Brings a secret treaty to light (spec §39, §43). The god it was aimed at learns both parties
    /// plotted against it; an ally of that god among them betrayed it.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="treaty">The treaty.</param>
    /// <param name="events">Receives <see cref="TreatyRevealed"/>.</param>
    public void Reveal(WorldState world, Treaty treaty, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(treaty);
        ArgumentNullException.ThrowIfNull(events);
        if (!treaty.Secret)
        {
            return;
        }

        treaty.Secret = false;
        if (treaty.TargetGodId is { } target)
        {
            foreach (var party in new[] { treaty.FirstGodId, treaty.SecondGodId })
            {
                _ledger.Record(world, target, party, DiplomacyModel.AreAllied(world, target, party) ? DiplomaticAct.Betrayed : DiplomaticAct.PlottedAgainst, null);
            }
        }

        events.Add(new TreatyRevealed { Tick = world.Tick, TreatyId = treaty.Id, Kind = treaty.Kind, FirstGodId = treaty.FirstGodId, SecondGodId = treaty.SecondGodId, TargetGodId = treaty.TargetGodId });
    }

    /// <summary>Ends every treaty binding two gods that a war between them breaks, as broken by the god that declared it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="aggressor">The god that declared war.</param>
    /// <param name="victim">The other.</param>
    /// <param name="events">Receives <see cref="TreatyEnded"/>.</param>
    public void EndByWar(WorldState world, int aggressor, int victim, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var treaty in world.Diplomacy.Treaties.FindAll(t => t.Binds(aggressor) && t.Binds(victim) && t.Kind != TreatyKind.Tribute))
        {
            End(world, treaty, aggressor, events);
        }
    }
}
