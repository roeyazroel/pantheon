using System.Globalization;
using Pantheon.Domain.Gods;
using Pantheon.Domain.HolySites;
using Pantheon.Domain.World;
using Pantheon.Simulation.Domains;

namespace Pantheon.Game.Domains;

/// <summary>Words for domains, holy sites and schisms (ADR-021).</summary>
public static class DomainText
{
    /// <summary>A domain's name as the player reads it.</summary>
    /// <param name="domain">The domain.</param>
    /// <returns>For example "Storm".</returns>
    public static string Name(GodDomain domain) => domain switch
    {
        GodDomain.Ocean => "the Sea",
        GodDomain.Nature or GodDomain.War or GodDomain.Life or GodDomain.Death or GodDomain.Knowledge or GodDomain.Prosperity or GodDomain.Storm
            or GodDomain.Fire or GodDomain.Trickery or GodDomain.Justice or GodDomain.Chaos or GodDomain.Protection => domain.ToString(),
    };

    /// <summary>A domain within a sentence, as one takes it up.</summary>
    /// <param name="domain">The domain.</param>
    /// <returns>For example "the Storm" or "Death".</returns>
    public static string InSentence(GodDomain domain) => domain switch
    {
        GodDomain.Storm => "the Storm",
        GodDomain.Ocean => "the Sea",
        GodDomain.Nature or GodDomain.War or GodDomain.Life or GodDomain.Death or GodDomain.Knowledge or GodDomain.Prosperity
            or GodDomain.Fire or GodDomain.Trickery or GodDomain.Justice or GodDomain.Chaos or GodDomain.Protection => domain.ToString(),
    };

    /// <summary>Why a domain cannot be taken up.</summary>
    /// <param name="rejection">The authority's verdict.</param>
    /// <param name="domain">The domain.</param>
    /// <returns>One line.</returns>
    public static string Describe(DomainRejection rejection, GodDomain domain) => rejection switch
    {
        DomainRejection.None => Format("You take up {0}", InSentence(domain)),
        DomainRejection.UnknownGod => "You have no god",
        DomainRejection.NotAuthorized => "You may not choose for another god",
        DomainRejection.UnknownDomain => Format("{0} cannot be chosen", Name(domain)),
        DomainRejection.AlreadyHeld => Format("You hold {0} already", InSentence(domain)),
        DomainRejection.NoFreeSlot => "Rise in rank to take up another domain",
    };

    /// <summary>A god's domains as a list.</summary>
    /// <param name="god">The god.</param>
    /// <returns>For example "Storm, Death", or "no domain yet".</returns>
    public static string List(GodState god)
    {
        System.ArgumentNullException.ThrowIfNull(god);
        return god.Domains.Count == 0 ? "no domain yet" : string.Join(", ", god.Domains.ConvertAll(Name));
    }

    /// <summary>A holy site's name within a sentence.</summary>
    /// <param name="site">The site.</param>
    /// <returns>For example "the Sacred Peak of Elar".</returns>
    public static string SiteName(HolySite site)
    {
        System.ArgumentNullException.ThrowIfNull(site);
        return site.Name.StartsWith("The ", System.StringComparison.Ordinal) ? "the " + site.Name[4..] : site.Name;
    }

    /// <summary>A holy site's name by id within a sentence.</summary>
    /// <param name="world">The world.</param>
    /// <param name="siteId">The site.</param>
    /// <returns>Its name, or "a forgotten holy place".</returns>
    public static string SiteName(WorldState world, int siteId)
    {
        System.ArgumentNullException.ThrowIfNull(world);
        return world.HolySites.Find(s => s.Id == siteId) is { } site ? SiteName(site) : "a forgotten holy place";
    }

    /// <summary>Who keeps a site and who is taking it.</summary>
    /// <param name="world">The world.</param>
    /// <param name="site">The site.</param>
    /// <param name="claimSeconds">Seconds a claim takes.</param>
    /// <returns>For example "kept by Seren; Vael is taking it (20 of 45 s)".</returns>
    public static string SiteState(WorldState world, HolySite site, float claimSeconds)
    {
        System.ArgumentNullException.ThrowIfNull(world);
        System.ArgumentNullException.ThrowIfNull(site);
        var keeper = site.KeeperGodId is { } k ? "kept by " + GodName(world, k) : "unclaimed";
        return site.ClaimantGodId is { } c
            ? Format("{0}; {1} is taking it ({2:0} of {3:0} s)", keeper, GodName(world, c), site.ClaimTicks / SimulationTime.TicksPerSecond, claimSeconds)
            : keeper;
    }

    /// <summary>A god's name, or "a forgotten god".</summary>
    private static string GodName(WorldState world, int godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a forgotten god";

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
