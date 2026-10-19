using System;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.GreatEvents;

namespace Pantheon.Game.GreatEvents;

/// <summary>Words for the great events and the plague (ADR-034). Pure and culture-invariant.</summary>
public static class GreatEventText
{
    // Seconds over which the sun fades out and back in.
    private const float FadeSeconds = 5f;

    /// <summary>A great event under way, in a line.</summary>
    /// <param name="great">The event.</param>
    /// <param name="world">World state.</param>
    /// <returns>For example "The Grey Plague: 4 villages sick".</returns>
    public static string Describe(GreatEventState great, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(great);
        ArgumentNullException.ThrowIfNull(world);
        var name = Capitalised(great.Name);
        return great.Kind switch
        {
            GreatEventKind.Plague => Format("{0}: {1} sick", name, Count(world.Settlements.Count(s => s.Plague > 0f), "village")),
            GreatEventKind.Eclipse => Format("{0}: the sun is dark ({1} left)", name, Clock(Math.Max(0L, (great.EndTick ?? world.Tick) - world.Tick))),
            GreatEventKind.Reformation => Format("{0} of {1}{2}", name, God(world, great.GodId), great.Peak > 0 ? Format(": {0} left the faith", great.Peak) : string.Empty),
            GreatEventKind.WorldWar => Format("{0}: {1} at war", name, Count(GreatEventWatch.GodsAtWar(world), "god")),
            GreatEventKind.GreatDrought => Format("{0}: the land's soil at {1:0%}", name, GreatEventWatch.LandSoil(world)),
            GreatEventKind.GreatMigration => Format("{0}: {1} on the road", name, world.Migration.Groups.Sum(g => g.Population)),
        };
    }

    /// <summary>Every great event under way, in a line for the top bar; empty when none.</summary>
    /// <param name="world">World state.</param>
    /// <returns>The line.</returns>
    public static string Bar(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return string.Join(" ", world.GreatEvents.Events.Select(e => Describe(e, world) + "."));
    }

    /// <summary>A village's plague, if it has been touched by it.</summary>
    /// <param name="settlement">The village.</param>
    /// <returns>For example "12% sick, 30% immune", or <c>null</c>.</returns>
    public static string? Village(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.Plague <= 0f && settlement.Immunity < 0.01f ? null : Format("{0:0%} sick, {1:0%} immune", settlement.Plague, settlement.Immunity);
    }

    /// <summary>How dark the sun is now (0..1): it fades out as an eclipse begins and back as it ends.</summary>
    /// <param name="world">World state.</param>
    /// <returns>The darkness.</returns>
    public static float Darkness(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (GreatEventModel.Active(world, GreatEventKind.Eclipse) is not { EndTick: { } end } eclipse)
        {
            return 0f;
        }

        var fade = FadeSeconds * SimulationTime.TicksPerSecond;
        return Math.Clamp(Math.Min(world.Tick - eclipse.StartTick, end - world.Tick) / fade, 0f, 1f);
    }

    /// <summary>A name with its first letter in capitals.</summary>
    /// <param name="name">The name.</param>
    /// <returns>For example "The Grey Plague".</returns>
    public static string Capitalised(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>Ticks as minutes and seconds.</summary>
    internal static string Clock(long ticks)
    {
        var seconds = ticks / SimulationTime.TicksPerSecond;
        return Format("{0}:{1:00}", seconds / 60, seconds % 60);
    }

    /// <summary>A god's name.</summary>
    internal static string God(WorldState world, int? godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a forgotten god";

    /// <summary>A count with its noun, singular for one.</summary>
    internal static string Count(int count, string noun) => Format("{0} {1}{2}", count, noun, count == 1 ? string.Empty : "s");

    /// <summary>Invariant-culture formatting.</summary>
    internal static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
