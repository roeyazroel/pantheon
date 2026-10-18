using Pantheon.Domain.World;

namespace Pantheon.Game.Alerts;

/// <summary>A message for the player about something that happened in the world (spec §59).</summary>
/// <param name="Text">What happened, in one sentence.</param>
/// <param name="Severity">How urgent it is.</param>
/// <param name="Location">Where it happened, for the camera to go there; <c>null</c> when it has no place.</param>
/// <param name="SimulationSeconds">Simulated time at which it was raised.</param>
public sealed record Alert(string Text, AlertSeverity Severity, GroundPosition? Location, double SimulationSeconds);
