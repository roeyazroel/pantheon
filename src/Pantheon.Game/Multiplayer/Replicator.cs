using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Networking.Protocol;

namespace Pantheon.Game.Multiplayer;

/// <summary>
/// Decides what each player receives and when (spec §95–96, ADR-019). Each section of the world
/// has its own interval; a section goes out only when it changed since the player last had it.
/// Creatures near the player's view move often, others rarely; regions travel as the values that
/// changed. A whole snapshot corrects everything now and then.
/// </summary>
public sealed class Replicator
{
    /// <summary>Seconds between whole snapshots after joining.</summary>
    public const double SnapshotSeconds = 20.0;

    /// <summary>Metres from a player's view within which Creatures move at the near rate.</summary>
    public const float InterestRadius = 600f;

    /// <summary>Seconds between the motions of Creatures far from a player's view.</summary>
    public const double FarMotionSeconds = 1.0;

    /// <summary>Seconds after which the gods' fading views of each other are refreshed even without a new event.</summary>
    public const double DiplomacyRefreshSeconds = 5.0;

    /// <summary>Seconds between the sends of each section (§96: Creatures high, gods medium, settlements low, diplomacy on change).</summary>
    public static IReadOnlyDictionary<WorldSection, double> Intervals { get; } = new Dictionary<WorldSection, double>
    {
        [WorldSection.CreatureMotion] = 0.2,
        [WorldSection.Gods] = 0.25,
        [WorldSection.Migration] = 0.5,
        [WorldSection.Diplomacy] = 0.5,
        [WorldSection.Settlements] = 2.0,
        [WorldSection.Weather] = 1.0,
        [WorldSection.Disasters] = 1.0,
        [WorldSection.Creatures] = 2.0,
        [WorldSection.Regions] = 2.0,
        [WorldSection.History] = 2.0,
        [WorldSection.HolySites] = 2.0,
        [WorldSection.Water] = 1.0,
        [WorldSection.Notables] = 1.0,
    };

    // Sections an intent can change at once, sent in the same update as the verdict.
    private static readonly WorldSection[] Urgent = [WorldSection.Gods, WorldSection.Settlements, WorldSection.Diplomacy, WorldSection.Weather, WorldSection.Disasters, WorldSection.Migration, WorldSection.Water];

    private readonly Dictionary<int, Peer> _peers = [];

    /// <summary>Starts following a player that has just received the whole world.</summary>
    /// <param name="peerId">The player.</param>
    /// <param name="world">The world it received.</param>
    public void Add(int peerId, WorldState world)
    {
        var peer = new Peer();
        peer.Synced(world);
        _peers[peerId] = peer;
    }

    /// <summary>Stops following a player.</summary>
    /// <param name="peerId">The player.</param>
    public void Remove(int peerId) => _peers.Remove(peerId);

    /// <summary>Records where a player looks.</summary>
    /// <param name="peerId">The player.</param>
    /// <param name="focus">The point its camera looks at.</param>
    public void Focus(int peerId, GroundPosition focus)
    {
        if (_peers.TryGetValue(peerId, out var peer))
        {
            peer.Focus = focus;
        }
    }

    /// <summary>Every player has just received the whole world (a new world, or a correction).</summary>
    /// <param name="world">The world they received.</param>
    public void SyncedAll(WorldState world)
    {
        foreach (var peer in _peers.Values)
        {
            peer.Synced(world);
        }
    }

    /// <summary>A player's intent may have changed these sections: they go out in the next update, whatever their schedule.</summary>
    public void Hurry()
    {
        foreach (var peer in _peers.Values)
        {
            foreach (var section in Urgent)
            {
                peer.Since[section] = double.PositiveInfinity;
            }
        }
    }

    /// <summary>What each player is due now.</summary>
    /// <param name="world">The host's world.</param>
    /// <param name="seconds">Real seconds since the last call.</param>
    /// <returns>The players due a whole snapshot, and the section updates for the others, by player.</returns>
    public (IReadOnlyList<int> Snapshots, IReadOnlyList<(int PeerId, SectionUpdate Update)> Updates) Due(WorldState world, double seconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        var snapshots = new List<int>();
        var updates = new List<(int, SectionUpdate)>();
        var written = new Dictionary<WorldSection, string>();
        string Shared(WorldSection section) => written.TryGetValue(section, out var json) ? json : written[section] = WorldSections.Write(world, section);
        foreach (var (peerId, peer) in _peers.OrderBy(p => p.Key))
        {
            peer.SinceSnapshot += seconds;
            if (peer.SinceSnapshot >= SnapshotSeconds)
            {
                snapshots.Add(peerId);
                peer.Synced(world);
                continue;
            }

            foreach (var (section, interval) in Intervals)
            {
                peer.Since[section] = peer.Since.GetValueOrDefault(section) + seconds;
                if (peer.Since[section] < interval)
                {
                    continue;
                }

                // Keep the overshoot, so a section keeps its rate whatever the frame time; a backlog (a hurry, a long frame) is dropped.
                peer.Since[section] = peer.Since[section] - interval is var left && left < interval ? left : 0.0;
                var data = section switch
                {
                    WorldSection.CreatureMotion => Motion(world, peer, interval),
                    WorldSection.Regions => WorldSections.WriteRegions(world, peer.Regions),
                    WorldSection.Diplomacy => Diplomacy(world, peer, interval, Shared),
                    WorldSection.Gods or WorldSection.Creatures or WorldSection.Settlements or WorldSection.Weather
                        or WorldSection.Disasters or WorldSection.Migration or WorldSection.History or WorldSection.HolySites or WorldSection.Water or WorldSection.Notables => Changed(peer, section, Shared(section)),
                };
                if (data is not null)
                {
                    updates.Add((peerId, new SectionUpdate(world.Tick, section, data)));
                }
            }
        }

        return (snapshots, updates);
    }

    /// <summary>The section, if the player does not have it already.</summary>
    private static string? Changed(Peer peer, WorldSection section, string data)
    {
        if (peer.Last.TryGetValue(section, out var last) && string.Equals(last, data, StringComparison.Ordinal))
        {
            return null;
        }

        peer.Last[section] = data;
        return data;
    }

    /// <summary>
    /// Diplomacy by event (§96): when a status, a proposal or a remembered act changed since the
    /// player last had it, or every <see cref="DiplomacyRefreshSeconds"/> for the views' fading.
    /// </summary>
    private static string? Diplomacy(WorldState world, Peer peer, double interval, Func<WorldSection, string> shared)
    {
        peer.SinceDiplomacy += interval;
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var events = string.Join(
            ';',
            world.Diplomacy.Stances.Select(s => string.Create(invariant, $"{s.GodA}-{s.GodB}:{s.Status}:{s.SinceTick}"))
                .Concat(world.Diplomacy.Proposals.Select(p => string.Create(invariant, $"{p.FromGodId}>{p.ToGodId}:{p.Kind}")))
                .Concat(world.Diplomacy.Relations.Select(r => string.Create(invariant, $"{r.ObserverGodId}>{r.SubjectGodId}:{r.Memories.Count}:{(r.Memories.Count > 0 ? r.Memories[^1].Tick : -1)}"))));
        if (string.Equals(events, peer.DiplomacyEvents, StringComparison.Ordinal) && peer.SinceDiplomacy < DiplomacyRefreshSeconds)
        {
            return null;
        }

        (peer.DiplomacyEvents, peer.SinceDiplomacy) = (events, 0.0);
        return Changed(peer, WorldSection.Diplomacy, shared(WorldSection.Diplomacy));
    }

    /// <summary>The Creatures near the player's view, or every Creature when the far ones are due too.</summary>
    private static string? Motion(WorldState world, Peer peer, double interval)
    {
        peer.SinceFarMotion += interval;
        var all = peer.Focus is null || peer.SinceFarMotion >= FarMotionSeconds;
        if (all)
        {
            peer.SinceFarMotion = 0.0;
        }

        var focus = peer.Focus ?? default;
        bool Near(Domain.Creatures.CreatureState c) => all || (MathF.Pow(c.Position.X - focus.X, 2) + MathF.Pow(c.Position.Z - focus.Z, 2) <= InterestRadius * InterestRadius);
        return world.Creatures.Any(Near) ? WorldSections.Write(world, WorldSection.CreatureMotion, Near) : null;
    }

    /// <summary>What the host knows about one player.</summary>
    private sealed class Peer
    {
        public Dictionary<WorldSection, double> Since { get; } = [];

        public Dictionary<WorldSection, string> Last { get; } = [];

        public Dictionary<int, RegionChange> Regions { get; } = [];

        public GroundPosition? Focus { get; set; }

        public double SinceSnapshot { get; set; }

        public double SinceFarMotion { get; set; }

        public double SinceDiplomacy { get; set; }

        public string? DiplomacyEvents { get; set; }

        /// <summary>The player has the whole world as it is now.</summary>
        public void Synced(WorldState world)
        {
            SinceSnapshot = 0.0;
            DiplomacyEvents = null;
            SinceDiplomacy = 0.0;
            Last.Clear();
            Regions.Clear();
            foreach (var region in world.Regions.Cells)
            {
                Regions[region.Id] = RegionChange.Of(region);
            }
        }
    }
}
