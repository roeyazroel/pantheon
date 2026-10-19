using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Tests.Support;

/// <summary>Worlds and pinned rules for migration tests (ADR-016).</summary>
internal static class MigrationWorlds
{
    /// <summary>Migration rules with fixed values (the balance shipped with Milestone 10).</summary>
    public static MigrationRules Rules { get; } = new()
    {
        StepIntervalTicks = 10,
        FamineWeight = 1f,
        HomelessWeight = 1f,
        DangerWeight = 0.8f,
        WarWeight = 0.5f,
        WarPresence = 0.2f,
        FearWeight = 0.6f,
        DoubtWeight = 0.5f,
        LoveLoyalty = 0.5f,
        PressureThreshold = 0.3f,
        EmigrationPerSecond = 0.005f,
        UrgeFadePerSecond = 0.02f,
        MinGroupSize = 8,
        GroupShare = 0.05f,
        MaxGroupShare = 0.25f,
        MinRemaining = 15,
        MaxGroups = 20,
        MaxDistance = 900f,
        BetterBy = 0.15f,
        RoomWeight = 1f,
        FoodWeight = 1f,
        SafetyWeight = 0.5f,
        FaithWeight = 0.8f,
        DistanceWeight = 0.6f,
        Speed = 3f,
        FoodTakenPerPerson = 6f,
        FoodPerPersonPerSecond = 0.05f,
        HungerHealthLossPerSecond = 0.01f,
        HealthRecoveryPerSecond = 0.002f,
        DeathsPerSecond = 0.004f,
        AbandonShare = 0.3f,
        WelcomeLove = 0.3f,
        ConversionDemand = 0.6f,
        ConversionFear = 0.3f,
        ConversionDoubt = 0.3f,
        TurnedAwayLoss = 0.3f,
        OverwhelmedHomeless = 0.2f,
    };

    /// <summary>
    /// A <see cref="DivineWorlds"/> world with settlements at the given x positions and three AI gods
    /// besides the player: 2 (Rivalis, Tyrant), 3 (Tertia, Guardian) and 4 (Quarta, Isolationist).
    /// </summary>
    /// <param name="positionsX">X positions of the settlements.</param>
    /// <returns>The world.</returns>
    public static WorldState Create(params float[] positionsX)
    {
        var world = DivineWorlds.Create(100f, positionsX);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 100f));
        world.Gods.Add(DivineWorlds.God(3, "Tertia", GodController.Ai, GodPersonality.Guardian, 100f));
        world.Gods.Add(DivineWorlds.God(4, "Quarta", GodController.Ai, GodPersonality.Isolationist, 100f));
        return world;
    }

    /// <summary>A migration system over the pinned rules that records acts in a ledger.</summary>
    /// <param name="rules">Migration rules; the pinned ones when <c>null</c>.</param>
    /// <returns>The system.</returns>
    public static MigrationSystem System(MigrationRules? rules = null) =>
        new(rules ?? Rules, SettlementWorlds.Rules, DivineWorlds.Rules, new DiplomacyLedger(DiplomacyWorlds.Rules));

    /// <summary>Runs migration steps.</summary>
    /// <param name="system">The system.</param>
    /// <param name="world">World state.</param>
    /// <param name="steps">Steps to run.</param>
    /// <param name="events">Receives the events; a new list when <c>null</c>.</param>
    /// <returns>The events.</returns>
    public static System.Collections.Generic.List<Domain.Events.WorldEvent> Run(MigrationSystem system, WorldState world, int steps, System.Collections.Generic.List<Domain.Events.WorldEvent>? events = null)
    {
        events ??= [];
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events));
            world.Tick += Rules.StepIntervalTicks;
        }

        return events;
    }

    /// <summary>Sets the status of a pair, as if it had begun at tick 0.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other god.</param>
    /// <param name="status">Status.</param>
    public static void SetStatus(WorldState world, int a, int b, DiplomaticStatus status) => PantheonWorlds.SetStatus(world, a, b, status);
}
