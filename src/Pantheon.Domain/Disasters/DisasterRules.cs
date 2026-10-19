namespace Pantheon.Domain.Disasters;

/// <summary>
/// Data-driven tuning of wildfires and earthquakes, loaded from <c>game/Data/disasters.json</c>
/// (ADR-014). Rates are per simulated second; values without units are normalised to 0..1.
/// </summary>
public sealed record DisasterRules
{
    /// <summary>Ticks between disaster steps.</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Chance per second, per unit of lightning, coverage and flammability, that a thunderstorm sets a region alight.</summary>
    public required float LightningIgnitionPerSecond { get; init; }

    /// <summary>Chance, per unit of the struck region's flammability, that a god's bolt of lightning sets it alight (spec §15, ADR-041); never when absent.</summary>
    public float LightningMiracleIgnition { get; init; }

    /// <summary>Chance per second that a spark from the hearths of a settlement of 100 people sets one of the regions around it alight, per unit of that region's flammability.</summary>
    public required float HearthFirePerSecond { get; init; }

    /// <summary>How fiercely a region burns per unit of flammability: a fire grows towards <c>min(1, FuelHeat × flammability)</c>.</summary>
    public required float FuelHeat { get; init; }

    /// <summary>Fire intensity of a newly lit region.</summary>
    public required float IgnitionIntensity { get; init; }

    /// <summary>Flammability below which a region cannot catch fire.</summary>
    public required float MinFlammability { get; init; }

    /// <summary>Soil moisture at and above which land does not burn at all; drier land burns more readily.</summary>
    public required float WetSoil { get; init; }

    /// <summary>Share of the gap to what its fuel allows a fire grows (or dies down) per second.</summary>
    public required float FireGrowthPerSecond { get; init; }

    /// <summary>Scorch a full fire leaves per second: it eats the land's fuel.</summary>
    public required float BurnPerSecond { get; init; }

    /// <summary>Heat a full fire sends to each neighbouring region per second; a region catches fire at a heat of 1.</summary>
    public required float SpreadPerSecond { get; init; }

    /// <summary>Extra spread downwind per unit of wind: heat times <c>1 + WindSpread × wind × alignment</c>.</summary>
    public required float WindSpread { get; init; }

    /// <summary>Fire intensity full rain puts out per second.</summary>
    public required float RainQuenchPerSecond { get; init; }

    /// <summary>Fire intensity a settlement of 100 people puts out per second in its own region (firefighting).</summary>
    public required float FirefightingPerSecond { get; init; }

    /// <summary>Share of the scorch that fades per second as the land regrows.</summary>
    public required float ScorchRecoveryPerSecond { get; init; }

    /// <summary>Share of a settlement's houses a full fire in its region burns per second, before their fire resistance.</summary>
    public required float FireHousingLossPerSecond { get; init; }

    /// <summary>Share of a settlement's people a full fire in its region kills per second.</summary>
    public required float FireDeathsPerSecond { get; init; }

    /// <summary>Share of a settlement's stores a full fire in its region burns per second.</summary>
    public required float FireFoodLossPerSecond { get; init; }

    /// <summary>How much a second of full fire in a settlement's region shakes its belief, as a share of a full earthquake.</summary>
    public required float FireDreadPerSecond { get; init; }

    /// <summary>Natural earthquakes per simulated hour.</summary>
    public required float EarthquakesPerHour { get; init; }

    /// <summary>Smallest magnitude of a natural earthquake.</summary>
    public required float MinMagnitude { get; init; }

    /// <summary>Largest magnitude of a natural earthquake.</summary>
    public required float MaxMagnitude { get; init; }

    /// <summary>Magnitude at which the shaking is strongest (intensity 1); weaker ones shake less.</summary>
    public required float FullMagnitude { get; init; }

    /// <summary>Magnitude below which the ground does not shake enough to damage anything.</summary>
    public required float HarmlessMagnitude { get; init; }

    /// <summary>Radius of the shaking per magnitude above the harmless one, in metres.</summary>
    public required float RadiusPerMagnitude { get; init; }

    /// <summary>How long the ground shakes, in simulated seconds.</summary>
    public required float ShakingSeconds { get; init; }

    /// <summary>Share of a settlement's houses full shaking destroys over the whole quake, before their resistance.</summary>
    public required float QuakeHousingLoss { get; init; }

    /// <summary>Share of a settlement's people full shaking kills over the whole quake.</summary>
    public required float QuakeDeaths { get; init; }

    /// <summary>Share of a settlement's stores full shaking spoils over the whole quake.</summary>
    public required float QuakeFoodLoss { get; init; }

    /// <summary>Chance that full shaking sets a settlement's region alight.</summary>
    public required float QuakeFireChance { get; init; }

    /// <summary>How well houses stand up to shaking (spec §94: EarthquakeResistance): the share of the damage they escape.</summary>
    public required float HouseQuakeResistance { get; init; }

    /// <summary>How well houses stand up to fire (spec §94: FireResistance): the share of the damage they escape.</summary>
    public required float HouseFireResistance { get; init; }

    /// <summary>How well houses stand up to floods (spec §94: FloodResistance, ADR-048): the share of the damage they escape; none when absent.</summary>
    public float HouseFloodResistance { get; init; }

    /// <summary>How disasters crack the houses left standing and villages mend them (spec §94: StructuralIntegrity, ADR-048); none when absent.</summary>
    public StructureRules Structure { get; init; } = StructureRules.None;

    /// <summary>Share of disaster damage a shielded settlement still takes.</summary>
    public required float ShieldedDamage { get; init; }

    /// <summary>How a settlement's preparations and technology lessen disasters (spec §21, ADR-023).</summary>
    public required Preparedness Preparedness { get; init; }

    /// <summary>Tsunamis, landslides, the volcano, tornadoes, meteors, blight and locusts (ADR-029); none when absent.</summary>
    public HazardRules Hazards { get; init; } = HazardRules.None;

    /// <summary>The plague, eclipses, reformations and the world's other great events (ADR-034); none when absent.</summary>
    public GreatEvents.GreatEventRules GreatEvents { get; init; } = Pantheon.Domain.GreatEvents.GreatEventRules.None;
}

/// <summary>How a settlement's buildings and technology lessen what disasters do to it (spec §21, §36, ADR-023).</summary>
public sealed record Preparedness
{
    /// <summary>Share of the houses a disaster would destroy saved per level of architecture.</summary>
    public required float ArchitecturePerLevel { get; init; }

    /// <summary>Share of flood and fire damage saved per level of engineering.</summary>
    public required float EngineeringPerLevel { get; init; }

    /// <summary>Share of the deaths in earthquakes and floods that shelters prevent.</summary>
    public required float ShelterDeaths { get; init; }

    /// <summary>Share of a fire's damage to the settlement that firebreaks prevent.</summary>
    public required float FirebreakDamage { get; init; }

    /// <summary>Share of a flood's damage to the settlement that levees prevent.</summary>
    public required float LeveeDamage { get; init; }

    /// <summary>Share of the food a disaster would spoil that a granary keeps.</summary>
    public required float GranaryFood { get; init; }

    /// <summary>Share of a tsunami's or a hurricane's damage that a sea wall prevents (ADR-029).</summary>
    public float SeaWallDamage { get; init; }

    /// <summary>Share of a flood's damage that a dam upstream prevents, beside a levee's (ADR-029).</summary>
    public float DamDamage { get; init; }

    /// <summary>Share of the deaths in every disaster that evacuation routes prevent (ADR-029).</summary>
    public float EvacuationDeaths { get; init; }

    /// <summary>Share of the ancient beast's harm that walls prevent (ADR-035).</summary>
    public float BeastWalls { get; init; }

    /// <summary>No preparedness at all.</summary>
    public static Preparedness None { get; } = new()
    {
        ArchitecturePerLevel = 0f, EngineeringPerLevel = 0f, ShelterDeaths = 0f, FirebreakDamage = 0f, LeveeDamage = 0f, GranaryFood = 0f,
    };
}

/// <summary>
/// The structural integrity of a village's houses (spec §94, ADR-048): every disaster that reaches it
/// cracks the houses it leaves standing, cracked houses fall more easily in the next, and the village
/// mends them over time. <see cref="None"/> leaves houses whole.
/// </summary>
public sealed record StructureRules
{
    /// <summary>Houses never crack.</summary>
    public static StructureRules None { get; } = new();

    /// <summary>How much integrity a disaster takes, per share of the houses it would destroy.</summary>
    public float CrackPerShare { get; init; }

    /// <summary>How much more easily houses fall at no integrity: the damage is 1 + this times the integrity lost.</summary>
    public float Weakness { get; init; }

    /// <summary>Integrity mended per second.</summary>
    public float RepairPerSecond { get; init; }
}
