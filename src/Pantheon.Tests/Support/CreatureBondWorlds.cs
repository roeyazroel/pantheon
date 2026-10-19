using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Systems;

namespace Pantheon.Tests.Support;

/// <summary>Divine worlds with two gods' Creatures standing near each other, for Creature relationship tests (ADR-017).</summary>
internal static class CreatureBondWorlds
{
    /// <summary>The shipped Creature rules.</summary>
    public static CreatureRules Rules => TestContent.Shipped.Creatures;

    /// <summary>The shipped bond rules.</summary>
    public static CreatureBondRules Bonds => Rules.Bonds;

    /// <summary>
    /// Two settlements (x = 200 followed by the player Testa, god 1; x = 450 followed by the Tyrant
    /// Rivalis, god 2) and each god's Creature (ids 1 and 2), standing <paramref name="gap"/> metres
    /// apart about x = 325, fed, rested and content.
    /// </summary>
    /// <param name="gap">Distance between the Creatures in metres.</param>
    /// <returns>The world and the two Creatures.</returns>
    public static (WorldState World, CreatureState Mine, CreatureState Theirs) Create(float gap = 20f)
    {
        var world = DivineWorlds.Create(500f, 200f, 450f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 500f));
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.6f, faith: 0.5f, godId: 2);
        var mine = Found(world, 0, "Testling");
        var theirs = Found(world, 1, "Rivaling");
        mine.Position = new GroundPosition(325f - (gap / 2f), 512f);
        theirs.Position = new GroundPosition(325f + (gap / 2f), 512f);
        foreach (var creature in new[] { mine, theirs })
        {
            creature.Needs.Hunger = 0f;
            creature.Needs.Fatigue = 0f;
            creature.Needs.Boredom = 0f;
        }

        return (world, mine, theirs);
    }

    /// <summary>Makes a Creature answer every meeting in a situation with one action: its preference 1, the others' 0.</summary>
    /// <param name="creature">The Creature.</param>
    /// <param name="situation">The meeting situation.</param>
    /// <param name="action">Fight, befriend, flee or ignore.</param>
    public static void Prefer(CreatureState creature, CreatureSituation situation, CreatureAction action)
    {
        foreach (var answer in new[] { CreatureAction.Fight, CreatureAction.Befriend, CreatureAction.Flee, CreatureAction.Ignore })
        {
            var preference = CreatureModel.FindPreference(creature, situation, answer);
            if (preference is null)
            {
                preference = new LearnedPreference { Situation = situation, Action = answer, Weight = 0f };
                creature.Preferences.Add(preference);
            }

            preference.Weight = answer == action ? 1f : 0f;
        }
    }

    /// <summary>A Creature system over the shipped Creature rules (or others), the pinned divine, settlement and diplomacy rules.</summary>
    /// <param name="rules">Creature rules; the shipped ones when <c>null</c>.</param>
    /// <param name="ledger">Where defeats are recorded, if anywhere.</param>
    /// <returns>The system.</returns>
    public static CreatureSystem System(CreatureRules? rules = null, IActRecorder? ledger = null) =>
        new(rules ?? Rules, DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths, DiplomacyWorlds.Rules, ledger);

    /// <summary>Runs only the Creature system for a number of ticks.</summary>
    /// <param name="world">World state.</param>
    /// <param name="system">The system.</param>
    /// <param name="ticks">Ticks to run.</param>
    /// <returns>Every event raised.</returns>
    public static List<WorldEvent> Run(WorldState world, CreatureSystem system, int ticks)
    {
        var engine = new SimulationEngine([system]);
        var events = new List<WorldEvent>();
        for (var i = 0; i < ticks; i++)
        {
            engine.Step(world, events);
        }

        return events;
    }

    /// <summary>Gives the god at an index a Creature born at the settlement of the same index.</summary>
    private static CreatureState Found(WorldState world, int index, string name)
    {
        var god = world.Gods[index];
        var withCreature = new GodState
        {
            Id = god.Id,
            Name = god.Name,
            Controller = god.Controller,
            Personality = god.Personality,
            DominantSinceTick = god.DominantSinceTick,
            DivinePower = god.DivinePower,
            Rank = god.Rank,
            CreatureId = god.Id,
            RefugeePolicy = god.RefugeePolicy,
            Cooldowns = god.Cooldowns,
            Domains = god.Domains,
        };
        world.Gods[index] = withCreature;
        var creature = CreatureFounder.Found(Rules, world.Seed, withCreature, name, world.Settlements[index], world.Regions);
        world.Creatures.Add(creature);
        return creature;
    }
}
