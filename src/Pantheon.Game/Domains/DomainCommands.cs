using System;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Domains;

namespace Pantheon.Game.Domains;

/// <summary>The player's choice of domains (spec §12, ADR-021). Each command returns a status line.</summary>
public static class DomainCommands
{
    /// <summary>Takes up a domain for the local player's god; a joined player asks the host.</summary>
    /// <param name="session">The session.</param>
    /// <param name="domain">The domain.</param>
    /// <returns>Status line.</returns>
    public static string Choose(GameSession session, GodDomain domain)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Client is { } client
            ? client.Request(id => new Networking.Protocol.DomainIntent(id, domain))
            : ChooseAs(session, session.LocalGodId ?? 0, domain).Text;
    }

    /// <summary>Takes up a domain for a god, as its player's intent.</summary>
    /// <param name="session">The session.</param>
    /// <param name="godId">The issuing god.</param>
    /// <param name="domain">The domain.</param>
    /// <returns>Whether it was taken up, and the status line.</returns>
    public static CommandOutcome ChooseAs(GameSession session, int godId, GodDomain domain)
    {
        ArgumentNullException.ThrowIfNull(session);
        var rejection = session.ChooseDomainAs(godId, domain);
        return new CommandOutcome(rejection == DomainRejection.None, DomainText.Describe(rejection, domain));
    }
}
