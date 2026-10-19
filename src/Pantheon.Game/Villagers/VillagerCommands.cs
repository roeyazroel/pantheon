using System;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Game.Villagers;

/// <summary>
/// The player follows a villager (spec §80's selected villager, ADR-043): / follows the figure
/// nearest the cursor, which becomes an individual of the world; Shift+/ lets them go. Each returns
/// a status line.
/// </summary>
public static class VillagerCommands
{
    /// <summary>How far from the cursor, in metres, a figure may stand to be followed.</summary>
    public const float PickRadius = 6f;

    // How far from a village's centre, in metres, the cursor may be for its people to be picked.
    private const float VillageReach = 150f;

    /// <summary>The god's followed villager, if any.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The villager, or <c>null</c>.</returns>
    public static VillagerState? Of(WorldState world, int? godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return godId is { } id ? world.Villagers.Find(v => v.FollowerGodId == id) : null;
    }

    /// <summary>The figure nearest a point among the people of the villages around it, as a villager to follow.</summary>
    /// <param name="session">The session.</param>
    /// <param name="view">Settlement presentation settings, for the figures' rounds.</param>
    /// <param name="point">Where the player points.</param>
    /// <param name="radius">How far from the point the figure may stand.</param>
    /// <returns>The villager to follow, or <c>null</c> when nobody stands there.</returns>
    public static FollowVillager? Pick(GameSession session, SettlementViewSettings view, GroundPosition point, float radius = PickRadius)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(view);
        FollowVillager? best = null;
        var bestDistance = radius;
        foreach (var settlement in session.World.Settlements)
        {
            if (settlement.Population <= 0 || Distance(settlement.Position, point) > VillageReach)
            {
                continue;
            }

            foreach (var candidate in Figures(session, view, settlement))
            {
                var distance = Distance(candidate.Position, point);
                if (distance <= bestDistance)
                {
                    (best, bestDistance) = (candidate, distance);
                }
            }
        }

        return best;
    }

    /// <summary>The first figure of a village, as a villager to follow; for the launch option.</summary>
    /// <param name="session">The session.</param>
    /// <param name="view">Settlement presentation settings.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>The villager to follow, or <c>null</c> when nobody lives there.</returns>
    public static FollowVillager? First(GameSession session, SettlementViewSettings view, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        foreach (var candidate in Figures(session, view, settlement))
        {
            return candidate;
        }

        return null;
    }

    /// <summary>Follows the figure nearest a point.</summary>
    /// <param name="session">The session.</param>
    /// <param name="view">Settlement presentation settings.</param>
    /// <param name="point">Where the player points.</param>
    /// <returns>Status line.</returns>
    public static string FollowAt(GameSession session, SettlementViewSettings view, GroundPosition point) =>
        Pick(session, view, point) is { } villager ? Follow(session, villager) : "Nobody there: point at someone in a village and press / to follow them";

    /// <summary>The local player's god follows a villager; a joined player asks the host.</summary>
    /// <param name="session">The session.</param>
    /// <param name="command">The villager.</param>
    /// <returns>Status line.</returns>
    public static string Follow(GameSession session, FollowVillager command)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Client is { } client
            ? client.Request(id => new Networking.Protocol.VillagerIntent(id, command))
            : FollowAs(session, session.LocalGodId ?? 0, command).Text;
    }

    /// <summary>The local player's god lets its villager go; a joined player asks the host.</summary>
    /// <param name="session">The session.</param>
    /// <returns>Status line.</returns>
    public static string Release(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Client is { } client
            ? client.Request(id => new Networking.Protocol.VillagerIntent(id, null))
            : FollowAs(session, session.LocalGodId ?? 0, null).Text;
    }

    /// <summary>A god follows a villager, or lets its villager go, as its player's intent.</summary>
    /// <param name="session">The session.</param>
    /// <param name="godId">The god.</param>
    /// <param name="command">The villager to follow; <c>null</c> lets the one followed go.</param>
    /// <returns>Whether it was done, and the status line.</returns>
    public static CommandOutcome FollowAs(GameSession session, int godId, FollowVillager? command)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        var before = Of(world, godId);
        var rejection = session.Command((authorities, events) => authorities.Villagers is not { } villagers ? VillagerRejection.NoVillagers
            : command is null ? villagers.Release(world, godId)
            : villagers.Follow(world, command with { GodId = godId }, events));
        if (rejection != VillagerRejection.None)
        {
            return new CommandOutcome(false, VillagerText.Describe(rejection));
        }

        return command is null || Of(world, godId) is not { } villager
            ? new CommandOutcome(true, before is null ? "You let them go" : $"You let {before.Name} go back to the crowd of {VillagerText.Village(world, before.SettlementId)}")
            : new CommandOutcome(true, VillagerText.Followed(session.Content.SettlementRules.Villagers, villager, world));
    }

    /// <summary>A village's figures where they stand now, as villagers to follow.</summary>
    private static System.Collections.Generic.IEnumerable<FollowVillager> Figures(GameSession session, SettlementViewSettings view, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(view);
        var rules = session.Content.SettlementRules;
        var site = SettlementLayout.ComputeSite(settlement, rules, session.Terrain, view.Layout);
        var roster = RepresentativeRoster.Build(session.World.Seed, settlement, SettlementEconomy.AllocateLabour(rules, settlement), site, view);
        foreach (var figure in roster)
        {
            var pose = CitizenRoutine.Evaluate(figure, session.Crowds.Seconds, view.Citizens);
            var work = figure.Role switch
            {
                CitizenRole.Farmer => VillagerWork.Farming,
                CitizenRole.Builder => VillagerWork.Building,
                CitizenRole.Villager => VillagerWork.Chores,
            };
            yield return new FollowVillager(session.LocalGodId ?? 0, settlement.Id, figure.Index, work, Ground(figure.Home), Ground(figure.Work), Ground(figure.Gathering), Ground(pose.Ground));
        }
    }

    /// <summary>A layout point on the ground.</summary>
    private static GroundPosition Ground(Vector2 point) => new(point.X, point.Y);

    /// <summary>Metres between two points on the ground.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
