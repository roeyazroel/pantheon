using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Disasters;

/// <summary>
/// What a disaster does to a settlement it reaches (ADR-014, ADR-022): houses fall into ruin, people
/// die, and the survivors fear their gods and doubt them. Shields and the Protection domain lessen
/// it. Shared by the disasters, the floods and the waves gods send.
/// </summary>
internal sealed class DisasterDamage
{
    private readonly DisasterRules _rules;
    private readonly SettlementRules _settlementRules;
    private readonly DivineRules _divine;
    private readonly DomainRules? _domains;

    /// <summary>Creates the damage over validated rules.</summary>
    public DisasterDamage(DisasterRules rules, SettlementRules settlementRules, DivineRules divine, DomainRules? domains)
    {
        _rules = rules;
        _settlementRules = settlementRules;
        _divine = divine;
        _domains = domains;
    }

    /// <summary>Share of disaster damage a settlement takes: all of it, or a little under a Shield.</summary>
    public float Protection(SettlementState settlement, long tick) =>
        DisasterModel.IsShielded(settlement, tick) ? _rules.ShieldedDamage : 1f;

    /// <summary>Share of the houses a disaster would destroy that fall: fewer where the majority's god holds Protection (ADR-021), and with architecture (ADR-023).</summary>
    public float Fragility(WorldState world, SettlementState settlement)
    {
        var protection = _domains is not null && DomainModel.MajorityHolds(world, settlement, GodDomain.Protection) ? 1f - _domains.Boons.ProtectionResilience : 1f;
        return protection * (1f - Math.Clamp(_rules.Preparedness.ArchitecturePerLevel * settlement.Society.Technology.Architecture, 0f, MaxPrepared));
    }

    /// <summary>Share of a fire's or a flood's harm a settlement takes: firebreaks or levees, and engineering, spare it some (ADR-023).</summary>
    public float Exposure(SettlementState settlement, DisasterKind kind)
    {
        var prepared = _rules.Preparedness;
        var works = kind switch
        {
            DisasterKind.Fire => Settlements.CivicModel.Has(settlement, Domain.Society.BuildingKind.Firebreak) ? prepared.FirebreakDamage : 0f,
            DisasterKind.Flood => Settlements.CivicModel.Has(settlement, Domain.Society.BuildingKind.Levee) ? prepared.LeveeDamage : 0f,
            DisasterKind.Earthquake => 0f,
            DisasterKind.Hurricane => Settlements.CivicModel.Has(settlement, Domain.Society.BuildingKind.Shelter) ? prepared.ShelterDeaths : 0f,
        };
        var engineering = kind == DisasterKind.Earthquake ? 0f : prepared.EngineeringPerLevel * settlement.Society.Technology.Engineering;
        return 1f - Math.Clamp(works + engineering, 0f, MaxPrepared);
    }

    /// <summary>Share of the deaths a disaster would cause that happen: shelters save people from quakes and floods (ADR-023).</summary>
    public float Deadliness(SettlementState settlement, DisasterKind kind) =>
        kind != DisasterKind.Fire && Settlements.CivicModel.Has(settlement, Domain.Society.BuildingKind.Shelter) ? 1f - _rules.Preparedness.ShelterDeaths : 1f;

    /// <summary>Share of the food a disaster would spoil that is lost: a granary keeps some (ADR-023).</summary>
    public float FoodLoss(SettlementState settlement) =>
        Settlements.CivicModel.Has(settlement, Domain.Society.BuildingKind.Granary) ? 1f - _rules.Preparedness.GranaryFood : 1f;

    // Preparations never spare more than this share of a disaster.
    private const float MaxPrepared = 0.9f;

    /// <summary>Destroys a Poisson draw of the settlement's houses; their housing is lost and they become ruins.</summary>
    /// <returns>Houses destroyed.</returns>
    public int Destroy(SettlementState settlement, SimulationRandom random, float share)
    {
        var houses = SettlementEconomy.ComputeHouseCount(_settlementRules, settlement.Housing);
        var lost = Math.Min(houses, random.NextPoisson(houses * Math.Max(0f, share)));
        settlement.Housing = Math.Max(0, settlement.Housing - (lost * _settlementRules.HousingPerHouse));
        settlement.Ruins += lost;
        return lost;
    }

    /// <summary>Kills a Poisson draw of the settlement's people.</summary>
    /// <returns>People killed.</returns>
    public static int Kill(SettlementState settlement, SimulationRandom random, float share)
    {
        var deaths = Math.Min(settlement.Population, random.NextPoisson(settlement.Population * Math.Max(0f, share)));
        settlement.Population -= deaths;
        return deaths;
    }

    /// <summary>The people fear the gods they know and doubt that the gods care: a disaster is a sign.</summary>
    public void Frighten(SettlementState settlement, float severity)
    {
        var impulse = new BeliefImpulse { Faith = 0f, Love = 0f, Fear = _divine.DisasterFear, Doubt = _divine.DisasterDoubt };
        foreach (var belief in settlement.Beliefs)
        {
            BeliefModel.ApplyImpulse(belief, impulse, severity, loveScale: 1f);
        }
    }

    /// <summary>Reports damage when there was any.</summary>
    public static void Report(WorldState world, SettlementState settlement, DisasterKind cause, int houses, int deaths, List<WorldEvent> events)
    {
        if (houses > 0 || deaths > 0)
        {
            events.Add(new SettlementDamaged { Tick = world.Tick, SettlementId = settlement.Id, Cause = cause, HousesLost = houses, Deaths = deaths });
        }
    }
}
