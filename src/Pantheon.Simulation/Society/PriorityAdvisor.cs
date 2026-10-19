using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Society;

/// <summary>
/// What an AI god asks of the settlements it leads (spec §29, §47, ADR-023): first what the people
/// lack (food, health, preparation against danger, walls in war), then a temple of its own, then
/// expansion for the ambitious, and otherwise worship or growth by its nature.
/// </summary>
public sealed class PriorityAdvisor
{
    // How bad a need must be before the god answers it.
    private const float Hunger = 0.6f;
    private const float Illness = 0.4f;
    private const float Peril = 0.2f;
    private const float Contested = 0.2f;

    // Crowding at which an ambitious god sends settlers.
    private const float Crowded = 0.85f;

    private readonly SocietyRules _rules;
    private readonly SettlementRules _settlementRules;
    private readonly DivineRules _divine;
    private readonly SocietyAuthority _authority;

    /// <summary>Creates the advisor.</summary>
    public PriorityAdvisor(SocietyRules rules, SettlementRules settlementRules, DivineRules divine, SocietyAuthority authority)
    {
        SocietyRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(authority);
        _rules = rules;
        _settlementRules = settlementRules;
        _divine = divine;
        _authority = authority;
    }

    /// <summary>The priority the god wants for a settlement it leads, and why.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The priority and a short reason.</returns>
    public (SettlementPriority Priority, string Reason) Choose(WorldState world, GodState god, GodProfile profile, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(settlement);
        var hazards = Hazards.Of(_settlementRules, world, settlement);
        var conditions = BeliefModel.AssessConditions(_divine, settlement, hazards);
        if (conditions.FoodNeed > Hunger)
        {
            return (SettlementPriority.Food, "hunger");
        }

        if (conditions.HealthNeed > Illness)
        {
            return (SettlementPriority.Health, "illness");
        }

        if ((conditions.DangerNeed > Peril || settlement.Ruins > 0 || hazards.Thirst > Peril) && Wants(world, settlement, god, SettlementPriority.Preparation))
        {
            return (SettlementPriority.Preparation, "danger");
        }

        if (SocietyModel.War(world, settlement) > Contested && Wants(world, settlement, god, SettlementPriority.Defence))
        {
            return (SettlementPriority.Defence, "war");
        }

        if (!CivicModel.HasTemple(settlement, god.Id))
        {
            return (SettlementPriority.Temple, "no temple of its own");
        }

        var ambitious = profile.Convert >= 0.8f || profile.Aggression >= 0.9f;
        if (ambitious && settlement.Population >= _rules.Expansion.MinPopulation && settlement.Population >= Crowded * settlement.Housing)
        {
            return (SettlementPriority.Expansion, "crowded and ambitious");
        }

        return profile.Protect >= profile.Intimidate ? (SettlementPriority.Growth, "its nature") : (SettlementPriority.Worship, "its nature");
    }

    /// <summary>Sets, through the authority, the priority the god wants in every settlement it leads.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <param name="events">Receives the changes.</param>
    public void Act(WorldState world, GodState god, GodProfile profile, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population <= 0 || !Leads(settlement, god.Id))
            {
                continue;
            }

            var (priority, _) = Choose(world, god, profile, settlement);
            if (priority != settlement.Society.Priority)
            {
                _authority.Apply(world, new PriorityCommand(god.Id, settlement.Id, priority), god.Id, events);
            }
        }
    }

    /// <summary>Whether the god is followed by the largest share of a settlement, and enough of it to set priorities.</summary>
    private bool Leads(SettlementState settlement, int godId)
    {
        var mine = BeliefModel.Find(settlement, godId)?.Adherence ?? 0f;
        if (mine < _rules.PriorityShare)
        {
            return false;
        }

        foreach (var belief in settlement.Beliefs)
        {
            if (belief.GodId != godId && belief.Adherence > mine)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether a priority would still raise something in the settlement.</summary>
    private bool Wants(WorldState world, SettlementState settlement, GodState god, SettlementPriority priority)
    {
        var society = settlement.Society;
        var (before, beforeGod) = (society.Priority, society.PriorityGodId);
        (society.Priority, society.PriorityGodId) = (priority, god.Id);
        var next = SocietyModel.NextProject(_rules, world, settlement);
        (society.Priority, society.PriorityGodId) = (before, beforeGod);
        return next is not null || society.Project is not null;
    }
}
