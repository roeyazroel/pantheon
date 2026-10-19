namespace Pantheon.Domain.Society;

/// <summary>What a settlement may build beyond its houses (spec §9, §21, §32, ADR-023).</summary>
public enum BuildingKind
{
    /// <summary>A temple to one god: its worship raises more power and spreads its influence.</summary>
    Temple,

    /// <summary>A granary: more food kept, and less lost to disasters.</summary>
    Granary,

    /// <summary>A cistern: more water kept.</summary>
    Cistern,

    /// <summary>Firebreaks around the village: fires burn it less.</summary>
    Firebreak,

    /// <summary>Shelters: fewer die in earthquakes, floods and storms.</summary>
    Shelter,

    /// <summary>Levees along the water: floods drown it less.</summary>
    Levee,

    /// <summary>A hospital: the sick recover faster and fewer die.</summary>
    Hospital,

    /// <summary>Walls: attacks harm it less, and its people feel safer.</summary>
    Walls,
}
