namespace Pantheon.Domain.Disasters;

/// <summary>What damaged a settlement.</summary>
public enum DisasterKind
{
    /// <summary>An earthquake's shaking.</summary>
    Earthquake,

    /// <summary>A wildfire.</summary>
    Fire,

    /// <summary>A flood: a river over its banks or a wave from the sea (ADR-022).</summary>
    Flood,

    /// <summary>A hurricane's winds (ADR-028).</summary>
    Hurricane,

    /// <summary>A wave from the sea after an earthquake (ADR-029).</summary>
    Tsunami,

    /// <summary>A slope giving way (ADR-029).</summary>
    Landslide,

    /// <summary>A volcano's lava and ash (ADR-029).</summary>
    Eruption,

    /// <summary>A tornado (ADR-029).</summary>
    Tornado,

    /// <summary>A meteor's impact (ADR-029).</summary>
    Meteor,

    /// <summary>The ancient beast ravaging a village (ADR-035).</summary>
    Beast,
}
