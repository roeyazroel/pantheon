namespace Pantheon.Domain.Ecology;

/// <summary>
/// Data-driven tuning of the land's life (spec §18, ADR-028), loaded from <c>game/Data/ecology.json</c>:
/// wildlife and hunting, harm and its healing into scars, and pollution. Rates are per simulated
/// second; values without units are normalised to 0..1.
/// </summary>
public sealed record EcologyRules
{
    /// <summary>Ticks between ecology steps.</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Share of the gap to the land's capacity for wildlife that the animals close per second.</summary>
    public required float WildlifeGrowthPerSecond { get; init; }

    /// <summary>Wildlife a hundred people hunt per second from the land around their village.</summary>
    public required float HuntPerHundredPerSecond { get; init; }

    /// <summary>Food a unit of hunted wildlife gives.</summary>
    public required float FoodPerWildlife { get; init; }

    /// <summary>Wildlife a full fire kills per second.</summary>
    public required float FireWildlifeLossPerSecond { get; init; }

    /// <summary>Damage a full fire does per second.</summary>
    public required float FireDamagePerSecond { get; init; }

    /// <summary>Damage a full flood does per second.</summary>
    public required float FloodDamagePerSecond { get; init; }

    /// <summary>Damage an earthquake does at its epicentre, falling off with the shaking.</summary>
    public required float QuakeDamage { get; init; }

    /// <summary>Damage a smiting miracle (Lightning) does to the land where it falls.</summary>
    public required float HarmDamage { get; init; }

    /// <summary>Share of the healable damage (above the scar) that heals per second.</summary>
    public required float HealPerSecond { get; init; }

    /// <summary>Damage above which harm turns into a scar.</summary>
    public required float ScarThreshold { get; init; }

    /// <summary>Share of the damage above <see cref="ScarThreshold"/> that becomes scar per second.</summary>
    public required float ScarPerSecond { get; init; }

    /// <summary>Factor on healing in land that mostly follows a god of nature.</summary>
    public required float NatureHealFactor { get; init; }

    /// <summary>Scar a god of nature's land loses per second.</summary>
    public required float NatureScarHealPerSecond { get; init; }

    /// <summary>Scar, around a village, from which its people are told their land is scarred.</summary>
    public required float ScarNotice { get; init; }

    /// <summary>People a village holds before it pollutes.</summary>
    public required int PollutionFreePeople { get; init; }

    /// <summary>Pollution a thousand people beyond <see cref="PollutionFreePeople"/> add to their region per second.</summary>
    public required float PollutionPerThousandPerSecond { get; init; }

    /// <summary>Share of pollution that clears per second.</summary>
    public required float PollutionClearPerSecond { get; init; }

    /// <summary>An ecology that changes nothing, for content without <c>ecology.json</c>.</summary>
    public static EcologyRules Inert { get; } = new()
    {
        StepIntervalTicks = 10,
        WildlifeGrowthPerSecond = 0f,
        HuntPerHundredPerSecond = 0f,
        FoodPerWildlife = 0f,
        FireWildlifeLossPerSecond = 0f,
        FireDamagePerSecond = 0f,
        FloodDamagePerSecond = 0f,
        QuakeDamage = 0f,
        HarmDamage = 0f,
        HealPerSecond = 0f,
        ScarThreshold = 1f,
        ScarPerSecond = 0f,
        NatureHealFactor = 1f,
        NatureScarHealPerSecond = 0f,
        ScarNotice = 1f,
        PollutionFreePeople = 0,
        PollutionPerThousandPerSecond = 0f,
        PollutionClearPerSecond = 0f,
    };
}
