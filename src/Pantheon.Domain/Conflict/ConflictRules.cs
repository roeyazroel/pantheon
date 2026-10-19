namespace Pantheon.Domain.Conflict;

/// <summary>Tuning of war goals, intelligence, proxy wars and holy wars (spec §43–46, ADR-026), in <c>diplomacy.json</c>'s <c>Conflict</c>.</summary>
public sealed record ConflictRules
{
    /// <summary>War score for a settlement won from the enemy.</summary>
    public required float ConversionScore { get; init; }

    /// <summary>War score for a strike on the enemy's followers: little, so destruction is not the best road (§44).</summary>
    public required float HarmScore { get; init; }

    /// <summary>War score for defeating the enemy's Creature.</summary>
    public required float CreatureScore { get; init; }

    /// <summary>War score for taking a holy site the enemy kept.</summary>
    public required float SiteScore { get; init; }

    /// <summary>War score for migrants the enemy's people sent to the god's lands.</summary>
    public required float RefugeeScore { get; init; }

    /// <summary>War score for a holy war's raid on the enemy's people.</summary>
    public required float RaidScore { get; init; }

    /// <summary>War score a goal needs, and the lead over the enemy's score, before peace can be dictated.</summary>
    public required float GoalScore { get; init; }

    /// <summary>Divine Power a minute a tribute dictated in peace pays, and how many seconds.</summary>
    public required float DictatedTribute { get; init; }

    /// <summary>Seconds a dictated tribute or territorial agreement lasts.</summary>
    public required float DictatedSeconds { get; init; }

    /// <summary>Share of the loser's followers in a ceded village who turn to the winner.</summary>
    public required float CededShare { get; init; }

    /// <summary>Intelligence an ally shares.</summary>
    public required float AllyIntel { get; init; }

    /// <summary>Intelligence any treaty brings.</summary>
    public required float TreatyIntel { get; init; }

    /// <summary>Intelligence per share of followers among the other god's people (spies and defectors).</summary>
    public required float PresenceIntel { get; init; }

    /// <summary>Intelligence a Creature brings from a village it was in lately.</summary>
    public required float ScoutIntel { get; init; }

    /// <summary>Seconds a Creature's visit counts as scouting.</summary>
    public required float ScoutSeconds { get; init; }

    /// <summary>Intelligence a Knowledge god has of everyone.</summary>
    public required float KnowledgeIntel { get; init; }

    /// <summary>Factor on the cost of a veiled miracle.</summary>
    public required float VeilCostFactor { get; init; }

    /// <summary>Chance a veiled act is exposed, at full intelligence of the god it hurt.</summary>
    public required float ExposureChance { get; init; }

    /// <summary>Doubt a veiled strike plants in the faith of the people's own god: why did it not protect them?</summary>
    public required float VeiledDoubt { get; init; }

    /// <summary>Divine Power inciting costs.</summary>
    public required float IncitePower { get; init; }

    /// <summary>Resentment inciting stirs.</summary>
    public required float InciteResentment { get; init; }

    /// <summary>Divine Power propaganda costs.</summary>
    public required float PropagandaPower { get; init; }

    /// <summary>Doubt propaganda plants.</summary>
    public required float PropagandaDoubt { get; init; }

    /// <summary>Influence a proxy needs where it acts.</summary>
    public required float ProxyInfluence { get; init; }

    /// <summary>Seconds a god must wait between proxy acts.</summary>
    public required float ProxyCooldownSeconds { get; init; }

    /// <summary>Militarism a settlement needs to wage holy war.</summary>
    public required float HolyWarMilitarism { get; init; }

    /// <summary>Chance a second that a settlement ready for holy war declares one.</summary>
    public required float HolyWarPerSecond { get; init; }

    /// <summary>Seconds a holy war lasts at most (half when discouraged).</summary>
    public required float HolyWarSeconds { get; init; }

    /// <summary>Seconds a settlement rests after a holy war.</summary>
    public required float HolyWarCooldownSeconds { get; init; }

    /// <summary>Share of the attacked people killed a second at even strength.</summary>
    public required float RaidDeathsPerSecond { get; init; }

    /// <summary>Share of the attacked god's followers converted a second at even strength.</summary>
    public required float RaidConversionPerSecond { get; init; }

    /// <summary>Love the attackers lose for their god when it discourages them (mortals are not puppets).</summary>
    public required float DiscourageLoveLoss { get; init; }
}
