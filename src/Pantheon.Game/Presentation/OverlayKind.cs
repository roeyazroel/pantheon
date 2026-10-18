namespace Pantheon.Game.Presentation;

/// <summary>Map overlay drawn over the terrain (spec §58).</summary>
public enum OverlayKind
{
    /// <summary>The local player's god's influence.</summary>
    Influence,

    /// <summary>Each settlement's lands, by its Faith in the local player's god.</summary>
    Faith,

    /// <summary>Each settlement's lands, by how well its stores feed it.</summary>
    Food,
}
