using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game.Domains;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about domains, holy sites and schisms (spec §12, §34, §51, §59, ADR-021): rivals taking up
/// domains, holy sites the player takes or loses or that are being taken from it, and the player's
/// followers breaking away or returning. Part of <see cref="AlertFeed"/>.
/// </summary>
internal static class FaithAlerts
{
    /// <summary>The alert for an event, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId) => worldEvent switch
    {
        DomainChosen chosen when chosen.GodId != localGodId && GodStanding.Find(world, chosen.GodId) is { } god =>
            (Key("domain", god.Id), Format("{0} takes up {1}.", god.Name, DomainText.InSentence(chosen.Domain)), AlertSeverity.Info, null),
        HolySiteClaimed claim => DescribeClaim(claim, world, localGodId),
        HolySiteContested contest when contest.KeeperGodId == localGodId && world.HolySites.Find(s => s.Id == contest.SiteId) is { } site && GodStanding.Find(world, contest.GodId) is { } rival =>
            (Key("contest", site.Id), Format("{0} is taking {1} from you. Send your Creature there, or win back the people around it.", rival.Name, DomainText.SiteName(site)), AlertSeverity.Warning, site.Position),
        SchismStarted schism when schism.GodId == localGodId && world.Settlements.Find(s => s.Id == schism.SettlementId) is { } place =>
            (Key("schism", place.Id), Format("Schism in {0}: {1} of your followers break away to keep your Old Faith. Win back their love.", place.Name, schism.People), AlertSeverity.Danger, place.Position),
        SchismEnded ended when ended.GodId == localGodId && world.Settlements.Find(s => s.Id == ended.SettlementId) is { } place =>
            ended.Reconciled
                ? (Key("reunion", place.Id), Format("The Old Faith of {0} returns to you.", place.Name), AlertSeverity.Good, place.Position)
                : (Key("reunion", place.Id), Format("The Old Faith of {0} has faded into unbelief.", place.Name), AlertSeverity.Warning, place.Position),
        _ => null,
    };

    /// <summary>The player took a site, lost one, or saw a rival take one.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeClaim(HolySiteClaimed claim, WorldState world, int? me)
    {
        if (world.HolySites.Find(s => s.Id == claim.SiteId) is not { } site || GodStanding.Find(world, claim.GodId) is not { } god)
        {
            return null;
        }

        var name = DomainText.SiteName(site);
        return claim.GodId == me ? (Key("site", site.Id), Format("You claim {0}.", name), AlertSeverity.Good, site.Position)
            : claim.PreviousGodId == me ? (Key("site", site.Id), Format("{0} has taken {1} from you.", god.Name, name), AlertSeverity.Danger, site.Position)
            : (Key("site", site.Id), Format("{0} claims {1}.", god.Name, name), AlertSeverity.Info, site.Position);
    }

    /// <summary>De-duplication key of an alert.</summary>
    private static string Key(string kind, int id) => string.Create(CultureInfo.InvariantCulture, $"{kind}:{id}");

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
