namespace Pantheon.Domain.World;

/// <summary>
/// Serializable state of the simulation's xoshiro256** random number generator. Persisting it in
/// <see cref="WorldState"/> makes a loaded save continue exactly the same random sequence.
/// </summary>
/// <param name="S0">State word 0.</param>
/// <param name="S1">State word 1.</param>
/// <param name="S2">State word 2.</param>
/// <param name="S3">State word 3.</param>
public readonly record struct RandomState(ulong S0, ulong S1, ulong S2, ulong S3);
