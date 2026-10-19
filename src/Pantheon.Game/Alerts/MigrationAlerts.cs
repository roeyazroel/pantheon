using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Game.Migration;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about migration that concern the local player (spec §31, §59, ADR-016): their followers
/// leaving, giving them up or being turned away, migrants arriving where they are followed, and
/// rivals' refugee policies. Part of <see cref="AlertFeed"/>.
/// </summary>
internal static class MigrationAlerts
{
    /// <summary>The alert for a migration event, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId) => worldEvent switch
    {
        MigrationStarted started => DescribeStart(started, world, localGodId),
        MigrationEnded ended => DescribeEnd(ended, world, localGodId),
        MigrantsTurnedAway turned => DescribeTurnedAway(turned, world, localGodId),
        RefugeePolicyChanged policy when policy.GodId != localGodId && GodStanding.Find(world, policy.GodId) is { } god =>
            (Key("policy", god.Id), Format("{0} now {1}.", god.Name, MigrationText.Practice(policy.Policy)), AlertSeverity.Info, null),
        _ => null,
    };

    /// <summary>The player's followers leave, or people leave giving the player up.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeStart(MigrationStarted started, WorldState world, int? me)
    {
        if (me is null || (started.GodId != me && started.AbandonedGodId != me))
        {
            return null;
        }

        var origin = world.Settlements.Find(s => s.Id == started.OriginSettlementId);
        var text = Format(
            "{0} people leave {1} for {2} ({3}){4}.",
            started.People,
            MigrationText.Name(world, started.OriginSettlementId),
            MigrationText.Name(world, started.DestinationSettlementId),
            MigrationText.Cause(started.Cause),
            started.AbandonedGodId == me ? ", and many of them give you up" : string.Empty);
        return (Key("leave", started.OriginSettlementId), text, AlertSeverity.Warning, origin?.Position);
    }

    /// <summary>Migrants arrive where the player is followed most, or the player's followers end their journey.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeEnd(MigrationEnded ended, WorldState world, int? me)
    {
        if (me is null)
        {
            return null;
        }

        var place = world.Settlements.Find(s => s.Id == ended.SettlementId);
        var (from, to) = (MigrationText.Name(world, ended.OriginSettlementId), MigrationText.Name(world, ended.SettlementId));
        var key = Key("arrive", ended.GroupId);
        if (ended.HostGodId == me && ended.GodId != me && ended.Outcome is MigrationOutcome.Welcomed or MigrationOutcome.Converted)
        {
            return ended.Outcome == MigrationOutcome.Converted
                ? (key, Format("{0} migrants from {1} settled in {2}, and many turned to you.", ended.People, from, to), AlertSeverity.Good, place?.Position)
                : (key, Format("{0} migrants from {1} settled in {2}; your people took them in.", ended.People, from, to), AlertSeverity.Good, place?.Position);
        }

        if (ended.GodId != me)
        {
            return null;
        }

        var host = ended.HostGodId is { } hostId ? GodStanding.Find(world, hostId)?.Name : null;
        return ended.Outcome switch
        {
            MigrationOutcome.Perished => (key, Format("{0} of your followers from {1} died on the road.", ended.People, from), AlertSeverity.Danger, place?.Position),
            MigrationOutcome.Converted => (key, Format("{0} made {1} of your followers from {2} turn to it in {3}.", host ?? "A god", ended.People, from, to), AlertSeverity.Warning, place?.Position),
            MigrationOutcome.Returned => (key, Format("{0} of your followers came home to {1}.", ended.People, to), AlertSeverity.Info, place?.Position),
            MigrationOutcome.Welcomed when ended.HostGodId is { } other && other != me => (key, Format("{0} of your followers from {1} settled in {2}, among {3}'s people.", ended.People, from, to, host), AlertSeverity.Info, place?.Position),
            MigrationOutcome.Welcomed => null,
        };
    }

    /// <summary>The player turned migrants away, or another god turned the player's followers away.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeTurnedAway(MigrantsTurnedAway turned, WorldState world, int? me)
    {
        var place = world.Settlements.Find(s => s.Id == turned.SettlementId);
        var at = MigrationText.Name(world, turned.SettlementId);
        if (turned.ByGodId == me)
        {
            return (Key("turned", turned.GroupId), Format("Your people turned away {0} migrants at {1}.", turned.People, at), AlertSeverity.Info, place?.Position);
        }

        return turned.GodId == me && GodStanding.Find(world, turned.ByGodId) is { } by
            ? (Key("turned", turned.GroupId), Format("{0} turned away {1} of your followers at {2}.", by.Name, turned.People, at), AlertSeverity.Warning, place?.Position)
            : null;
    }

    /// <summary>De-duplication key of an alert.</summary>
    private static string Key(string kind, int id) => string.Create(CultureInfo.InvariantCulture, $"{kind}:{id}");

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
