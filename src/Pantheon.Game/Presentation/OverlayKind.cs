namespace Pantheon.Game.Presentation;

/// <summary>Map overlay drawn over the terrain (spec §58).</summary>
public enum OverlayKind
{
    /// <summary>Each region in the colour of the god with the most influence there.</summary>
    Influence,

    /// <summary>Each settlement's lands in the colour of the god most of its people follow.</summary>
    Religion,

    /// <summary>Each settlement's lands, by its Faith in the local player's god.</summary>
    Faith,

    /// <summary>Each settlement's lands, by how well its stores feed it.</summary>
    Food,

    /// <summary>Each land region by the water in its soil: drought, enough or soaked (ADR-013).</summary>
    Weather,
}
