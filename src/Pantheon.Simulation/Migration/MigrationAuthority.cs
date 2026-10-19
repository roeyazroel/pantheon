using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Migration;

/// <summary>Why the authority refuses a refugee policy, or <see cref="None"/> when it is set.</summary>
public enum RefugeePolicyRejection
{
    /// <summary>The policy was set.</summary>
    None,

    /// <summary>The god does not exist.</summary>
    UnknownGod,

    /// <summary>The issuer does not control the god.</summary>
    NotAuthorized,

    /// <summary>The god already has that policy.</summary>
    Unchanged,
}

/// <summary>
/// The authority for a god's refugee policy (spec §31, §73, ADR-016): the local player, the AI gods
/// and, later, a multiplayer host set it through here.
/// </summary>
public sealed class MigrationAuthority
{
    /// <summary>Whether a policy would be set, without changing anything.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="policy">The policy.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <returns>Why it would be refused, or <see cref="RefugeePolicyRejection.None"/>.</returns>
    public static RefugeePolicyRejection Check(WorldState world, int godId, RefugeePolicy policy, int? issuerGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (GodStanding.Find(world, godId) is not { } god)
        {
            return RefugeePolicyRejection.UnknownGod;
        }

        return issuerGodId is { } issuer && issuer != godId ? RefugeePolicyRejection.NotAuthorized
            : god.RefugeePolicy == policy ? RefugeePolicyRejection.Unchanged
            : RefugeePolicyRejection.None;
    }

    /// <summary>Checks a policy and sets it when allowed.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="policy">The policy.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <param name="events">Receives a <see cref="RefugeePolicyChanged"/> event when set.</param>
    /// <returns>Why it was refused, or <see cref="RefugeePolicyRejection.None"/>.</returns>
    public RefugeePolicyRejection SetPolicy(WorldState world, int godId, RefugeePolicy policy, int? issuerGodId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var rejection = Check(world, godId, policy, issuerGodId);
        if (rejection == RefugeePolicyRejection.None)
        {
            GodStanding.Find(world, godId)!.RefugeePolicy = policy;
            events.Add(new RefugeePolicyChanged { Tick = world.Tick, GodId = godId, Policy = policy });
        }

        return rejection;
    }
}
