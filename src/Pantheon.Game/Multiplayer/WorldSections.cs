using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Networking.Protocol;
using Pantheon.Persistence;

namespace Pantheon.Game.Multiplayer;

/// <summary>
/// Writes a part of a world for a player and applies it to a replica (ADR-019), in the save format
/// without indentation.
/// Applying replaces the part in place, so presenters keep reading the same world.
/// </summary>
public static class WorldSections
{
    private static JsonSerializerOptions Options => PantheonJson.WireOptions;

    /// <summary>Writes a section of the world.</summary>
    /// <param name="world">The host's world.</param>
    /// <param name="section">The section.</param>
    /// <param name="creatures">For <see cref="WorldSection.CreatureMotion"/>, which Creatures to include; all when <c>null</c>.</param>
    /// <returns>The section as JSON.</returns>
    /// <exception cref="ArgumentException"><see cref="WorldSection.Regions"/>, which only <see cref="WriteRegions"/> writes.</exception>
    public static string Write(WorldState world, WorldSection section, Func<CreatureState, bool>? creatures = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        return section switch
        {
            WorldSection.Gods => Json(new GodsPart(world.Tick, world.Random, world.Gods, world.WinnerGodIds)),
            WorldSection.CreatureMotion => Json(world.Creatures.Where(c => creatures?.Invoke(c) ?? true).Select(CreatureMotion.Of).ToList()),
            WorldSection.Creatures => Json(world.Creatures),
            WorldSection.Settlements => Json(world.Settlements),
            WorldSection.Weather => Json(world.Weather),
            WorldSection.Disasters => Json(world.Disasters),
            WorldSection.Migration => Json(world.Migration),
            WorldSection.Diplomacy => Json(world.Diplomacy),
            WorldSection.History => Json(world.History),
            WorldSection.Regions => throw new ArgumentException("Regions are written as changes, by WriteRegions.", nameof(section)),
        };
    }

    /// <summary>Writes the regions whose changing values differ from what a player last received, and remembers them as sent.</summary>
    /// <param name="world">The host's world.</param>
    /// <param name="sent">What the player has, by region id; updated.</param>
    /// <returns>The changes as JSON, or <c>null</c> when nothing changed.</returns>
    public static string? WriteRegions(WorldState world, Dictionary<int, RegionChange> sent)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sent);
        var changes = new List<RegionChange>();
        foreach (var region in world.Regions.Cells)
        {
            var now = RegionChange.Of(region);
            if (!sent.TryGetValue(region.Id, out var before) || before != now)
            {
                changes.Add(now);
                sent[region.Id] = now;
            }
        }

        return changes.Count == 0 ? null : Json(changes);
    }

    /// <summary>Applies a section to a replica's world in place.</summary>
    /// <param name="world">The replica's world.</param>
    /// <param name="section">The section.</param>
    /// <param name="data">The section as JSON.</param>
    /// <returns>The host's tick, for the gods' section; otherwise <c>null</c>.</returns>
    /// <exception cref="JsonException">The data is not that section.</exception>
    public static long? Apply(WorldState world, WorldSection section, string data)
    {
        ArgumentNullException.ThrowIfNull(world);
        switch (section)
        {
            case WorldSection.Gods:
                var gods = Read<GodsPart>(data);
                Replace(world.Gods, gods.Gods);
                Replace(world.WinnerGodIds, gods.WinnerGodIds);
                world.Random = gods.Random;
                return gods.Tick;
            case WorldSection.CreatureMotion:
                foreach (var motion in Read<List<CreatureMotion>>(data))
                {
                    if (world.Creatures.Find(c => c.Id == motion.Id) is { } creature)
                    {
                        motion.ApplyTo(creature);
                    }
                }

                return null;
            case WorldSection.Creatures:
                Replace(world.Creatures, Read<List<CreatureState>>(data));
                return null;
            case WorldSection.Settlements:
                Replace(world.Settlements, Read<List<SettlementState>>(data));
                return null;
            case WorldSection.Regions:
                foreach (var change in Read<List<RegionChange>>(data))
                {
                    if (change.Id >= 0 && change.Id < world.Regions.Cells.Count)
                    {
                        change.ApplyTo(world.Regions.Cells[change.Id]);
                    }
                }

                return null;
            case WorldSection.Weather:
                var weather = Read<WeatherState>(data);
                Replace(world.Weather.Cells, weather.Cells);
                (world.Weather.NextCellId, world.Weather.Spell, world.Weather.SpellEndsTick, world.Weather.WindDirection) = (weather.NextCellId, weather.Spell, weather.SpellEndsTick, weather.WindDirection);
                return null;
            case WorldSection.Disasters:
                var disasters = Read<DisasterState>(data);
                Replace(world.Disasters.Earthquakes, disasters.Earthquakes);
                world.Disasters.NextEarthquakeId = disasters.NextEarthquakeId;
                return null;
            case WorldSection.Migration:
                var migration = Read<MigrationState>(data);
                Replace(world.Migration.Groups, migration.Groups);
                world.Migration.NextGroupId = migration.NextGroupId;
                return null;
            case WorldSection.Diplomacy:
                var diplomacy = Read<DiplomacyState>(data);
                Replace(world.Diplomacy.Relations, diplomacy.Relations);
                Replace(world.Diplomacy.Stances, diplomacy.Stances);
                Replace(world.Diplomacy.Proposals, diplomacy.Proposals);
                return null;
            case WorldSection.History:
                Replace(world.History, Read<List<Domain.History.HistoryRecord>>(data));
                return null;
        }

        throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown world section.");
    }

    /// <summary>A value as JSON in the save format.</summary>
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>A value from JSON in the save format.</summary>
    private static T Read<T>(string data) => JsonSerializer.Deserialize<T>(data, Options) ?? throw new JsonException("The section is empty.");

    /// <summary>Replaces a list's items, keeping the list.</summary>
    private static void Replace<T>(List<T> target, IEnumerable<T> items)
    {
        target.Clear();
        target.AddRange(items);
    }

    /// <summary>The gods with the world's clock, random state and winners.</summary>
    /// <param name="Tick">The world's tick.</param>
    /// <param name="Random">The world's random state.</param>
    /// <param name="Gods">The gods.</param>
    /// <param name="WinnerGodIds">The winners, if any.</param>
    private sealed record GodsPart(long Tick, RandomState Random, List<GodState> Gods, List<int> WinnerGodIds);
}

/// <summary>Where a Creature is and what it does now: what a player needs to see it move (ADR-019).</summary>
/// <param name="Id">The Creature.</param>
/// <param name="Position">Where it stands.</param>
/// <param name="Heading">Which way it faces.</param>
/// <param name="Health">Its health.</param>
/// <param name="Action">What it does.</param>
/// <param name="Situation">The situation it chose it in.</param>
/// <param name="Phase">How far it has got.</param>
/// <param name="TargetSettlementId">The settlement it concerns.</param>
/// <param name="TargetCreatureId">The Creature it concerns.</param>
/// <param name="Target">Where it is going.</param>
/// <param name="Path">The waypoints left.</param>
/// <param name="TicksLeft">Ticks left in its phase.</param>
public sealed record CreatureMotion(
    int Id,
    GroundPosition Position,
    float Heading,
    float Health,
    CreatureAction Action,
    CreatureSituation Situation,
    CreaturePhase Phase,
    int? TargetSettlementId,
    int? TargetCreatureId,
    GroundPosition Target,
    IReadOnlyList<GroundPosition> Path,
    int TicksLeft)
{
    /// <summary>A Creature's motion now.</summary>
    /// <param name="creature">The Creature.</param>
    /// <returns>Its motion.</returns>
    public static CreatureMotion Of(CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var a = creature.Activity;
        return new CreatureMotion(creature.Id, creature.Position, creature.Heading, creature.Health, a.Action, a.Situation, a.Phase, a.TargetSettlementId, a.TargetCreatureId, a.Target, [.. a.Path], a.TicksLeft);
    }

    /// <summary>Moves a replica's Creature to this motion.</summary>
    /// <param name="creature">The replica's Creature.</param>
    public void ApplyTo(CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        (creature.Position, creature.Heading, creature.Health) = (Position, Heading, Health);
        var a = creature.Activity;
        (a.Action, a.Situation, a.Phase, a.TargetSettlementId, a.TargetCreatureId, a.Target, a.TicksLeft) = (Action, Situation, Phase, TargetSettlementId, TargetCreatureId, Target, TicksLeft);
        a.Path.Clear();
        a.Path.AddRange(Path);
    }
}

/// <summary>A region's values that change during play (ADR-019).</summary>
/// <param name="Id">The region.</param>
/// <param name="Fertility">Its fertility.</param>
/// <param name="Vegetation">Its vegetation.</param>
/// <param name="Blessing">Its blessing.</param>
/// <param name="SoilMoisture">The water in its soil.</param>
/// <param name="Fire">How fiercely it burns.</param>
/// <param name="Scorch">How scorched it is.</param>
public readonly record struct RegionChange(int Id, float Fertility, float Vegetation, float Blessing, float SoilMoisture, float Fire, float Scorch)
{
    /// <summary>A region's changing values now.</summary>
    /// <param name="region">The region.</param>
    /// <returns>Its values.</returns>
    public static RegionChange Of(RegionState region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return new(region.Id, region.Fertility, region.Vegetation, region.Blessing, region.SoilMoisture, region.Fire, region.Scorch);
    }

    /// <summary>Sets a replica's region to these values.</summary>
    /// <param name="region">The replica's region.</param>
    public void ApplyTo(RegionState region)
    {
        ArgumentNullException.ThrowIfNull(region);
        (region.Fertility, region.Vegetation, region.Blessing, region.SoilMoisture, region.Fire, region.Scorch) = (Fertility, Vegetation, Blessing, SoilMoisture, Fire, Scorch);
    }
}
