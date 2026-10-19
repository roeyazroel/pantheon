namespace Pantheon.Domain.Disasters;

/// <summary>
/// The disasters of spec §19 beyond earthquakes, wildfires and floods (ADR-029), in
/// <c>disasters.json</c>'s <c>Hazards</c>: tsunamis, landslides, a volcano, tornadoes, meteors, crop
/// blight and locusts, each a process, and the marks they leave on the land (§64). Each is off when
/// its chance is zero. Rates are per simulated second.
/// </summary>
public sealed record HazardRules
{
    /// <summary>Waves from earthquakes near the coast.</summary>
    public TsunamiRules Tsunami { get; init; } = new();

    /// <summary>Slopes giving way.</summary>
    public LandslideRules Landslide { get; init; } = new();

    /// <summary>The island's volcano.</summary>
    public VolcanoRules Volcano { get; init; } = new();

    /// <summary>Whirlwinds from thunderstorms.</summary>
    public TornadoRules Tornado { get; init; } = new();

    /// <summary>Stones from the sky.</summary>
    public MeteorRules Meteor { get; init; } = new();

    /// <summary>Disease in the fields.</summary>
    public BlightRules Blight { get; init; } = new();

    /// <summary>Swarms of locusts.</summary>
    public LocustRules Locusts { get; init; } = new();

    /// <summary>Seconds the marks of quakes, slides and impacts stay on the land.</summary>
    public float MarkSeconds { get; init; } = 600f;

    /// <summary>No hazards beyond earthquakes, wildfires and floods.</summary>
    public static HazardRules None { get; } = new();
}

/// <summary>A wave raised by an earthquake near the coast.</summary>
public sealed record TsunamiRules
{
    /// <summary>Smallest magnitude that raises a wave; above every quake for none.</summary>
    public float MinMagnitude { get; init; } = 99f;

    /// <summary>How near the sea, in metres, the epicentre must be.</summary>
    public float CoastDistance { get; init; } = 200f;

    /// <summary>Metres inland the wave reaches per magnitude above the smallest, plus one.</summary>
    public float RadiusPerMagnitude { get; init; } = 150f;

    /// <summary>Metres above the sea the wave rises per magnitude above the smallest, plus one.</summary>
    public float HeightPerMagnitude { get; init; } = 3f;

    /// <summary>Share of a village's houses the wave destroys where it lies deepest.</summary>
    public float HousingLoss { get; init; }

    /// <summary>Share of a village's people the wave drowns where it lies deepest.</summary>
    public float Deaths { get; init; }
}

/// <summary>Slopes that give way when soaked or shaken, burying what lies below and blocking the way.</summary>
public sealed record LandslideRules
{
    /// <summary>Chance a second that a soaked, steep region slides; 0 for none.</summary>
    public float SlidePerSecond { get; init; }

    /// <summary>Metres a region must stand above its lowest neighbour to slide.</summary>
    public float MinDrop { get; init; } = 25f;

    /// <summary>Soil water from which a slope is soaked.</summary>
    public float WetSoil { get; init; } = 0.85f;

    /// <summary>Chance a second, per unit of shaking, that an earthquake brings a steep region down.</summary>
    public float ShakeChance { get; init; }

    /// <summary>Share of a buried village's houses the slide destroys.</summary>
    public float HousingLoss { get; init; }

    /// <summary>Share of a buried village's people the slide kills.</summary>
    public float Deaths { get; init; }

    /// <summary>Harm the slide does to the land's life.</summary>
    public float Damage { get; init; }

    /// <summary>Rubble that clears per second; half of it blocks the way (ADR-029).</summary>
    public float RubbleClearPerSecond { get; init; } = 0.005f;
}

/// <summary>The island's volcano: its highest peak, which rumbles before it erupts.</summary>
public sealed record VolcanoRules
{
    /// <summary>Eruptions an hour while it sleeps; 0 for a mountain that never wakes.</summary>
    public float EruptionsPerHour { get; init; }

    /// <summary>Metres the highest peak must rise to be a volcano.</summary>
    public float MinElevation { get; init; } = 60f;

    /// <summary>Seconds it rumbles before it erupts.</summary>
    public float RumbleSeconds { get; init; } = 30f;

    /// <summary>Seconds it erupts.</summary>
    public float EruptSeconds { get; init; } = 60f;

    /// <summary>Seconds between tremors while it rumbles.</summary>
    public float TremorSeconds { get; init; } = 8f;

    /// <summary>Magnitude of its tremors.</summary>
    public float TremorMagnitude { get; init; } = 4.5f;

    /// <summary>Regions down its steepest slope that its lava sets alight.</summary>
    public int LavaRegions { get; init; } = 6;

    /// <summary>Metres around the crater its ash falls on.</summary>
    public float AshRadius { get; init; } = 400f;

    /// <summary>Harm to the land's life per second of full ashfall.</summary>
    public float AshDamagePerSecond { get; init; }

    /// <summary>Health a village under full ashfall loses per second.</summary>
    public float AshHealthLossPerSecond { get; init; }

    /// <summary>Share of a village's stores full ashfall spoils per second.</summary>
    public float AshFoodLossPerSecond { get; init; }

    /// <summary>Seconds it sleeps after an eruption before it can wake.</summary>
    public float CooldownSeconds { get; init; } = 900f;
}

/// <summary>Whirlwinds that thunderstorms spin down onto warm land.</summary>
public sealed record TornadoRules
{
    /// <summary>Chance a second that a thunderstorm over warm land spins one down; 0 for none.</summary>
    public float PerStormPerSecond { get; init; }

    /// <summary>Warmth of the land (with the season) a tornado needs.</summary>
    public float MinTemperature { get; init; } = 0.55f;

    /// <summary>Metres around its path it destroys.</summary>
    public float Radius { get; init; } = 40f;

    /// <summary>Shortest life in seconds.</summary>
    public float MinSeconds { get; init; } = 20f;

    /// <summary>Longest life in seconds.</summary>
    public float MaxSeconds { get; init; } = 50f;

    /// <summary>Speed in metres per second.</summary>
    public float Speed { get; init; } = 12f;

    /// <summary>Share of a village's houses it tears down per second.</summary>
    public float HousingLossPerSecond { get; init; }

    /// <summary>Share of a village's people it kills per second.</summary>
    public float DeathsPerSecond { get; init; }

    /// <summary>Harm to the land's life per second.</summary>
    public float DamagePerSecond { get; init; }
}

/// <summary>Stones that fall from the sky, rarely.</summary>
public sealed record MeteorRules
{
    /// <summary>Impacts an hour; 0 for none.</summary>
    public float PerHour { get; init; }

    /// <summary>Metres around the impact it devastates.</summary>
    public float Radius { get; init; } = 150f;

    /// <summary>Magnitude of the quake it sets off.</summary>
    public float Magnitude { get; init; } = 5.5f;

    /// <summary>Share of a village's houses it destroys at the impact.</summary>
    public float HousingLoss { get; init; }

    /// <summary>Share of a village's people it kills at the impact.</summary>
    public float Deaths { get; init; }

    /// <summary>Scar it leaves on the land.</summary>
    public float Scar { get; init; }
}

/// <summary>Disease in the fields, spreading in warm, wet weather.</summary>
public sealed record BlightRules
{
    /// <summary>Chance a second that blight appears in a warm, wet field; 0 for none.</summary>
    public float EmergencePerSecond { get; init; }

    /// <summary>Warmth (with the season) blight needs.</summary>
    public float MinTemperature { get; init; } = 0.55f;

    /// <summary>Soil water blight needs.</summary>
    public float WetSoil { get; init; } = 0.65f;

    /// <summary>Share of the healthy crop blight takes per second where it thrives.</summary>
    public float GrowthPerSecond { get; init; }

    /// <summary>Chance a second, per unit of blight, that it spreads to a neighbouring field.</summary>
    public float SpreadPerSecond { get; init; }

    /// <summary>Share of blight that dies away per second where it does not thrive.</summary>
    public float DeclinePerSecond { get; init; }

    /// <summary>Share of blight a full blessing (Bless Crops) cures per second.</summary>
    public float BlessingCurePerSecond { get; init; }

    /// <summary>Mean blight of a village's fields from which it is told; it is told gone when it falls below half of this.</summary>
    public float Notice { get; init; } = 0.3f;
}

/// <summary>Locust swarms born in hot, dry weather, eating what they cross.</summary>
public sealed record LocustRules
{
    /// <summary>Swarms an hour while it is hot and dry; 0 for none.</summary>
    public float SwarmsPerHour { get; init; }

    /// <summary>Warmth (with the season) they need.</summary>
    public float MinTemperature { get; init; } = 0.6f;

    /// <summary>Soil water at most of the land they rise from.</summary>
    public float MaxSoil { get; init; } = 0.35f;

    /// <summary>Metres around the swarm it eats.</summary>
    public float Radius { get; init; } = 250f;

    /// <summary>Speed in metres per second, with the wind.</summary>
    public float Speed { get; init; } = 6f;

    /// <summary>Shortest life in seconds.</summary>
    public float MinSeconds { get; init; } = 90f;

    /// <summary>Longest life in seconds.</summary>
    public float MaxSeconds { get; init; } = 180f;

    /// <summary>Share of a village's stores a full swarm eats per second.</summary>
    public float FoodEatenPerSecond { get; init; }

    /// <summary>Share of a swarm that rain kills per second at full rain.</summary>
    public float RainLossPerSecond { get; init; }

    /// <summary>Harm to the land's life per second under a full swarm.</summary>
    public float DamagePerSecond { get; init; }
}
