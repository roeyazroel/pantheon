namespace Pantheon.Game.Launch;

/// <summary>A disaster the <c>--disaster</c> launch option starts at the focused settlement (ADR-014).</summary>
public enum DisasterAtStart
{
    /// <summary>An earthquake under it.</summary>
    Quake,

    /// <summary>A fire in its region.</summary>
    Fire,

    /// <summary>The nearest river bursts its banks, or a wave floods the land (ADR-022).</summary>
    Flood,

    /// <summary>A tornado at it (ADR-029).</summary>
    Tornado,

    /// <summary>A landslide in its region (ADR-029).</summary>
    Landslide,

    /// <summary>A meteor striking it (ADR-029).</summary>
    Meteor,

    /// <summary>Blight in its fields (ADR-029).</summary>
    Blight,

    /// <summary>Locusts over it (ADR-029).</summary>
    Locusts,

    /// <summary>A wave on the nearest coast (ADR-029).</summary>
    Tsunami,

    /// <summary>The volcano wakes (ADR-029).</summary>
    Eruption,
}
