using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game.Conflict;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about wars' goals, covert acts and holy wars (spec §43–46, §59, ADR-026). A proxy act is
/// news only to its author, unless it is exposed. Part of <see cref="AlertFeed"/>.
/// </summary>
internal static class ConflictAlerts
{
    /// <summary>The alert for an event, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="me">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? me)
    {
        string God(int id) => ConflictText.GodName(world, id);
        string Place(int? id) => world.Settlements.Find(s => s.Id == id)?.Name ?? "a lost village";
        GroundPosition? Where(int? id) => world.Settlements.Find(s => s.Id == id)?.Position;
        switch (worldEvent)
        {
            case WarGoalAchieved won when won.GodId == me:
                return (Key("goal", won.EnemyGodId), ConflictText.Format("You have won your war on {0}: demand its surrender (P).", God(won.EnemyGodId)), AlertSeverity.Good, null);
            case WarGoalAchieved won when won.EnemyGodId == me:
                return (Key("goal", won.GodId), ConflictText.Format("{0} has won its war on you and may dictate peace.", God(won.GodId)), AlertSeverity.Danger, null);
            case PeaceDictated peace when peace.WinnerGodId == me:
                return (Key("dictated", peace.LoserGodId), ConflictText.Format("{0} accepted your terms: the war is over.", God(peace.LoserGodId)), AlertSeverity.Good, Where(peace.SettlementId));
            case PeaceDictated peace when peace.LoserGodId == me:
                return (Key("dictated", peace.WinnerGodId), ConflictText.Format("You accepted {0}'s terms: the war is over.", God(peace.WinnerGodId)), AlertSeverity.Warning, Where(peace.SettlementId));
            case PeaceDictated peace:
                return (Key("dictated", peace.WinnerGodId), ConflictText.Format("{0} dictated peace to {1}.", God(peace.WinnerGodId), God(peace.LoserGodId)), AlertSeverity.Info, null);
            case CovertActExposed exposed when exposed.VictimGodId == me:
                return (Key("exposed", exposed.GodId), ConflictText.Format("{0} was behind the {1} in {2}!", God(exposed.GodId), Act(exposed.Act), Place(exposed.SettlementId)), AlertSeverity.Danger, Where(exposed.SettlementId));
            case CovertActExposed exposed when exposed.GodId == me:
                return (Key("caught", exposed.VictimGodId), ConflictText.Format("{0} traced the {1} in {2} to you.", God(exposed.VictimGodId), Act(exposed.Act), Place(exposed.SettlementId)), AlertSeverity.Warning, Where(exposed.SettlementId));
            case CovertActExposed exposed:
                return (Key("exposed", exposed.GodId), ConflictText.Format("{0} was caught working against {1} in secret.", God(exposed.GodId), God(exposed.VictimGodId)), AlertSeverity.Info, null);
            case HolyWarDeclared declared when declared.GodId == me:
                return (Key("holywar", declared.SettlementId), ConflictText.Format("Your people in {0} declare holy war on {1}. Urge them on or hold them back (N).", Place(declared.SettlementId), Place(declared.DefenderSettlementId)), AlertSeverity.Warning, Where(declared.SettlementId));
            case HolyWarDeclared declared when declared.EnemyGodId == me:
                return (Key("holywar", declared.SettlementId), ConflictText.Format("{0}, for {1}, wages holy war on your people in {2}!", Place(declared.SettlementId), God(declared.GodId), Place(declared.DefenderSettlementId)), AlertSeverity.Danger, Where(declared.DefenderSettlementId));
            case HolyWarEnded ended when ended.GodId == me || world.Settlements.Find(s => s.Id == ended.DefenderSettlementId) is { } attacked && AlertFeed.Concerns(attacked, me):
                return (Key("holypeace", ended.SettlementId), ConflictText.Format(
                    "The holy war of {0} on {1} is over{2}: {3} killed, {4} converted.",
                    Place(ended.SettlementId), Place(ended.DefenderSettlementId), ended.Won ? ", and the village turned" : string.Empty, ended.Killed, ended.Converted), AlertSeverity.Info, Where(ended.DefenderSettlementId));
            default:
                return null;
        }
    }

    /// <summary>A covert act's name: a miracle's id, or a proxy act.</summary>
    private static string Act(string act) => act switch
    {
        "Incite" => "unrest",
        "Propaganda" => "propaganda",
        _ => act.Replace('_', ' '),
    };

    /// <summary>De-duplication key of an alert.</summary>
    private static string Key(string kind, int id) => string.Create(CultureInfo.InvariantCulture, $"conflict-{kind}:{id}");
}
