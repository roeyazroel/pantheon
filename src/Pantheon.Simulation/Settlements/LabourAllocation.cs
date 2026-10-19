using System;

namespace Pantheon.Simulation.Settlements;

/// <summary>How a settlement's workforce splits between the fields, the building site, the shrine and gathering in one step.</summary>
/// <param name="Workers">People who work: the population times the workforce share.</param>
/// <param name="Farmers">Workers in the fields.</param>
/// <param name="Builders">Workers on the house under construction.</param>
/// <param name="Worshippers">Workers who worship at the shrine instead of farming (ADR-009).</param>
/// <param name="Gatherers">Workers who gather wood and stone instead of farming (ADR-033).</param>
public readonly record struct LabourAllocation(float Workers, float Farmers, float Builders, float Worshippers = 0f, float Gatherers = 0f)
{
    /// <summary>Inhabitants who do not work: children, the old and those who keep house.</summary>
    /// <param name="population">Number of inhabitants.</param>
    /// <returns><c>population - Workers</c>, at least 0.</returns>
    public float NotWorking(int population) => Math.Max(0f, population - Workers);
}
