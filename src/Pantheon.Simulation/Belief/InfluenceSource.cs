using Pantheon.Domain.World;

namespace Pantheon.Simulation.Belief;

/// <summary>A place from which a god's influence spreads: today, a settlement with followers.</summary>
/// <param name="Position">Centre of the source.</param>
/// <param name="Strength">Influence at the centre (0..1).</param>
/// <param name="Reach">Distance in metres at which the influence fades to nothing.</param>
public readonly record struct InfluenceSource(GroundPosition Position, float Strength, float Reach)
{
    /// <summary>Influence of this source at a distance: <see cref="Strength"/> fading linearly to 0 at <see cref="Reach"/>.</summary>
    /// <param name="distance">Distance in metres.</param>
    /// <returns>Influence (0..1).</returns>
    public float At(float distance) => Reach > 0f ? Strength * SimMath.Clamp01(1f - (distance / Reach)) : 0f;
}
