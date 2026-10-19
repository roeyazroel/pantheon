using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Weather;

namespace Pantheon.Simulation.Systems;

/// <summary>
/// Runs the simulation systems in a fixed order, one tick at a time. Every step is a pure
/// function of the incoming <see cref="WorldState"/> (including its RNG state), so a loaded save
/// continues exactly like an uninterrupted run.
/// </summary>
public sealed class SimulationEngine
{
    private readonly ISimulationSystem[] _systems;

    /// <summary>
    /// Creates the settlement-only pipeline of Milestone 2: the statistical settlement step, run
    /// once every <see cref="SettlementRules.StepIntervalTicks"/> ticks. Belief does not change.
    /// </summary>
    /// <param name="settlementRules">Tuning of the statistical settlement model.</param>
    public SimulationEngine(SettlementRules settlementRules)
        : this([CreateSettlementStep(settlementRules)])
    {
    }

    /// <summary>
    /// Creates the game's pipeline: the settlement step, then the belief step (ADR-009), each on its
    /// own interval. On a tick where both are due, the belief step sees the settlement step's result.
    /// </summary>
    /// <param name="settlementRules">Tuning of the statistical settlement model.</param>
    /// <param name="divineRules">Tuning of belief, worship and Divine Power.</param>
    public SimulationEngine(SettlementRules settlementRules, DivineRules divineRules)
        : this([CreateSettlementStep(settlementRules), CreateBeliefStep(divineRules, settlementRules)])
    {
    }

    /// <summary>
    /// Creates the game's pipeline with Creatures: the settlement step, the belief step, then every
    /// Creature every tick (ADR-010).
    /// </summary>
    /// <param name="settlementRules">Tuning of the statistical settlement model.</param>
    /// <param name="divineRules">Tuning of belief, worship and Divine Power.</param>
    /// <param name="creatureRules">Tuning of Creatures.</param>
    public SimulationEngine(SettlementRules settlementRules, DivineRules divineRules, CreatureRules creatureRules)
    {
        Creatures = new CreatureSystem(creatureRules, divineRules, settlementRules);
        _systems = [CreateSettlementStep(settlementRules), CreateBeliefStep(divineRules, settlementRules), Creatures];
    }

    /// <summary>
    /// Creates the full game pipeline (ADR-011 to ADR-016): the weather step, the disaster step, the
    /// settlement step, the belief step, the victory check, the diplomacy step and the migration step
    /// on their intervals, then every Creature every tick, then the AI gods' strategic evaluations
    /// (and refugee policies) on theirs. One ledger records
    /// every act gods notice of each other, and AI gods act through the same
    /// <see cref="Authorities"/> as a player.
    /// </summary>
    /// <param name="settlementRules">Tuning of the statistical settlement model.</param>
    /// <param name="divineRules">Tuning of belief, worship and Divine Power.</param>
    /// <param name="creatureRules">Tuning of Creatures.</param>
    /// <param name="godAiRules">Tuning of the gods' utility AI.</param>
    /// <param name="miracles">The miracles.</param>
    /// <param name="diplomacyRules">Tuning of relations between gods.</param>
    /// <param name="weatherRules">Tuning of weather and soil water.</param>
    /// <param name="disasterRules">Tuning of wildfires and earthquakes.</param>
    /// <param name="migrationRules">Tuning of migration.</param>
    /// <param name="historyRules">What is significant enough for the world's history; <c>null</c> to keep none (ADR-020).</param>
    public SimulationEngine(SettlementRules settlementRules, DivineRules divineRules, CreatureRules creatureRules, GodAiRules godAiRules, MiracleBook miracles, DiplomacyRules diplomacyRules, WeatherRules weatherRules, DisasterRules disasterRules, MigrationRules migrationRules, Domain.History.HistoryRules? historyRules = null)
    {
        History = historyRules is null ? null : new History.HistoryRecorder(historyRules);
        var ledger = new DiplomacyLedger(diplomacyRules);
        Creatures = new CreatureSystem(creatureRules, divineRules, settlementRules, diplomacyRules, ledger);
        Authorities = new Authorities(
            new MiracleCaster(divineRules, settlementRules, miracles, ledger, weatherRules, disasterRules),
            new CreatureAuthority(creatureRules, Creatures.Brain),
            new DiplomacyAuthority(diplomacyRules, ledger),
            new MigrationAuthority());
        var diplomat = new GodDiplomat(diplomacyRules, divineRules, Authorities.Diplomacy);
        Gods = new GodAiSystem(
            new GodAi(godAiRules, divineRules, miracles, Authorities.Miracles, Authorities.Creatures, creatureRules, diplomat),
            godAiRules.EvaluationIntervalTicks,
            new RefugeeAdvisor(migrationRules, Authorities.Migration));
        Migration = new MigrationSystem(migrationRules, settlementRules, divineRules, ledger);
        _systems =
        [
            new IntervalSystem(new WeatherSystem(weatherRules, settlementRules), weatherRules.StepIntervalTicks),
            new IntervalSystem(new DisasterSystem(disasterRules, settlementRules, divineRules, weatherRules), disasterRules.StepIntervalTicks),
            CreateSettlementStep(settlementRules),
            new IntervalSystem(new BeliefSystem(divineRules, settlementRules, diplomacyRules, ledger), divineRules.StepIntervalTicks),
            new IntervalSystem(new VictorySystem(divineRules), divineRules.StepIntervalTicks),
            new IntervalSystem(new DiplomacySystem(diplomacyRules), diplomacyRules.StepIntervalTicks),
            new IntervalSystem(Migration, migrationRules.StepIntervalTicks),
            Creatures,
            Gods,
        ];
    }

    /// <summary>The authorities of the full pipeline, or <c>null</c> when the engine has none.</summary>
    public Authorities? Authorities { get; }

    /// <summary>The Creature system of the game's pipeline, or <c>null</c> when the engine has none.</summary>
    public CreatureSystem? Creatures { get; }

    /// <summary>The AI gods' system of the full pipeline, or <c>null</c> when the engine has none.</summary>
    public GodAiSystem? Gods { get; }

    /// <summary>The migration system of the full pipeline, or <c>null</c> when the engine has none.</summary>
    public MigrationSystem? Migration { get; }

    /// <summary>The world's chronicler (ADR-020), or <c>null</c> when the engine keeps no history.</summary>
    public History.HistoryRecorder? History { get; }

    /// <summary>Creates an engine with an explicit system order.</summary>
    /// <param name="systems">Systems, in the order they run each tick.</param>
    public SimulationEngine(IReadOnlyList<ISimulationSystem> systems)
    {
        ArgumentNullException.ThrowIfNull(systems);
        _systems = [.. systems];
    }

    /// <summary>Advances the world by one tick.</summary>
    /// <param name="world">World to mutate.</param>
    /// <param name="events">Receives the events raised during the tick; <c>null</c> to discard them.</param>
    public void Step(WorldState world, List<WorldEvent>? events = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        var random = SimulationRandom.FromState(world.Random);
        var context = new TickContext(world, random, events ?? []);
        var first = context.Events.Count;
        foreach (var system in _systems)
        {
            system.Update(context);
        }

        History?.Record(world, context.Events, first);

        world.Random = random.GetState();
        world.Tick++;
    }

    /// <summary>Adds the significant events among those a command raised outside a tick to the world's history (ADR-020).</summary>
    /// <param name="world">World state.</param>
    /// <param name="events">The events.</param>
    /// <param name="from">The first event the command raised.</param>
    public void Chronicle(WorldState world, IReadOnlyList<WorldEvent> events, int from) => History?.Record(world, events, from);

    /// <summary>Advances the world by several ticks.</summary>
    /// <param name="world">World to mutate.</param>
    /// <param name="ticks">Number of ticks; must not be negative.</param>
    public void Run(WorldState world, int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        for (var i = 0; i < ticks; i++)
        {
            Step(world);
        }
    }

    /// <summary>Wraps a validated <see cref="SettlementSystem"/> in its step interval.</summary>
    private static IntervalSystem CreateSettlementStep(SettlementRules rules)
    {
        var settlements = new SettlementSystem(rules);
        return new IntervalSystem(settlements, rules.StepIntervalTicks);
    }

    /// <summary>Wraps a validated <see cref="BeliefSystem"/> in its step interval.</summary>
    private static IntervalSystem CreateBeliefStep(DivineRules rules, SettlementRules settlementRules)
    {
        var belief = new BeliefSystem(rules, settlementRules);
        return new IntervalSystem(belief, rules.StepIntervalTicks);
    }
}
