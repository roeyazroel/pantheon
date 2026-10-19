using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Xunit;

namespace Pantheon.Tests.Support;

/// <summary>Worlds of four gods for treaty, favour and council tests (ADR-025).</summary>
internal static class PoliticsWorlds
{
    /// <summary>The player's god.</summary>
    public const int Me = 1;

    /// <summary>An opportunist rival.</summary>
    public const int Rival = 2;

    /// <summary>A guardian.</summary>
    public const int Third = 3;

    /// <summary>A tyrant.</summary>
    public const int Fourth = 4;

    /// <summary>Four gods, each with power, over settlements at the given x positions, long settled so any status may change.</summary>
    /// <param name="positionsX">X positions of the settlements.</param>
    /// <returns>The world.</returns>
    public static WorldState Create(params float[] positionsX)
    {
        var world = DivineWorlds.Create(500f, positionsX);
        world.Gods.Add(DivineWorlds.God(Rival, "Rivalis", GodController.Ai, GodPersonality.Opportunist, 500f));
        world.Gods.Add(DivineWorlds.God(Third, "Tertia", GodController.Ai, GodPersonality.Guardian, 500f));
        world.Gods.Add(DivineWorlds.God(Fourth, "Quartus", GodController.Ai, GodPersonality.Tyrant, 500f));
        world.Tick = 10_000;
        return world;
    }

    /// <summary>A diplomacy authority over the pinned rules.</summary>
    /// <returns>The authority.</returns>
    public static DiplomacyAuthority Authority() => DiplomacyWorlds.Authority();

    /// <summary>One god proposes a treaty and the other accepts it; both must succeed.</summary>
    /// <param name="world">World state.</param>
    /// <param name="authority">The authority.</param>
    /// <param name="proposer">The proposing god.</param>
    /// <param name="accepter">The accepting god.</param>
    /// <param name="terms">The terms.</param>
    /// <param name="events">Receives the events.</param>
    /// <returns>The treaty.</returns>
    public static Treaty Sign(WorldState world, DiplomacyAuthority authority, int proposer, int accepter, TreatyTerms terms, List<WorldEvent>? events = null)
    {
        events ??= [];
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(proposer, accepter, DiplomaticCommandKind.ProposeTreaty, Terms: terms), proposer, events));
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(accepter, proposer, DiplomaticCommandKind.Accept), accepter, events));
        return world.Diplomacy.Treaties[^1];
    }

    /// <summary>Terms of a kind, five minutes, in public.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The terms.</returns>
    public static TreatyTerms Terms(TreatyKind kind) => new() { Kind = kind, DurationSeconds = 300f, Secret = false };

    /// <summary>Sets the status of a pair, long since.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other.</param>
    /// <param name="status">Status.</param>
    public static void SetStatus(WorldState world, int a, int b, DiplomaticStatus status)
    {
        var stance = DiplomacyModel.EnsureStance(world, a, b);
        (stance.Status, stance.SinceTick) = (status, 0);
    }

    /// <summary>Runs the politics step a number of times over the pinned rules.</summary>
    /// <param name="world">World state.</param>
    /// <param name="steps">Steps.</param>
    /// <param name="events">Receives the events.</param>
    /// <param name="rules">Diplomacy rules; the pinned ones when <c>null</c>.</param>
    public static void Step(WorldState world, int steps, List<WorldEvent>? events = null, DiplomacyRules? rules = null)
    {
        rules ??= DiplomacyWorlds.Rules;
        var ledger = new DiplomacyLedger(rules);
        var system = new PoliticsSystem(rules, ledger, new DiplomacyAuthority(rules, ledger));
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events ?? []));
            world.Tick += rules.StepIntervalTicks;
        }
    }
}
