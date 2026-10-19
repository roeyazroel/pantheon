using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Game.Politics;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about treaties, favours and the Divine Council (spec §38–42, §54, §59, ADR-025). A secret
/// treaty is news only to its parties until it comes to light. Part of <see cref="AlertFeed"/>.
/// </summary>
internal static class PoliticsAlerts
{
    /// <summary>A treaty or request put to the player.</summary>
    /// <param name="world">World state.</param>
    /// <param name="fromGodId">The god proposing.</param>
    /// <param name="localGodId">The player's god.</param>
    /// <param name="key">The alert's key.</param>
    /// <returns>The alert, or <c>null</c> when the proposal is gone.</returns>
    public static (string, string, AlertSeverity, GroundPosition?)? Proposal(WorldState world, int fromGodId, int localGodId, string key)
    {
        if (DiplomacyAuthority.FindProposal(world, fromGodId, localGodId) is not { } proposal)
        {
            return null;
        }

        var name = PoliticsText.GodName(world, fromGodId);
        if (proposal.Terms is { } terms)
        {
            return (key, Format("{0} proposes {1} (P to answer).", name, PoliticsText.Terms(terms, world, localGodId)), AlertSeverity.Warning, null);
        }

        if (proposal.Ask is not { } ask)
        {
            return null;
        }

        return FavourBook.Find(world, fromGodId, localGodId) is not null
            ? (key, Format("{0} calls in the favour you owe it: {1} (P to answer).", name, PoliticsText.Ask(ask, world)), AlertSeverity.Warning, null)
            : (key, Format("{0} asks you for {1} (P to answer).", name, PoliticsText.Ask(ask, world)), AlertSeverity.Warning, null);
    }

    /// <summary>The alert for an event, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="me">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? me)
    {
        string God(int id) => PoliticsText.GodName(world, id);
        switch (worldEvent)
        {
            case TreatySigned signed when signed.FirstGodId == me || signed.SecondGodId == me:
                return (Key("signed", signed.TreatyId), Format("You and {0} signed a {1}{2}.", God(signed.FirstGodId == me ? signed.SecondGodId : signed.FirstGodId), signed.Secret ? "secret " : string.Empty, PoliticsText.Name(signed.Kind)), AlertSeverity.Good, null);
            case TreatySigned { Secret: false } signed:
                return (Key("signed", signed.TreatyId), Format("{0} and {1} signed a {2}.", God(signed.FirstGodId), God(signed.SecondGodId), PoliticsText.Name(signed.Kind)), AlertSeverity.Info, null);
            case TreatyEnded ended:
                return Ended(ended, world, me);
            case TreatyRevealed revealed:
                return Revealed(revealed, world, me);
            case TreatyInvoked invoked when invoked.AggressorGodId == me:
                return (Key("invoked", invoked.TreatyId), Format("{0} honours its pact with {1} and declares war on you!", God(invoked.DefenderGodId), God(invoked.ProtectedGodId)), AlertSeverity.Danger, null);
            case TreatyInvoked invoked when invoked.ProtectedGodId == me:
                return (Key("invoked", invoked.TreatyId), Format("{0} comes to your defence against {1}.", God(invoked.DefenderGodId), God(invoked.AggressorGodId)), AlertSeverity.Good, null);
            case TreatyInvoked invoked when invoked.DefenderGodId == me:
                return (Key("invoked", invoked.TreatyId), Format("You honour your pact with {0}: you are at war with {1}.", God(invoked.ProtectedGodId), God(invoked.AggressorGodId)), AlertSeverity.Warning, null);
            case FavourOwed owed when owed.CreditorGodId == me:
                return (Key("favour", owed.DebtorGodId), Format("{0} owes you a favour: you saved its people{1}.", God(owed.DebtorGodId), Place(world, owed.SettlementId)), AlertSeverity.Good, null);
            case FavourOwed owed when owed.DebtorGodId == me:
                return (Key("owing", owed.CreditorGodId), Format("You owe {0} a favour: it saved your people{1}.", God(owed.CreditorGodId), Place(world, owed.SettlementId)), AlertSeverity.Info, null);
            case RequestAnswered answer when answer.AskerGodId == me:
                return answer.Granted
                    ? (Key("granted", answer.AskedGodId), Format("{0} granted your request.", God(answer.AskedGodId)), AlertSeverity.Good, null)
                    : (Key("refused", answer.AskedGodId), Format("{0} refused your request{1}.", God(answer.AskedGodId), answer.Owed ? ", though it owed you" : string.Empty), AlertSeverity.Warning, null);
            case ObligationEnded { Kept: false } broken when broken.CreditorGodId == me:
                return (Key("promise", broken.DebtorGodId), Format("{0} broke its promise to you.", God(broken.DebtorGodId)), AlertSeverity.Warning, null);
            case ObligationEnded { Kept: false } broken when broken.DebtorGodId == me:
                return (Key("promise", broken.CreditorGodId), Format("You broke your promise to {0}: it trusts you less.", God(broken.CreditorGodId)), AlertSeverity.Warning, null);
            case CouncilSummoned summoned:
                return (Key("council", (int)summoned.Motion.Kind), Format("The Divine Council meets {0}: {1}. Vote in the council (U).", PoliticsText.Trigger(summoned.Trigger), PoliticsText.Motion(summoned.Motion, world)), AlertSeverity.Warning, null);
            case CouncilClosed closed:
                return (Key("closed", (int)closed.Motion.Kind), Format("The council {0} \"{1}\": {2} for, {3} against.", closed.Passed ? "passes" : "rejects", PoliticsText.Motion(closed.Motion, world), closed.Yes, closed.No),
                    closed.Passed && closed.Motion.TargetGodId == me ? AlertSeverity.Danger : closed.Passed ? AlertSeverity.Good : AlertSeverity.Info, null);
            case CouncilDefied defied when defied.GodId == me:
                return (Key("defied", 0), "You defied the council: every god trusts you less.", AlertSeverity.Warning, null);
            case CouncilDefied defied:
                return (Key("defied", defied.GodId), Format("{0} defied the council: {1}.", God(defied.GodId), PoliticsText.Decree(defied.Motion, world)), AlertSeverity.Info, null);
            case AllyAbsorbed absorbed when absorbed.AllyGodId == me:
                return (Key("absorbed", absorbed.GodId), Format("{0} broke your alliance and took {1} of your followers!", God(absorbed.GodId), absorbed.People), AlertSeverity.Danger, null);
            case AllyAbsorbed absorbed:
                return (Key("absorbed", absorbed.GodId), Format("{0} absorbed {1} followers of its ally {2}.", God(absorbed.GodId), absorbed.People, God(absorbed.AllyGodId)), AlertSeverity.Info, null);
            default:
                return null;
        }
    }

    /// <summary>A treaty ran out or was broken.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? Ended(TreatyEnded ended, WorldState world, int? me)
    {
        var name = PoliticsText.Name(ended.Kind);
        var party = ended.FirstGodId == me || ended.SecondGodId == me;
        var partner = PoliticsText.GodName(world, ended.FirstGodId == me ? ended.SecondGodId : ended.FirstGodId);
        if (ended.BrokenByGodId is not { } breaker)
        {
            return party ? (Key("ended", ended.TreatyId), Format("Your {0} with {1} has run its course.", name, partner), AlertSeverity.Info, null) : null;
        }

        return breaker == me ? null
            : party ? (Key("broken", ended.TreatyId), Format("{0} broke your {1}!", partner, name), AlertSeverity.Danger, null)
            : ended.Secret ? null
            : (Key("broken", ended.TreatyId), Format("{0} broke its {1} with {2}.", PoliticsText.GodName(world, breaker), name, PoliticsText.GodName(world, breaker == ended.FirstGodId ? ended.SecondGodId : ended.FirstGodId)), AlertSeverity.Info, null);
    }

    /// <summary>A secret treaty came to light.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? Revealed(TreatyRevealed revealed, WorldState world, int? me)
    {
        var name = PoliticsText.Name(revealed.Kind);
        var (a, b) = (PoliticsText.GodName(world, revealed.FirstGodId), PoliticsText.GodName(world, revealed.SecondGodId));
        var key = Key("revealed", revealed.TreatyId);
        if (revealed.TargetGodId == me)
        {
            return (key, Format("A secret {0} between {1} and {2} against you has come to light!", name, a, b), AlertSeverity.Danger, null);
        }

        return revealed.FirstGodId == me || revealed.SecondGodId == me
            ? (key, Format("Your secret {0} with {1} has come to light.", name, revealed.FirstGodId == me ? b : a), AlertSeverity.Warning, null)
            : (key, Format("A secret {0} between {1} and {2} has come to light.", name, a, b), AlertSeverity.Info, null);
    }

    /// <summary>" in Brightwater", or nothing.</summary>
    private static string Place(WorldState world, int? settlementId) =>
        settlementId is { } id && world.Settlements.Find(s => s.Id == id) is { } place ? " in " + place.Name : string.Empty;

    /// <summary>De-duplication key of an alert.</summary>
    private static string Key(string kind, int id) => string.Create(CultureInfo.InvariantCulture, $"politics-{kind}:{id}");

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
