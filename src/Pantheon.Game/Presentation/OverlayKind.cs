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

    /// <summary>Fires burning, scorched land and shielded settlements (ADR-014).</summary>
    Danger,

    /// <summary>The pressure on each settlement's people to leave, and the routes of people on the move (ADR-016).</summary>
    Migration,

    /// <summary>Each settlement's lands by the doctrine it holds most, grey where it has turned from the gods (ADR-024).</summary>
    Culture,

    /// <summary>The land's life, its scars and its snow (ADR-028).</summary>
    Nature,

    /// <summary>Each village's people, and people on the road (spec §58, ADR-031).</summary>
    Population,

    /// <summary>The land's risk of disaster: fire, flood, landslide and the volcano (spec §58, ADR-031).</summary>
    Risk,

    /// <summary>Political control: the bloc that holds each village (spec §58, ADR-031).</summary>
    Politics,
}
