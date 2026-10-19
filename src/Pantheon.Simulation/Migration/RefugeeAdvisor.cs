using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Migration;

/// <summary>
/// An AI god's refugee policy (ADR-016): its personality's, except that a welcoming god turns
/// migrants away while its own followers lack room.
/// </summary>
public sealed class RefugeeAdvisor
{
    private readonly MigrationRules _rules;
    private readonly MigrationAuthority _authority;

    /// <summary>Creates the advisor.</summary>
    /// <param name="rules">Migration rules.</param>
    /// <param name="authority">The authority it sets policies through.</param>
    public RefugeeAdvisor(MigrationRules rules, MigrationAuthority authority)
    {
        MigrationRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(authority);
        _rules = rules;
        _authority = authority;
    }

    /// <summary>The policy the god wants now, and why.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <returns>The policy and a short reason.</returns>
    public (RefugeePolicy Policy, string Reason) Choose(WorldState world, GodState god, GodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        var homeless = MigrationModel.FollowersHomeless(world, god.Id);
        return profile.RefugeePolicy == RefugeePolicy.Welcome && homeless > _rules.OverwhelmedHomeless
            ? (RefugeePolicy.TurnAway, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"no room: {homeless:0%} of its followers homeless"))
            : (profile.RefugeePolicy, "its nature");
    }

    /// <summary>Sets the god's policy through the authority when it wants another.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <param name="events">Receives the change, if any.</param>
    public void Act(WorldState world, GodState god, GodProfile profile, ICollection<WorldEvent> events)
    {
        var (policy, _) = Choose(world, god, profile);
        if (policy != god.RefugeePolicy)
        {
            _authority.SetPolicy(world, god.Id, policy, god.Id, events);
        }
    }
}
