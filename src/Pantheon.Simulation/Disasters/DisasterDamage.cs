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
    private readonly Domain.Creatures.CallingRules? _callings;

    /// <summary>Creates the damage over validated rules.</summary>
    public DisasterDamage(DisasterRules rules, SettlementRules settlementRules, DivineRules divine, DomainRules? domains, Domain.Creatures.CallingRules? callings = null)
    {
        _rules = rules;
        _settlementRules = settlementRules;
        _divine = divine;
        _domains = domains;
        _callings = callings;
    }

    /// <summary>Share of disaster damage a settlement takes: all of it, a little under a Shield, and less with a Guardian Creature of its god near (ADR-030).</summary>
    public float Protection(WorldState world, SettlementState settlement) =>
        (DisasterModel.IsShielded(settlement, world.Tick) ? _rules.ShieldedDamage : 1f) * (_callings is null ? 1f : Creatures.CallingEffects.Shield(_callings, world, settlement));

    /// <summary>Share of the houses a disaster would destroy that fall: fewer where the majority's god holds Protection (ADR-021), and with architecture (ADR-023).</summary>
    public float Fragility(WorldState world, SettlementState settlement)
    {
        var protection = _domains is not null && DomainModel.MajorityHolds(world, settlement, GodDomain.Protection) ? 1f - _domains.Boons.ProtectionResilience : 1f;
        return protection * (1f - Math.Clamp(_rules.Preparedness.ArchitecturePerLevel * settlement.Society.Technology.Architecture, 0f, MaxPrepared));
    }

    /// <summary>
    /// Share of a disaster's harm a settlement takes: firebreaks, levees and a dam, shelters and a sea
    /// wall spare it some (ADR-023, ADR-029), and so does engineering, except in an earthquake.
    /// </summary>
    public float Exposure(SettlementState settlement, DisasterKind kind)
    {
        var prepared = _rules.Preparedness;
        bool Has(Domain.Society.BuildingKind building) => Settlements.CivicModel.Has(settlement, building);
        var works = kind switch
        {
            DisasterKind.Fire => Has(Domain.Society.BuildingKind.Firebreak) ? prepared.FirebreakDamage : 0f,
            DisasterKind.Flood => (Has(Domain.Society.BuildingKind.Levee) ? prepared.LeveeDamage : 0f) + (Has(Domain.Society.BuildingKind.Dam) ? prepared.DamDamage : 0f),
            DisasterKind.Earthquake => 0f,
            DisasterKind.Hurricane => (Has(Domain.Society.BuildingKind.Shelter) ? prepared.ShelterDeaths : 0f) + (Has(Domain.Society.BuildingKind.SeaWall) ? prepared.SeaWallDamage : 0f),
            DisasterKind.Tsunami => Has(Domain.Society.BuildingKind.SeaWall) ? prepared.SeaWallDamage : 0f,
            DisasterKind.Landslide or DisasterKind.Eruption or DisasterKind.Tornado or DisasterKind.Meteor => 0f,
        };
        var engineering = kind == DisasterKind.Earthquake ? 0f : prepared.EngineeringPerLevel * settlement.Society.Technology.Engineering;
        return 1f - Math.Clamp(works + engineering, 0f, MaxPrepared);
    }

    /// <summary>
    /// Share of the deaths a disaster would cause that happen: shelters save people from all but
    /// fire (ADR-023), and evacuation routes from every disaster (ADR-029).
    /// </summary>
    public float Deadliness(SettlementState settlement, DisasterKind kind)
    {
        var sheltered = kind != DisasterKind.Fire && Settlements.CivicModel.Has(settlement, Domain.Society.BuildingKind.Shelter) ? 1f - _rules.Preparedness.ShelterDeaths : 1f;
        var evacuated = Settlements.CivicModel.Has(settlement, Domain.Society.BuildingKind.Evacuation) ? 1f - _rules.Preparedness.EvacuationDeaths : 1f;
        return sheltered * evacuated;
    }

    /// <summary>
    /// Strikes a settlement: destroys a share of its houses and kills a share of its people, by the
    /// severity, as its shield, protection, preparations and technology allow, frightens it and
    /// reports the damage (ADR-029).
    /// </summary>
    public void Strike(WorldState world, SettlementState settlement, DisasterKind kind, float housing, float deaths, float severity, SimulationRandom random, List<WorldEvent> events)
    {
        if (settlement.Population <= 0 || severity <= 0f)
        {
            return;
        }

        severity *= Protection(world, settlement);
        var houses = Destroy(settlement, random, housing * severity * Fragility(world, settlement) * Exposure(settlement, kind));
        var killed = Kill(settlement, random, deaths * severity * Deadliness(settlement, kind) * Exposure(settlement, kind));
        Frighten(settlement, severity);
        Report(world, settlement, kind, houses, killed, events);
    }

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
