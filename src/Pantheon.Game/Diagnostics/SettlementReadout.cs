using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Settlements;
using Pantheon.Game.Population;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Game.Diagnostics;

/// <summary>
/// Formats the debug overlay lines of one settlement: population, housing, health, food, jobs,
/// births and deaths, construction, the simulation tier and warnings. Rates are per simulated
/// second.
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
        lines.Add(FormatWater(snapshot, settlement, outlook, stepsPerSecond));
        lines.AddRange(SocietyReadout.Describe(settlement));
        if (snapshot.Style is { } styles)
        {
            lines.Add(Presentation.ReligiousStyles.Of(settlement, snapshot.World, styles) is { } style
                ? Format("  style: {0} {1:0.00} ({2})", style.Motif, style.Strength, style.Reason)
                : "  style: none (no god holds most of it, or its worship shows too little)");
        }

        lines.Add(FormatJobs(settlement, outlook));
        lines.Add(Format(
            "  births {0:0.00}/s   deaths {1:0.00}/s   {2}",
            outlook.ExpectedBirths * stepsPerSecond,
            outlook.ExpectedDeaths * stepsPerSecond,
            FormatConstruction(rules, settlement, outlook, houses)));
        lines.Add(FormatTier(FindCrowd(snapshot.Crowds, settlement.Id)));
        if (FindCrowd(snapshot.Crowds, settlement.Id)?.Navigation is { } navigation)
        {
            lines.Add(Format("  local nav: {0} cells, {1} blocked; {2} ways searched, {3} round something", navigation.Cells, navigation.Blocked, navigation.Searches, navigation.Detours));
        }
        if (FormatWarnings(outlook, houses) is { } warnings)
        {
            lines.Add(warnings);
        }
    }

    /// <summary>Number and name of a tier, as the overlay and the logs show it.</summary>
    /// <param name="tier">Tier.</param>
    /// <returns>For example "tier 2 regional".</returns>
    public static string DescribeTier(SimulationTier tier) => tier switch
    {
        SimulationTier.Statistical => "tier 1 statistical",
        SimulationTier.Regional => "tier 2 regional",
    };

    /// <summary>The status of a settlement's crowd, or <c>null</c> when the client reported none.</summary>
    /// <param name="crowds">Statuses of every settlement.</param>
    /// <param name="settlementId">Settlement.</param>
    /// <returns>Its status, if any.</returns>
    public static CrowdStatus? FindCrowd(IReadOnlyList<CrowdStatus> crowds, int settlementId)
    {
        foreach (var crowd in crowds)
        {
            if (crowd.SettlementId == settlementId)
            {
                return crowd;
            }
        }

        return null;
    }

    /// <summary>The simulation tier, the camera distance that changes it and, in Tier 2, the representatives.</summary>
    private static string FormatTier(CrowdStatus? crowd)
    {
        if (crowd is null)
        {
            return "  sim " + DescribeTier(SimulationTier.Statistical);
        }

        return crowd.Tier switch
        {
            SimulationTier.Statistical => Format(
                "  sim {0}: camera {1:0} m, citizens shown within {2:0} m",
                DescribeTier(crowd.Tier),
                crowd.CameraDistance,
                crowd.RegionalTierEnterDistance),
            SimulationTier.Regional => Format(
                "  sim {0}: {1} citizens shown, 1 per {2:0.0} people, camera {3:0} m, statistical beyond {4:0} m",
                DescribeTier(crowd.Tier),
                crowd.Representatives,
                crowd.PeoplePerRepresentative,
                crowd.CameraDistance,
                crowd.RegionalTierExitDistance),
        };
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

    /// <summary>Stored water against storage, how readily the people find it, and the river or flood beside them (ADR-022).</summary>
    private static string FormatWater(DebugSnapshot snapshot, SettlementState settlement, SettlementOutlook outlook, double stepsPerSecond)
    {
        var region = snapshot.World.Regions.GetRegion(settlement.RegionId);
        var river = Simulation.Water.WaterModel.RiverAt(snapshot.World.Water, settlement.RegionId);
        return Format(
            "  water {0:0.0} / {1:0}   access {2:0%}   drawn {3:0.00}/s   drunk {4:0.00}/s   fresh water {5:0.00}{6}{7}",
            settlement.Water,
            outlook.WaterStorage,
            outlook.WaterAccess,
            outlook.WaterDrawn * stepsPerSecond,
            outlook.WaterNeeded * stepsPerSecond,
            region.FreshWater,
            river is null ? string.Empty : Format("   on the {0} (level {1:0.00})", river.Name, river.Level),
            region.Flood > 0f ? Format("   FLOODED {0:0%}", region.Flood) : string.Empty);
    }

    /// <summary>The labour split and the farmland it works.</summary>
    private static string FormatJobs(SettlementState settlement, SettlementOutlook outlook) =>
        Format(
            "  jobs: {0:0} farming, {1:0} building, {2:0.0} worshipping, {3:0} not working   farmland {4} regions, fertility {5:0.00}, blessing {6:0.00}",
            outlook.Labour.Farmers,
            outlook.Labour.Builders,
            outlook.Labour.Worshippers,
            outlook.Labour.NotWorking(settlement.Population),
            outlook.Land.LandRegions,
            outlook.Land.MeanFertility,
            outlook.Land.MeanBlessing);

    /// <summary>The house under way, or why none is wanted.</summary>
    private static string FormatConstruction(SettlementRules rules, SettlementState settlement, SettlementOutlook outlook, int houses)
    {
        if (outlook.ConstructionWanted)
        {
            return Format("building house {0}: {1:0%}", houses + 1, settlement.ConstructionProgress / SettlementEconomy.ComputeHouseWork(rules, settlement));
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

        if (outlook.IsThirsty)
        {
            warnings.Add(Format("! thirsty: {0:0%} watered", outlook.WateredShare));
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
