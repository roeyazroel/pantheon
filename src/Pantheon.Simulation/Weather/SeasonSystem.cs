using System;
using Pantheon.Domain.Events;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Weather;

/// <summary>
/// One season step (spec §17, ADR-028): it tells when a season begins, spoils stored food in the
/// warm months and in a heat wave, and lets the cold of winter and a cold wave (or a heat wave's
/// heat) sicken the people, the homeless most. Temperature, rain, harvests, fire and migration
/// take the season from <see cref="ClimateModel"/> in their own steps.
/// </summary>
public sealed class SeasonSystem : ISimulationSystem
{
    private readonly WeatherRules _rules;
    private readonly int _intervalTicks;

    /// <summary>Creates the system.</summary>
    /// <param name="rules">Weather rules, with the seasons and the waves.</param>
    /// <param name="intervalTicks">Ticks between its steps.</param>
    public SeasonSystem(WeatherRules rules, int intervalTicks)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _rules = rules;
        _intervalTicks = intervalTicks;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        if (ClimateModel.SeasonOf(_rules, world) is { } season && season != ClimateModel.SeasonOf(_rules, world, world.Tick - _intervalTicks))
        {
            context.Events.Add(new SeasonChanged { Tick = world.Tick, Season = season, Year = ClimateModel.YearOf(_rules, world) });
        }

        var climate = ClimateModel.Now(_rules, world);
        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population <= 0)
            {
                continue;
            }

            settlement.Food *= Math.Max(0f, 1f - (climate.SpoilagePerSecond * seconds));
            var exposure = 0.5f + (0.5f * SettlementEconomy.ComputeHomelessShare(settlement));
            settlement.Health = Math.Max(0f, settlement.Health - (climate.HealthLossPerSecond * exposure * seconds));
        }
    }
}
