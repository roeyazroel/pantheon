using Pantheon.Domain.World;

namespace Pantheon.Simulation.Gods;

/// <summary>What an AI god could do in one strategic evaluation.</summary>
public enum AiActionKind
{
    /// <summary>Do nothing and save Divine Power.</summary>
    Wait,

    /// <summary>Cast a miracle on a settlement.</summary>
    CastMiracle,

    /// <summary>Send its Creature to a settlement.</summary>
    SendCreature,
}

/// <summary>
/// One action an AI god weighed, with its utility and the reasons for it (spec §48–49: utility AI
/// with explainable scores).
/// </summary>
/// <param name="Kind">The kind of action.</param>
/// <param name="MiracleId">The miracle, for a cast.</param>
/// <param name="SettlementId">The settlement it targets, if any.</param>
/// <param name="Target">Where it acts.</param>
/// <param name="Value">What the god expects to gain, before cost.</param>
/// <param name="Cost">Divine Power it costs.</param>
/// <param name="Utility">Value after the cost of spending, which the god compares.</param>
/// <param name="Reason">The factors behind the value, for the debug view and the player.</param>
public readonly record struct AiCandidate(AiActionKind Kind, string? MiracleId, int? SettlementId, GroundPosition Target, float Value, float Cost, float Utility, string Reason);
