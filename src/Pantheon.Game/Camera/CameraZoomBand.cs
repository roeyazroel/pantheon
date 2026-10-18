namespace Pantheon.Game.Camera;

/// <summary>Zoom band of the god camera. Later milestones switch detail and controls per band.</summary>
public enum CameraZoomBand
{
    /// <summary>Close to the ground: individual buildings and, later, villagers and the creature.</summary>
    Close,

    /// <summary>A settlement and its surroundings.</summary>
    Regional,

    /// <summary>The whole island.</summary>
    Strategic,
}
