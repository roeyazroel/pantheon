namespace Pantheon.Simulation.Settlements;

/// <summary>The land a settlement farms: the farmable regions around it, their mean fertility, blessing and soil moisture.</summary>
/// <param name="LandRegions">Farmable regions (not sea, fertility above 0) within the farmland radius.</param>
/// <param name="MeanFertility">Mean fertility of those regions (0..1); 0 when there are none.</param>
/// <param name="MeanBlessing">Mean divine blessing of those regions (0..1); 0 when there are none.</param>
/// <param name="MeanSoilMoisture">Mean water in their soil (0..1); 1 when there are none, so no land means no drought.</param>
/// <param name="MeanScorch">Mean scorch of their land (0..1) that wildfires left (ADR-014).</param>
public readonly record struct FarmlandSurvey(int LandRegions, float MeanFertility, float MeanBlessing = 0f, float MeanSoilMoisture = 1f, float MeanScorch = 0f);
