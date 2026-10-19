using System.Globalization;
using Pantheon.Domain.Society;
using Pantheon.Simulation.Society;

namespace Pantheon.Game.Society;

/// <summary>Words for settlements' society (ADR-023): priorities, buildings, fields of technology and refusals.</summary>
public static class SocietyText
{
    /// <summary>A priority's short name.</summary>
    /// <param name="priority">The priority.</param>
    /// <returns>For example "Prepare for disaster".</returns>
    public static string Name(SettlementPriority priority) => priority switch
    {
        SettlementPriority.None => "Run itself",
        SettlementPriority.Food => "Increase food",
        SettlementPriority.Health => "Improve health",
        SettlementPriority.Growth => "Grow",
        SettlementPriority.Worship => "Focus on worship",
        SettlementPriority.Defence => "Prepare for war",
        SettlementPriority.Preparation => "Prepare for disaster",
        SettlementPriority.Expansion => "Expand",
        SettlementPriority.Temple => "Build a temple",
    };

    /// <summary>What a priority does.</summary>
    /// <param name="priority">The priority.</param>
    /// <returns>One sentence.</returns>
    public static string Describe(SettlementPriority priority) => priority switch
    {
        SettlementPriority.None => "The people decide for themselves.",
        SettlementPriority.Food => "More is harvested, and a granary keeps more food and saves it from disasters.",
        SettlementPriority.Health => "Healers tend the sick, and a hospital saves lives.",
        SettlementPriority.Growth => "More children are born while there is food and room.",
        SettlementPriority.Worship => "The people spend twice the time worshipping, raising more power.",
        SettlementPriority.Defence => "Watchmen keep the people safe, and walls turn attacks away.",
        SettlementPriority.Preparation => "Levees, firebreaks, shelters and cisterns, as the land threatens.",
        SettlementPriority.Expansion => "Settlers leave to found new villages on free land nearby.",
        SettlementPriority.Temple => "A temple to you: its worship raises half again as much power.",
    };

    /// <summary>A building's name.</summary>
    /// <param name="kind">The building.</param>
    /// <returns>For example "levee".</returns>
    public static string Name(BuildingKind kind) => kind switch
    {
        BuildingKind.Temple => "temple",
        BuildingKind.Granary => "granary",
        BuildingKind.Cistern => "cistern",
        BuildingKind.Firebreak => "firebreaks",
        BuildingKind.Shelter => "shelters",
        BuildingKind.Levee => "levees",
        BuildingKind.Hospital => "hospital",
        BuildingKind.Walls => "walls",
    };

    /// <summary>A field of technology's name.</summary>
    /// <param name="field">The field.</param>
    /// <returns>For example "agriculture".</returns>
    public static string Name(TechnologyField field) => field switch
    {
        TechnologyField.Agriculture => "agriculture",
        TechnologyField.Architecture => "architecture",
        TechnologyField.Medicine => "medicine",
        TechnologyField.Warfare => "warfare",
        TechnologyField.Navigation => "navigation",
        TechnologyField.Engineering => "engineering",
        TechnologyField.Science => "science",
        TechnologyField.Religion => "religion",
    };

    /// <summary>Why a priority could not be set, or that it was.</summary>
    /// <param name="rejection">The verdict.</param>
    /// <param name="priority">The priority asked for.</param>
    /// <param name="place">The settlement's name.</param>
    /// <returns>Status line.</returns>
    public static string Describe(PriorityRejection rejection, SettlementPriority priority, string place) => rejection switch
    {
        PriorityRejection.None => Format("{0} will {1}", place, Name(priority).ToLowerInvariant()),
        PriorityRejection.UnknownGod => "You are no god here",
        PriorityRejection.UnknownSettlement => "There is no one there to ask",
        PriorityRejection.NotAuthorized => "You may only speak for your own god",
        PriorityRejection.TooFewFollowers => Format("Too few in {0} follow you to set its course", place),
        PriorityRejection.TooSoon => Format("{0} changed its course only lately", place),
        PriorityRejection.Unchanged => Format("{0} already does that", place),
    };

    /// <summary>A share as a word, for the panel.</summary>
    /// <param name="share">The share (0..1).</param>
    /// <returns>For example "high".</returns>
    public static string Level(float share) => share switch
    {
        >= 0.8f => "very high",
        >= 0.6f => "high",
        >= 0.4f => "middling",
        >= 0.2f => "low",
        _ => "very low",
    };

    /// <summary>Invariant-culture string formatting.</summary>
    internal static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
