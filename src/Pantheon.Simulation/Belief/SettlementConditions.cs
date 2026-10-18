namespace Pantheon.Simulation.Belief;

/// <summary>
/// How a settlement's people are faring, as the belief model sees it. Hard times breed doubt among
/// followers and wear down their love; good times let love grow.
/// </summary>
/// <param name="Hardship">0 (no hardship) to 1: the worst of ill health, hunger and the worry a drought brings.</param>
/// <param name="Comfort">0 to 1: health times food security.</param>
/// <param name="FoodNeed">0 (stores are full) to 1 (stores are empty).</param>
/// <param name="HealthNeed">0 (healthy) to 1 (dying).</param>
/// <param name="WaterNeed">0 (well-watered farmland) to 1 (parched): the drought.</param>
public readonly record struct SettlementConditions(float Hardship, float Comfort, float FoodNeed, float HealthNeed, float WaterNeed);
