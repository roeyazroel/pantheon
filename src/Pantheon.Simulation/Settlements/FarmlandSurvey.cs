namespace Pantheon.Simulation.Settlements;

/// <summary>The land a settlement farms: the farmable regions around it, their mean fertility and their mean blessing.</summary>
/// <param name="LandRegions">Farmable regions (not sea, fertility above 0) within the farmland radius.</param>
/// <param name="MeanFertility">Mean fertility of those regions (0..1); 0 when there are none.</param>
/// <param name="MeanBlessing">Mean divine blessing of those regions (0..1); 0 when there are none.</param>
public readonly record struct FarmlandSurvey(int LandRegions, float MeanFertility, float MeanBlessing = 0f);
