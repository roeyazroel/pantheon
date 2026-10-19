using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.HolySites;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.HolySites;

/// <summary>
/// One step of the holy sites (spec §51, ADR-021). The god whose influence holds a site, far enough
/// ahead of every other god, works to take it; after <c>ClaimSeconds</c> without a break it keeps it,
/// and the god that kept it before remembers. A keeper gains the site's power, its influence (in
/// <see cref="InfluenceField"/>) and its boon: blessed land, healing or a wiser Creature, or cheaper
/// miracles (in the miracle authority).
/// </summary>
public sealed class HolySiteSystem : ISimulationSystem
{
    private readonly HolySiteRules _rules;
    private readonly DivineRules _divine;
    private readonly CreatureRules? _creatures;
    private readonly IActRecorder? _ledger;

    /// <summary>Creates the system over validated rules.</summary>
    /// <param name="rules">Holy site rules.</param>
    /// <param name="divine">Divine rules, for influence.</param>
    /// <param name="creatures">Creature rules, for the experience a site gives; <c>null</c> where there are no Creatures.</param>
    /// <param name="ledger">Where a god notes that another took its site; <c>null</c> for none.</param>
    public HolySiteSystem(HolySiteRules rules, DivineRules divine, CreatureRules? creatures = null, IActRecorder? ledger = null)
    {
        HolySiteRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(divine);
        _rules = rules;
        _divine = divine;
        _creatures = creatures;
        _ledger = ledger;
    }

    /// <summary>The holy site rules.</summary>
    public HolySiteRules Rules => _rules;

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        if (world.HolySites.Count == 0)
        {
            return;
        }

        var fields = new List<InfluenceField>(world.Gods.Count);
        foreach (var god in world.Gods)
        {
            fields.Add(InfluenceField.Build(_divine, world, god.Id));
        }

        foreach (var site in world.HolySites)
        {
            Contest(world, site, fields, context.Events);
            if (site.KeeperGodId is { } keeper && HolySiteModel.Find(_rules, site.Kind) is { } definition)
            {
                Reward(world, site, keeper, definition, context.Events);
            }
        }
    }

    /// <summary>The god leading at a site and by how much: its id, its influence and its lead over the next god.</summary>
    /// <param name="world">World state.</param>
    /// <param name="site">The site.</param>
    /// <param name="fields">Every god's influence, in world order.</param>
    /// <returns>The leader (or <c>null</c>), its influence and its lead.</returns>
    public static (int? GodId, float Influence, float Lead) Leader(WorldState world, HolySite site, IReadOnlyList<InfluenceField> fields)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(fields);
        int? leader = null;
        var best = 0f;
        var second = 0f;
        foreach (var field in fields)
        {
            var influence = field.At(site.Position);
            if (influence > best)
            {
                second = best;
                best = influence;
                leader = field.GodId;
            }
            else if (influence > second)
            {
                second = influence;
            }
        }

        return (leader, best, best - second);
    }

    /// <summary>Moves the claim on a site on by one step, and hands the site over when it is complete.</summary>
    private void Contest(WorldState world, HolySite site, IReadOnlyList<InfluenceField> fields, List<WorldEvent> events)
    {
        var (leader, influence, lead) = Leader(world, site, fields);
        if (leader is not { } claimant || influence < _rules.ClaimInfluence || lead < _rules.ClaimLead || claimant == site.KeeperGodId)
        {
            site.ClaimantGodId = null;
            site.ClaimTicks = 0;
            return;
        }

        if (site.ClaimantGodId != claimant && site.KeeperGodId is { } holder)
        {
            events.Add(new HolySiteContested { Tick = world.Tick, SiteId = site.Id, GodId = claimant, KeeperGodId = holder });
        }

        site.ClaimTicks = site.ClaimantGodId == claimant ? site.ClaimTicks + _rules.StepIntervalTicks : _rules.StepIntervalTicks;
        site.ClaimantGodId = claimant;
        if (site.ClaimTicks < (long)MathF.Round(_rules.ClaimSeconds * SimulationTime.TicksPerSecond))
        {
            return;
        }

        var previous = site.KeeperGodId;
        site.KeeperGodId = claimant;
        site.ClaimantGodId = null;
        site.ClaimTicks = 0;
        events.Add(new HolySiteClaimed { Tick = world.Tick, SiteId = site.Id, GodId = claimant, PreviousGodId = previous });
        if (previous is { } loser)
        {
            _ledger?.Record(world, loser, claimant, DiplomaticAct.TookHolySite, null);
        }
    }

    /// <summary>What keeping a site gives its keeper this step.</summary>
    private void Reward(WorldState world, HolySite site, int keeper, HolySiteDefinition definition, List<WorldEvent> events)
    {
        if (GodStanding.Find(world, keeper) is { } god)
        {
            god.DivinePower += definition.PowerPerStep;
        }

        switch (definition.Boon)
        {
            case HolySiteBoon.Blessing:
                foreach (var region in world.Regions.Cells)
                {
                    if (region.Biome != Domain.Ecology.Biome.Sea && HolySiteModel.Distance(world.Regions.GetRegionCenter(region.Id), site.Position) <= definition.BoonReach)
                    {
                        region.Blessing = Math.Max(region.Blessing, definition.BoonAmount);
                    }
                }

                break;
            case HolySiteBoon.Healing:
                foreach (var settlement in world.Settlements)
                {
                    if (DomainModel.MajorityGodId(settlement) == keeper && HolySiteModel.Distance(settlement.Position, site.Position) <= definition.BoonReach)
                    {
                        settlement.Health = BeliefModel.ApplyChange(settlement.Health, definition.BoonAmount);
                    }
                }

                break;
            case HolySiteBoon.Wisdom when _creatures is not null:
                foreach (var creature in world.Creatures)
                {
                    if (creature.GodId == keeper && creature.Activity.Phase != CreaturePhase.Banished && HolySiteModel.Distance(creature.Position, site.Position) <= definition.BoonReach
                        && CreatureModel.AddExperience(_creatures, creature, definition.BoonAmount))
                    {
                        events.Add(new CreatureChanged { Tick = world.Tick, CreatureId = creature.Id, Change = CreatureChange.LevelledUp });
                    }
                }

                break;
            case HolySiteBoon.Wisdom:
            case HolySiteBoon.CheaperMiracles:
                break;
        }
    }
}
