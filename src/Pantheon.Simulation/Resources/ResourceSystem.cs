using System;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Resources;

/// <summary>
/// The land's timber (spec §5, §18, ADR-033): fire burns it, and it grows back towards the land's
/// forest, as far as the land's life is unharmed, and not under deep snow.
/// </summary>
public sealed class ResourceSystem : ISimulationSystem
{
    // A gap this small is closed at once, so a whole forest stops changing.
    private const float Settled = 0.001f;

    private readonly ResourceRules _rules;
    private readonly int _intervalTicks;

    /// <summary>Creates the system.</summary>
    /// <param name="rules">Resource rules.</param>
    /// <param name="intervalTicks">Ticks between its steps.</param>
    public ResourceSystem(ResourceRules rules, int intervalTicks)
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
        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        foreach (var region in context.World.Regions.Cells)
        {
            if (region.Forest <= 0f && region.Timber <= 0f)
            {
                continue;
            }

            if (region.Fire > 0f)
            {
                region.Timber = Math.Max(0f, region.Timber - (region.Fire * _rules.TimberBurnPerSecond * seconds));
                continue;
            }

            var grown = region.Forest * (1f - SimMath.Clamp01(region.Damage));
            if (region.Timber < grown && region.Snow < 0.5f)
            {
                var timber = region.Timber + ((grown - region.Timber) * Math.Min(1f, _rules.TimberRegrowthPerSecond * seconds));
                region.Timber = grown - timber < Settled ? grown : timber;
            }

            region.Timber = Math.Min(region.Timber, region.Forest);
        }
    }
}
