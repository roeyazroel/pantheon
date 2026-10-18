using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Settlements;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Game.Diagnostics;

/// <summary>
/// Formats the debug overlay lines of one settlement: population, housing, health, food, jobs,
/// births and deaths, construction and warnings. Rates are per simulated second.
/// </summary>
internal static class SettlementReadout
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Adds the lines of one settlement.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data, for the region grid, the rules and the tick rate.</param>
    /// <param name="settlement">Settlement to describe.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot, SettlementState settlement)
    {
        var rules = snapshot.SettlementRules;
        var outlook = SettlementEconomy.Assess(rules, settlement, snapshot.World.Regions);
        var stepsPerSecond = snapshot.TicksPerSecond / rules.StepIntervalTicks;
        var houses = SettlementEconomy.ComputeHouseCount(rules, settlement.Housing);

        lines.Add(Format("Settlement {0} (region #{1})", settlement.Name, settlement.RegionId));
        lines.Add(Format(
            "  population {0} / {1}   {2} houses   health {3:0%}",
            settlement.Population,
            settlement.Housing,
            houses,
            settlement.Health));
        lines.Add(FormatFood(settlement, outlook, stepsPerSecond));
        lines.Add(FormatJobs(settlement, outlook));
        lines.Add(Format(
            "  births {0:0.00}/s   deaths {1:0.00}/s   {2}",
            outlook.ExpectedBirths * stepsPerSecond,
            outlook.ExpectedDeaths * stepsPerSecond,
            FormatConstruction(rules, settlement, outlook, houses)));
        if (FormatWarnings(outlook, houses) is { } warnings)
        {
            lines.Add(warnings);
        }
    }

    /// <summary>Stored food against storage, and the step's harvest, meals and net change.</summary>
    private static string FormatFood(SettlementState settlement, SettlementOutlook outlook, double stepsPerSecond) =>
        Format(
            "  food {0:0.0} / {1:0}   net {2:+0.00;-0.00;0.00}/s   harvest {3:0.00}/s   eaten {4:0.00}/s",
            settlement.Food,
            outlook.Storage,
            outlook.NetFood * stepsPerSecond,
            outlook.Production * stepsPerSecond,
            outlook.Consumption * stepsPerSecond);

    /// <summary>The labour split and the farmland it works.</summary>
    private static string FormatJobs(SettlementState settlement, SettlementOutlook outlook) =>
        Format(
            "  jobs: {0:0} farming, {1:0} building, {2:0} not working   farmland {3} regions, fertility {4:0.00}",
            outlook.Labour.Farmers,
            outlook.Labour.Builders,
            outlook.Labour.NotWorking(settlement.Population),
            outlook.Land.LandRegions,
            outlook.Land.MeanFertility);

    /// <summary>The house under way, or why none is wanted.</summary>
    private static string FormatConstruction(SettlementRules rules, SettlementState settlement, SettlementOutlook outlook, int houses)
    {
        if (outlook.ConstructionWanted)
        {
            return Format("building house {0}: {1:0%}", houses + 1, settlement.ConstructionProgress / rules.WorkPerHouse);
        }

        if (settlement.Housing >= rules.MaxHousing)
        {
            return Format("no more houses: housing is at its cap of {0}", rules.MaxHousing);
        }

        var crowding = settlement.Housing > 0 ? (float)settlement.Population / settlement.Housing : 0f;
        return Format("no house needed: {0:0%} full, builds from {1:0%}", crowding, rules.ConstructionStartCrowding);
    }

    /// <summary>One line of warnings, or <c>null</c> when nothing is wrong.</summary>
    private static string? FormatWarnings(SettlementOutlook outlook, int houses)
    {
        var warnings = new List<string>();
        if (outlook.IsStarving)
        {
            warnings.Add(Format("! starving: {0:0%} fed", outlook.FedShare));
        }

        if (DescribeBirthBlocker(outlook.BirthBlocker) is { } blocker)
        {
            warnings.Add("! no births: " + blocker);
        }

        if (outlook.ConstructionWaitsForFood)
        {
            warnings.Add(Format("! house {0} waits for food", houses + 1));
        }

        return warnings.Count > 0 ? "  " + string.Join("   ", warnings) : null;
    }

    /// <summary>Why births are blocked, or <c>null</c> when they are not.</summary>
    private static string? DescribeBirthBlocker(BirthBlocker blocker) => blocker switch
    {
        BirthBlocker.None => null,
        BirthBlocker.NoPeople => "nobody left",
        BirthBlocker.NoFreeHousing => "houses full",
        BirthBlocker.NoFoodSurplus => "no food surplus",
    };

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(Invariant, format, args);
}
