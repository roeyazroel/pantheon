using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Audio;

/// <summary>A sound to play: where in the world, or everywhere (the player's own news), and how loud.</summary>
/// <param name="Kind">The sound.</param>
/// <param name="Position">Where it happens, so it is heard from there; <c>null</c> for the player's news.</param>
/// <param name="Volume">Its loudness (0..1).</param>
public readonly record struct SoundCue(SoundKind Kind, GroundPosition? Position, float Volume);

/// <summary>
/// What the game sounds like now (spec §60, ADR-021): the sounds the world's events make, where they
/// make them (geographically, so distant thunder and Creatures' roars are heard from afar), the
/// music's mood, and the wind and rain where the camera looks. Pure; the client plays them.
/// </summary>
public static class AudioDirector
{
    // A breeze is always heard; wind and rain add to it.
    private const float Breeze = 0.15f;

    /// <summary>The sounds of a frame's events.</summary>
    /// <param name="events">The events, in order.</param>
    /// <param name="world">The world.</param>
    /// <param name="miracles">The miracles, to know what one does.</param>
    /// <param name="localGodId">The player's god.</param>
    /// <returns>The cues, in order.</returns>
    public static IReadOnlyList<SoundCue> Cues(IReadOnlyList<WorldEvent> events, WorldState world, MiracleBook miracles, int? localGodId)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(miracles);
        var cues = new List<SoundCue>();
        foreach (var worldEvent in events)
        {
            if (Cue(worldEvent, world, miracles, localGodId) is { } cue)
            {
                cues.Add(cue);
            }
        }

        return cues;
    }

    /// <summary>The music's mood: Triumph while the player's god dominates or has won, Tension in war, near a rival's victory or while the ground shakes, Calm otherwise.</summary>
    /// <param name="world">The world.</param>
    /// <param name="localGodId">The player's god.</param>
    /// <returns>The mood.</returns>
    public static MusicMood Mood(WorldState world, int? localGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var me = localGodId is { } id ? world.Gods.Find(g => g.Id == id) : null;
        if (me is not null && (me.DominantSinceTick is not null || world.WinnerGodIds.Contains(me.Id)))
        {
            return MusicMood.Triumph;
        }

        var war = me is not null && world.Gods.Exists(g => DiplomacyModel.AreAtWar(world, me.Id, g.Id));
        var rivalAhead = world.Gods.Exists(g => g.Id != localGodId && g.DominantSinceTick is not null);
        return war || rivalAhead || world.Disasters.Earthquakes.Count > 0 ? MusicMood.Tension : MusicMood.Calm;
    }

    /// <summary>How loud the rain and the wind are where the camera looks (0..1 each).</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="world">The world.</param>
    /// <param name="focus">Where the camera looks.</param>
    /// <returns>The rain's and the wind's loudness.</returns>
    public static (float Rain, float Wind) Ambience(WeatherRules rules, WorldState world, GroundPosition focus)
    {
        ArgumentNullException.ThrowIfNull(world);
        var weather = WeatherModel.At(rules, world.Weather, focus);
        return (Math.Clamp(weather.Rain, 0f, 1f), Math.Clamp(Breeze + weather.Wind, 0f, 1f));
    }

    /// <summary>The sound of one event, if it makes one.</summary>
    private static SoundCue? Cue(WorldEvent worldEvent, WorldState world, MiracleBook miracles, int? me) => worldEvent switch
    {
        MiracleCast cast when miracles.Find(cast.MiracleId) is { } miracle => MiracleSound(miracle) is { } kind ? new SoundCue(kind, cast.Target, 1f) : null,
        EarthquakeStruck quake => new SoundCue(SoundKind.Rumble, quake.Epicentre, 1f),
        FloodStarted { GodId: null } flood when flood.RegionId >= 0 && flood.RegionId < world.Regions.Cells.Count => new SoundCue(SoundKind.Rumble, world.Regions.GetRegionCenter(flood.RegionId), 0.6f),
        StormStruck storm when world.Settlements.Find(s => s.Id == storm.SettlementId) is { } place => new SoundCue(SoundKind.Thunder, place.Position, 0.9f),
        WildfireStarted { Cause: not FireCause.Hearth } fire when fire.RegionId >= 0 && fire.RegionId < world.Regions.Cells.Count => new SoundCue(SoundKind.Crackle, world.Regions.GetRegionCenter(fire.RegionId), 0.6f),
        CreaturesMet { Outcome: MeetingOutcome.Fought } met when world.Creatures.Find(c => c.Id == met.CreatureId) is { } creature => new SoundCue(SoundKind.Roar, creature.Position, 1f),
        CreatureFightEnded ended when world.Creatures.Find(c => c.Id == ended.CreatureId) is { } winner => new SoundCue(SoundKind.Roar, winner.Position, 0.8f),
        CreatureChanged { Change: CreatureChange.LevelledUp } grown when world.Creatures.Find(c => c.Id == grown.CreatureId)?.GodId == me => new SoundCue(SoundKind.Fanfare, null, 0.6f),
        RankAchieved rank when rank.GodId == me => new SoundCue(SoundKind.Fanfare, null, 0.8f),
        DomainChosen chosen when chosen.GodId == me => new SoundCue(SoundKind.Shimmer, null, 0.8f),
        HolySiteClaimed claim when claim.GodId == me => new SoundCue(SoundKind.Fanfare, null, 0.7f),
        HolySiteClaimed claim when claim.PreviousGodId == me => new SoundCue(SoundKind.Warning, null, 0.8f),
        HolySiteContested contest when contest.KeeperGodId == me => new SoundCue(SoundKind.Warning, null, 0.6f),
        SchismStarted schism when schism.GodId == me => new SoundCue(SoundKind.Warning, null, 0.8f),
        DiplomacyChanged move when move.OtherGodId == me && (move.Betrayal || move.Kind == DiplomaticCommandKind.DeclareWar) => new SoundCue(SoundKind.Warning, null, 0.9f),
        DiplomacyChanged move when (move.OtherGodId == me || move.ActorGodId == me) && move.Status == DiplomaticStatus.Alliance => new SoundCue(SoundKind.Alert, null, 0.7f),
        VictoryAchieved victory => new SoundCue(System.Linq.Enumerable.Contains(victory.GodIds, me ?? 0) ? SoundKind.Fanfare : SoundKind.Warning, null, 1f),
        _ => null,
    };

    /// <summary>What a miracle sounds like: by what it does first; Sow Discord is unseen and unheard.</summary>
    private static SoundKind? MiracleSound(MiracleDefinition miracle)
    {
        foreach (var effect in miracle.Effects)
        {
            switch (effect.Kind)
            {
                case MiracleEffectKind.Smite:
                case MiracleEffectKind.Wound:
                case MiracleEffectKind.Judge:
                    return SoundKind.Bolt;
                case MiracleEffectKind.Quake:
                case MiracleEffectKind.Flood:
                case MiracleEffectKind.Awaken:
                    return SoundKind.Rumble;
                case MiracleEffectKind.Ignite:
                    return SoundKind.Crackle;
                case MiracleEffectKind.Rain:
                case MiracleEffectKind.Redirect:
                    return SoundKind.Thunder;
                case MiracleEffectKind.Terrify:
                    return SoundKind.Warning;
                case MiracleEffectKind.Discord:
                    return null;
                case MiracleEffectKind.AddFood:
                case MiracleEffectKind.Heal:
                case MiracleEffectKind.Bless:
                case MiracleEffectKind.Shield:
                case MiracleEffectKind.Regrow:
                case MiracleEffectKind.Populate:
                case MiracleEffectKind.Sanctuary:
                case MiracleEffectKind.Teach:
                case MiracleEffectKind.Water:
                case MiracleEffectKind.Soak:
                case MiracleEffectKind.Hallow:
                case MiracleEffectKind.Still:
                case MiracleEffectKind.Anoint:
                    continue;
            }
        }

        return SoundKind.Chime;
    }
}
