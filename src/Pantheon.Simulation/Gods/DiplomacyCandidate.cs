using Pantheon.Domain.Diplomacy;

namespace Pantheon.Simulation.Gods;

/// <summary>A diplomatic move an AI god weighed towards another god, with its utility and reasons (spec §49).</summary>
/// <param name="Kind">The move.</param>
/// <param name="OtherGodId">The god it is towards.</param>
/// <param name="Utility">How much the god wants it; it acts above the threshold.</param>
/// <param name="Reason">The factors behind it.</param>
public readonly record struct DiplomacyCandidate(DiplomaticCommandKind Kind, int OtherGodId, float Utility, string Reason);
