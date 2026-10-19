using System.Collections.Generic;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Weather;

namespace Pantheon.Simulation.Miracles;

/// <summary>
/// The Divine Acts of spec §14 (ADR-030), the dearest miracles, each remembered by the world:
/// hallowing the land so its blessing never fades, waking the volcano, and stilling every disaster on
/// the island at once. Acts that would reshape the terrain (a new island, a split mountain, a river's
/// new course) are left out, as §64 postpones changing the terrain.
/// </summary>
public static class DivineActs
{
    /// <summary>Hallows a region: its blessing never falls below the amount again.</summary>
    /// <param name="region">The region.</param>
    /// <param name="amount">The lasting blessing (0..1).</param>
    public static void Hallow(RegionState region, float amount)
    {
        System.ArgumentNullException.ThrowIfNull(region);
        var hallowed = SimMath.Clamp01(amount);
        region.Hallowed = System.Math.Max(region.Hallowed, hallowed);
        region.Blessing = System.Math.Max(region.Blessing, hallowed);
    }

    /// <summary>Whether the island has a sleeping volcano to wake.</summary>
    /// <param name="disasters">Disaster rules; <c>null</c> for none.</param>
    /// <param name="world">World state.</param>
    /// <returns><c>true</c> when there is one.</returns>
    public static bool CanAwaken(DisasterRules? disasters, WorldState world) =>
        disasters is not null && HazardModel.Find(disasters.Hazards.Volcano, world) is { Phase: VolcanoPhase.Dormant };

    /// <summary>Wakes the island's volcano now: it rumbles, then erupts.</summary>
    /// <param name="disasters">Disaster rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god that wakes it.</param>
    /// <param name="events">Receives <see cref="VolcanoChanged"/> and <see cref="DivineActDone"/>.</param>
    public static void Awaken(DisasterRules disasters, WorldState world, int godId, ICollection<WorldEvent> events)
    {
        System.ArgumentNullException.ThrowIfNull(disasters);
        System.ArgumentNullException.ThrowIfNull(world);
        if (HazardModel.Wake(disasters.Hazards.Volcano, world) is not { } volcano)
        {
            return;
        }

        events.Add(new VolcanoChanged { Tick = world.Tick, RegionId = volcano.RegionId, Phase = VolcanoPhase.Rumbling });
        events.Add(new DivineActDone { Tick = world.Tick, GodId = godId, Act = DivineActKind.Awakening, Position = world.Regions.GetRegionCenter(volcano.RegionId) });
    }

    /// <summary>Whether any disaster on the island could be stilled.</summary>
    /// <param name="weather">Weather rules, for storms; <c>null</c> for none.</param>
    /// <param name="world">World state.</param>
    /// <returns><c>true</c> when something rages.</returns>
    public static bool Unquiet(WeatherRules? weather, WorldState world)
    {
        System.ArgumentNullException.ThrowIfNull(world);
        var disasters = world.Disasters;
        return disasters.Earthquakes.Count > 0 || disasters.Tornadoes.Count > 0 || disasters.Swarms.Count > 0
            || disasters.Volcano is { Phase: not VolcanoPhase.Dormant }
            || world.Regions.Cells.Exists(r => r.Fire > 0f || r.Flood > 0f || r.Blight > 0f)
            || (weather is not null && world.Weather.Cells.Exists(c => IsStorm(weather, c)))
            || world.Weather.Spell is WeatherSpell.Dry or WeatherSpell.HeatWave or WeatherSpell.ColdWave;
    }

    /// <summary>
    /// Stills every disaster on the island: quakes stop, fires go out, floods drain, blight dies,
    /// storms, tornadoes and locusts vanish, the volcano sleeps, and a drought or a wave of heat or cold
    /// breaks.
    /// </summary>
    /// <param name="weather">Weather rules, for storms; <c>null</c> for none.</param>
    /// <param name="disasters">Disaster rules, for the volcano's sleep; <c>null</c> for none.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god that stills them.</param>
    /// <param name="target">Where it was done.</param>
    /// <param name="events">Receives <see cref="DivineActDone"/> and, if it slept, <see cref="VolcanoChanged"/>.</param>
    public static void Still(WeatherRules? weather, DisasterRules? disasters, WorldState world, int godId, GroundPosition target, ICollection<WorldEvent> events)
    {
        System.ArgumentNullException.ThrowIfNull(world);
        var state = world.Disasters;
        state.Earthquakes.Clear();
        state.Tornadoes.Clear();
        state.Swarms.Clear();
        foreach (var region in world.Regions.Cells)
        {
            (region.Fire, region.Flood, region.Blight) = (0f, 0f, 0f);
        }

        if (weather is not null)
        {
            world.Weather.Cells.RemoveAll(cell => IsStorm(weather, cell));
        }

        if (world.Weather.Spell is WeatherSpell.Dry or WeatherSpell.HeatWave or WeatherSpell.ColdWave)
        {
            (world.Weather.Spell, world.Weather.SpellEndsTick) = (WeatherSpell.Normal, null);
        }

        if (state.Volcano is { Phase: not VolcanoPhase.Dormant } volcano && disasters is not null)
        {
            (volcano.Phase, volcano.PhaseEndsTick) = (VolcanoPhase.Dormant, world.Tick + HazardModel.Ticks(disasters.Hazards.Volcano.CooldownSeconds));
            events.Add(new VolcanoChanged { Tick = world.Tick, RegionId = volcano.RegionId, Phase = VolcanoPhase.Dormant });
        }

        events.Add(new DivineActDone { Tick = world.Tick, GodId = godId, Act = DivineActKind.Stillness, Position = target });
    }

    /// <summary>Whether a cell is a storm: a thunderstorm, a windstorm or a hurricane.</summary>
    private static bool IsStorm(WeatherRules weather, WeatherCell cell) =>
        WeatherModel.Classify(weather, cell) is WeatherKind.Thunderstorm or WeatherKind.Windstorm or WeatherKind.Hurricane;
}
