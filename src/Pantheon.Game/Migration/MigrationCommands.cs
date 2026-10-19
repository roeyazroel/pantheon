using System;
using System.Globalization;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Migration;

namespace Pantheon.Game.Migration;

/// <summary>
/// Commands for migration (ADR-016): the player's refugee policy, and the debug driver that sends
/// people out of a settlement. Each returns a status line.
/// </summary>
public static class MigrationCommands
{
    /// <summary>People a debug migration sends out.</summary>
    public const int DebugGroup = 20;

    /// <summary>How near a point a settlement must be for the debug migration to pick it, in metres.</summary>
    public const float PickRadius = 150f;

    /// <summary>Sets the local player's refugee policy.</summary>
    /// <param name="session">The session.</param>
    /// <param name="policy">The policy.</param>
    /// <returns>Status line.</returns>
    public static string SetPolicy(GameSession session, RefugeePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.SetRefugeePolicy(policy) switch
        {
            RefugeePolicyRejection.None => "Your people now " + Verb(policy),
            RefugeePolicyRejection.Unchanged => "That is already your refugee policy",
            RefugeePolicyRejection.UnknownGod or RefugeePolicyRejection.NotAuthorized => "You have no god to set a refugee policy for",
        };
    }

    /// <summary>Sends a group out of the settlement nearest a point.</summary>
    /// <param name="session">The session.</param>
    /// <param name="point">The point.</param>
    /// <param name="people">How many leave.</param>
    /// <returns>Status line.</returns>
    public static string SendFrom(GameSession session, GroundPosition point, int people = DebugGroup)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Nearest(session.World, point) is { } settlement
            ? SendFrom(session, settlement, people)
            : "Point at a village to send people away";
    }

    /// <summary>Sends a group out of a settlement.</summary>
    /// <param name="session">The session.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="people">How many.</param>
    /// <returns>Status line.</returns>
    public static string SendFrom(GameSession session, SettlementState settlement, int people)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settlement);
        return session.SendMigrants(settlement, people) is { } group
            ? string.Create(CultureInfo.InvariantCulture, $"{group.Population} people leave {settlement.Name} for {MigrationText.Name(session.World, group.DestinationSettlementId)}")
            : string.Create(CultureInfo.InvariantCulture, $"Nobody can leave {settlement.Name} now: too few people, or nowhere better to go");
    }

    /// <summary>What the player's people do with migrants under a policy.</summary>
    private static string Verb(RefugeePolicy policy) => policy switch
    {
        RefugeePolicy.Welcome => "welcome migrants",
        RefugeePolicy.DemandConversion => "take in migrants who turn to you",
        RefugeePolicy.TurnAway => "turn migrants away",
    };

    /// <summary>The settlement nearest a point within <see cref="PickRadius"/>.</summary>
    private static SettlementState? Nearest(WorldState world, GroundPosition point)
    {
        SettlementState? nearest = null;
        var best = PickRadius * PickRadius;
        foreach (var settlement in world.Settlements)
        {
            var (dx, dz) = (settlement.Position.X - point.X, settlement.Position.Z - point.Z);
            var distance = (dx * dx) + (dz * dz);
            if (distance <= best)
            {
                best = distance;
                nearest = settlement;
            }
        }

        return nearest;
    }
}
