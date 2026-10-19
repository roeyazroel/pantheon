using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Domains;

/// <summary>Why the authority refuses a domain, or <see cref="None"/> when the god may take it up.</summary>
public enum DomainRejection
{
    /// <summary>The god may take it up.</summary>
    None,

    /// <summary>No god has the requested id.</summary>
    UnknownGod,

    /// <summary>The issuer does not control the god.</summary>
    NotAuthorized,

    /// <summary>Gods may not choose the domain.</summary>
    UnknownDomain,

    /// <summary>The god holds it already.</summary>
    AlreadyHeld,

    /// <summary>The god holds as many domains as its rank allows.</summary>
    NoFreeSlot,
}

/// <summary>
/// The authority for domains (spec §12, §73, ADR-021): a god takes up a domain when its rank leaves
/// a slot free. A domain, once taken up, is held for good.
/// </summary>
public sealed class DomainAuthority
{
    /// <summary>Creates the authority over validated rules.</summary>
    /// <param name="rules">Domain rules.</param>
    public DomainAuthority(DomainRules rules)
    {
        DomainRulesValidator.Validate(rules);
        Rules = rules;
    }

    /// <summary>The domain rules.</summary>
    public DomainRules Rules { get; }

    /// <summary>Checks whether a god may take up a domain, without changing the world.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <returns>The verdict.</returns>
    public DomainRejection Check(WorldState world, int godId, GodDomain domain, int? issuerGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return GodStanding.Find(world, godId) is not { } god ? DomainRejection.UnknownGod
            : issuerGodId is { } issuer && issuer != godId ? DomainRejection.NotAuthorized
            : DomainModel.Find(Rules, domain) is null ? DomainRejection.UnknownDomain
            : god.Domains.Contains(domain) ? DomainRejection.AlreadyHeld
            : DomainModel.FreeSlots(Rules, god) <= 0 ? DomainRejection.NoFreeSlot
            : DomainRejection.None;
    }

    /// <summary>Checks the choice and, when it is allowed, gives the god the domain.</summary>
    /// <param name="world">World state to change.</param>
    /// <param name="godId">The god.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <param name="events">Receives <see cref="DomainChosen"/>.</param>
    /// <returns>The verdict; the world changed only when it is <see cref="DomainRejection.None"/>.</returns>
    public DomainRejection Choose(WorldState world, int godId, GodDomain domain, int? issuerGodId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var rejection = Check(world, godId, domain, issuerGodId);
        if (rejection == DomainRejection.None)
        {
            GodStanding.Find(world, godId)!.Domains.Add(domain);
            events.Add(new DomainChosen { Tick = world.Tick, GodId = godId, Domain = domain });
        }

        return rejection;
    }
}
