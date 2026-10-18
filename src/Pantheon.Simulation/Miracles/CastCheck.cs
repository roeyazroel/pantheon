using Pantheon.Domain.Miracles;

namespace Pantheon.Simulation.Miracles;

/// <summary>The authority's verdict on a cast request, with the numbers behind it.</summary>
/// <param name="Rejection">Why the cast is refused, or <see cref="CastRejection.None"/>.</param>
/// <param name="Miracle">The miracle, when the id is known.</param>
/// <param name="Influence">The god's influence at the target (0..1).</param>
/// <param name="Cost">Divine Power the cast costs there.</param>
/// <param name="CooldownTicksLeft">Ticks until the miracle is ready again; 0 when ready.</param>
public readonly record struct CastCheck(CastRejection Rejection, MiracleDefinition? Miracle, float Influence, float Cost, long CooldownTicksLeft)
{
    /// <summary>Whether the miracle may be cast.</summary>
    public bool IsAccepted => Rejection == CastRejection.None;
}
