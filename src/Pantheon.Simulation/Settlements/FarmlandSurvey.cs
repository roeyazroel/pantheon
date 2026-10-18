namespace Pantheon.Simulation.Settlements;

/// <summary>The land a settlement farms: the farmable regions around it and their mean fertility.</summary>
/// <param name="LandRegions">Farmable regions (not sea, fertility above 0) within the farmland radius.</param>
/// <param name="MeanFertility">Mean fertility of those regions (0..1); 0 when there are none.</param>
public readonly record struct FarmlandSurvey(int LandRegions, float MeanFertility);
