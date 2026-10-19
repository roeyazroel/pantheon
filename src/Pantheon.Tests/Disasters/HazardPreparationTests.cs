using System;
using System.Linq;
using Pantheon.Domain.Society;
using Pantheon.Game;
using Pantheon.Persistence;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Society;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>Sea walls, dams, irrigation and evacuation routes, and the hazards' content (spec §21, §64, ADR-029).</summary>
public sealed class HazardPreparationTests
{
    private static readonly SocietyRules Society = TestContent.Shipped.Society;

    [Fact]
    public void Irrigation_WatersDryFields_ForABetterHarvest()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Regions.Cells.ForEach(r => r.SoilMoisture = 0.05f);
        var village = world.Settlements[0];
        var dry = SettlementEconomy.Assess(SocietyWorlds.Civic, village, world.Regions).Production;

        HazardWorlds.Build(world, 0, BuildingKind.Irrigation);

        Assert.True(SettlementEconomy.Assess(SocietyWorlds.Civic, village, world.Regions).Production > dry * 1.2f);
    }

    [Fact]
    public void SeaWallsRiseOnlyByTheSea_AndDamsAndIrrigationOnlyBesideWater()
    {
        BuildingKind? Next(bool coast, bool river, SettlementPriority priority)
        {
            var world = DivineWorlds.Create(0f, 200f);
            if (coast)
            {
                HazardWorlds.Coast(world, 3);
            }

            if (river)
            {
                WaterWorlds.LayRiver(world, mouthColumn: 3);
            }

            var village = world.Settlements[0];
            village.Society.Priority = priority;
            foreach (var kind in new[] { BuildingKind.Levee, BuildingKind.Firebreak, BuildingKind.Shelter, BuildingKind.Cistern, BuildingKind.Evacuation, BuildingKind.Granary })
            {
                HazardWorlds.Build(world, 0, kind);
            }

            return SocietyModel.NextProject(Society, world, village)?.Kind;
        }

        Assert.Null(Next(coast: false, river: false, SettlementPriority.Preparation));
        Assert.Equal(BuildingKind.SeaWall, Next(coast: true, river: false, SettlementPriority.Preparation));
        Assert.Equal(BuildingKind.Dam, Next(coast: false, river: true, SettlementPriority.Preparation));
        Assert.Null(Next(coast: false, river: false, SettlementPriority.Food));
        Assert.Equal(BuildingKind.Irrigation, Next(coast: false, river: true, SettlementPriority.Food));
    }

    [Fact]
    public void TheShippedHazards_Parse_AndBrokenOnesAreRejected()
    {
        var hazards = TestContent.Shipped.Disasters.Hazards;
        Assert.Equal((6f, 0.5f, 0.3f, 1f, 600f), (hazards.Tsunami.MinMagnitude, hazards.Volcano.EruptionsPerHour, hazards.Meteor.PerHour, hazards.Locusts.SwarmsPerHour, hazards.MarkSeconds));
        Assert.Equal(0.5f, TestContent.Shipped.Disasters.Preparedness.EvacuationDeaths);
        Assert.Contains(Society.Buildings, b => b.Kind == BuildingKind.SeaWall && b.Priority == SettlementPriority.Preparation);

        AssertRejected("\"MaxSeconds\": 45.0", "\"MaxSeconds\": 5.0", "Hazards.Tornado.MaxSeconds");
        AssertRejected("\"LavaRegions\": 6", "\"LavaRegions\": 0", "Hazards.Volcano.LavaRegions");
        AssertRejected("\"Notice\": 0.15", "\"Notice\": 1.5", "Hazards.Blight.Notice");
        AssertRejected("\"MarkSeconds\": 600.0", "\"MarkSeconds\": 600.0, \"Comets\": 1", "Comets");
    }

    /// <summary>Breaks the shipped disasters file one way and checks the error names the member.</summary>
    private static void AssertRejected(string shipped, string broken, string expected)
    {
        var text = TestContent.ReadText(ContentJson.DisastersFileName);
        Assert.Contains(shipped, text, StringComparison.Ordinal);
        var files = GameContentFiles.Read(name => name == ContentJson.DisastersFileName ? text.Replace(shipped, broken, StringComparison.Ordinal) : TestContent.ReadText(name));

        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));

        Assert.Contains("disasters.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }
}
