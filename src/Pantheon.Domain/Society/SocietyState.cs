using System.Collections.Generic;

namespace Pantheon.Domain.Society;

/// <summary>
/// The life of a settlement beyond its food and houses (spec §29, §30, §36, ADR-023): how safe,
/// happy, rich, learned and settled its people are, what they know and have built, and what their
/// god has asked them to put first.
/// </summary>
public sealed class SocietyState
{
    /// <summary>How safe the people feel (0..1): disasters and war nearby lower it.</summary>
    public required float Safety { get; set; }

    /// <summary>How content the people are (0..1).</summary>
    public required float Happiness { get; set; }

    /// <summary>The goods the settlement has put by beyond its food, in abstract units; it pays for buildings.</summary>
    public required float Wealth { get; set; }

    /// <summary>How learned the people are (0..1); it drives their technology.</summary>
    public required float Education { get; set; }

    /// <summary>How settled the settlement's order is (0..1): unity of faith, contentment and peace raise it.</summary>
    public required float Stability { get; set; }

    /// <summary>What the people know, field by field.</summary>
    public required TechnologyLevels Technology { get; init; }

    /// <summary>What they have built, each kind once (a temple names its god).</summary>
    public required List<Building> Buildings { get; init; }

    /// <summary>What they put first.</summary>
    public required SettlementPriority Priority { get; set; }

    /// <summary>The god that set the priority, if one did.</summary>
    public required int? PriorityGodId { get; set; }

    /// <summary>Tick the priority was set (0 when never), so a god cannot change it every moment.</summary>
    public required long PrioritySinceTick { get; set; }

    /// <summary>The building under way, if any.</summary>
    public required BuildingKind? Project { get; set; }

    /// <summary>Work done on the building under way.</summary>
    public required float ProjectProgress { get; set; }

    /// <summary>Tick the settlement last sent settlers to found a village (0 when never).</summary>
    public required long LastFoundingTick { get; set; }

    /// <summary>The holy war the settlement wages, if any (spec §46, ADR-026).</summary>
    public Conflict.HolyWarState? HolyWar { get; set; }

    /// <summary>Tick its last holy war ended, so it rests before another.</summary>
    public long HolyWarEndedTick { get; set; }

    /// <summary>A newly founded settlement's society: safe, fairly content and settled, poor and unlearned.</summary>
    /// <returns>The society.</returns>
    public static SocietyState Founded() => new()
    {
        Safety = 1f,
        Happiness = 0.6f,
        Wealth = 0f,
        Education = 0.1f,
        Stability = 0.7f,
        Technology = TechnologyLevels.None(),
        Buildings = [],
        Priority = SettlementPriority.None,
        PriorityGodId = null,
        PrioritySinceTick = 0,
        Project = null,
        ProjectProgress = 0f,
        LastFoundingTick = 0,
    };
}
