namespace Pantheon.Domain.Settlements;

/// <summary>
/// Data-driven tuning of the statistical settlement model, loaded from
/// <c>game/Data/settlement_rules.json</c>. All rates are per simulation tick.
/// </summary>
public sealed record SettlementRules
{
    /// <summary>Share of the population working the fields (0..1).</summary>
    public required float WorkforceShare { get; init; }

    /// <summary>Food produced per worker per tick on land with fertility 1.</summary>
    public required float FoodPerWorkerPerTick { get; init; }

    /// <summary>Food eaten per person per tick.</summary>
    public required float FoodPerPersonPerTick { get; init; }

    /// <summary>Food storage capacity per unit of housing.</summary>
    public required float FoodStoragePerHousing { get; init; }

    /// <summary>Stored food per person required before births occur.</summary>
    public required float SurplusFoodPerPerson { get; init; }

    /// <summary>Chance per person per tick of a birth while food is in surplus and housing is free.</summary>
    public required double BirthChancePerPersonPerTick { get; init; }

    /// <summary>Baseline chance per person per tick of a death.</summary>
    public required double DeathChancePerPersonPerTick { get; init; }

    /// <summary>Additional chance per person per tick of a death while the food store is empty.</summary>
    public required double StarvationDeathChancePerPersonPerTick { get; init; }
}
