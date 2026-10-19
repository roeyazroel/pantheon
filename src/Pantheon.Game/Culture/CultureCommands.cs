using System;
using Pantheon.Domain.Culture;
using Pantheon.Game.Launch;

namespace Pantheon.Game.Culture;

/// <summary>Debug commands that shape a settlement's culture, for trying doctrines and secularism out (ADR-024). Each returns a status line.</summary>
public static class CultureCommands
{
    // Secularism given to a settlement turned against the gods: past the anti-divine stage.
    private const float Godless = 0.9f;

    /// <summary>Gives a settlement a doctrine in full, or turns it against the gods.</summary>
    /// <param name="session">The session; only the host may change the world.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="culture">The doctrine, or secularism.</param>
    /// <returns>Status line.</returns>
    public static string Impose(GameSession session, int settlementId, CultureAtStart culture)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        if (session.World.Settlements.Find(s => s.Id == settlementId) is not { } settlement)
        {
            return "No such village";
        }

        if (Doctrine(culture) is { } doctrine)
        {
            settlement.Culture.Set(doctrine, 1f);
            return settlement.Name + " embraces " + CultureText.Name(doctrine);
        }

        settlement.Culture.Secularism = Godless;
        return settlement.Name + " turns against the gods";
    }

    /// <summary>The doctrine a launch choice names, or <c>null</c> for secularism.</summary>
    private static Doctrine? Doctrine(CultureAtStart culture) => culture switch
    {
        CultureAtStart.Pacifism => Domain.Culture.Doctrine.Pacifism,
        CultureAtStart.Militarism => Domain.Culture.Doctrine.Militarism,
        CultureAtStart.Sacrifice => Domain.Culture.Doctrine.Sacrifice,
        CultureAtStart.Charity => Domain.Culture.Doctrine.Charity,
        CultureAtStart.NatureWorship => Domain.Culture.Doctrine.NatureWorship,
        CultureAtStart.KnowledgeSeeking => Domain.Culture.Doctrine.KnowledgeSeeking,
        CultureAtStart.Expansionism => Domain.Culture.Doctrine.Expansionism,
        CultureAtStart.Isolationism => Domain.Culture.Doctrine.Isolationism,
        CultureAtStart.Secular => null,
    };
}
