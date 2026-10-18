namespace Pantheon.Simulation.Weather;

/// <summary>The weather at one point of the ground.</summary>
/// <param name="Rain">Rain falling there (0..1).</param>
/// <param name="Wind">Wind there (0..1).</param>
/// <param name="Lightning">Lightning activity there (0..1).</param>
/// <param name="CellId">The cell that brings most of it, or <c>null</c> under a clear sky.</param>
public readonly record struct PointWeather(float Rain, float Wind, float Lightning, int? CellId);
