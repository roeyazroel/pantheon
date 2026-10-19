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
}
