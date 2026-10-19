namespace Pantheon.Domain.Society;

/// <summary>What a settlement's people put first, as a god asks them (spec §29, ADR-023).</summary>
public enum SettlementPriority
{
    /// <summary>Nothing in particular: the settlement runs itself.</summary>
    None,

    /// <summary>Increase food: more hands in the fields, and a granary.</summary>
    Food,

    /// <summary>Improve health: healers, and a hospital.</summary>
    Health,

    /// <summary>Grow the population: more births while there is food and room.</summary>
    Growth,

    /// <summary>Focus on religion: more of the day spent worshipping.</summary>
    Worship,

    /// <summary>Prepare for war: watchmen, and walls.</summary>
    Defence,

    /// <summary>Prepare for disaster: granaries, cisterns, firebreaks, shelters and levees, as the land threatens.</summary>
    Preparation,

    /// <summary>Expand the territory: settlers leave to found new villages.</summary>
    Expansion,

    /// <summary>Build a temple to the god that asks.</summary>
    Temple,
}
