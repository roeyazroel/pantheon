namespace Pantheon.Game.Population;

/// <summary>
/// How much detail a settlement's population is shown with (ADR-004, ADR-008). The statistical
/// model runs for every settlement whatever its tier; higher tiers only add derived detail. The
/// Full tier (Tier 3) arrives in a later milestone.
/// </summary>
public enum SimulationTier
{
    /// <summary>Tier 1: aggregates only, nobody is drawn.</summary>
    Statistical,

    /// <summary>Tier 2: representative citizens, derived from the aggregates and never saved.</summary>
    Regional,
}
