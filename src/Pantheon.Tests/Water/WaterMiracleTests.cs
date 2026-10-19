using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Domains;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Water;

/// <summary>The Water miracle, the Ocean and Justice domains, Tidal Wave and Judgement (spec §12, §110, ADR-022).</summary>
public sealed class WaterMiracleTests
{
    private static DomainRules Domains => TestContent.Shipped.Domains;

    [Fact]
    public void Water_FillsTheWellsUpToTheirStorage_AndSoaksTheFields()
    {
        var (world, caster) = Create(domain: null);
        var village = world.Settlements[0];
        village.Water = 0f;
        world.Regions.Cells[village.RegionId].SoilMoisture = 0.05f;

        Cast(world, caster, "water", village.Position);

        var storage = SettlementEconomy.ComputeWaterStorage(SettlementWorlds.Rules, village.Housing);
        Assert.Equal(System.Math.Min(storage, 200f), village.Water, 3);
        Assert.Equal(0.6f, world.Regions.Cells[village.RegionId].SoilMoisture, 3);
    }

    [Fact]
    public void TidalWave_FloodsTheLowCoast_AndIsRaisedAsAGodsFlood_ButFindsNothingOnHighGround()
    {
        var (world, caster) = Create(GodDomain.Ocean);
        var village = world.Settlements[0];
        world.Regions.Cells[village.RegionId].Elevation = 2f;
        var events = new List<WorldEvent>();

        var wave = caster.Cast(world, new CastMiracleRequest(1, "tidal_wave", village.Position), 1, events);
        foreach (var region in world.Regions.Cells)
        {
            region.Elevation = 50f;
        }

        world.Gods[0].Cooldowns.Clear();
        var high = caster.Cast(world, new CastMiracleRequest(1, "tidal_wave", village.Position), 1, []);

        Assert.True(wave.IsAccepted, wave.Rejection.ToString());
        Assert.True(world.Regions.Cells[village.RegionId].Flood > 0.8f);
        Assert.Equal((null, (int?)1), events.OfType<FloodStarted>().Select(f => (f.RiverId, f.GodId)).Single());
        Assert.Equal(CastRejection.NothingThere, high.Rejection);
    }

    [Fact]
    public void Judgement_StrikesOnlyTheFollowersOfGodsWhoWrongedTheJudge()
    {
        var (world, caster) = Create(GodDomain.Justice);
        world.Gods.Add(DivineWorlds.God(3, "Innocens", GodController.Ai, GodPersonality.Guardian, 0f));
        var village = world.Settlements[0];
        var guilty = DivineWorlds.Believe(village, adherence: 0.3f, faith: 0.6f, godId: 2);
        var innocent = DivineWorlds.Believe(village, adherence: 0.15f, faith: 0.6f, godId: 3);
        Assert.Equal(CastRejection.NothingThere, caster.Check(world, new CastMiracleRequest(1, "judgement", village.Position), 1).Rejection);
        DiplomacyModel.EnsureStance(world, 1, 2).Status = DiplomaticStatus.War;

        Cast(world, caster, "judgement", village.Position);

        Assert.Equal(100 - 4, village.Population);
        Assert.True(guilty.Faith < 0.6f);
        Assert.True(guilty.Doubt > 0.1f);
        Assert.Equal(0.6f, innocent.Faith, 3);
    }

    [Fact]
    public void Ocean_FeedsVillagesFromTheWatersAroundThem()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.8f);
        world.Gods[0].Domains.Add(GodDomain.Ocean);
        WaterWorlds.LayRiver(world);
        village.Food = 0f;

        Boons(world);

        Assert.Equal(Domains.Boons.OceanFishPerWaterRegion * 5, village.Food, 3);
    }

    [Fact]
    public void Justice_KeepsVillagesOrderly_AndLetsDoubtFade()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var belief = DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.8f, doubt: 0.4f);
        world.Gods[0].Domains.Add(GodDomain.Justice);
        village.Emigration = 10f;

        Boons(world);

        Assert.Equal(10f * (1f - Domains.Boons.JusticeCalmPerStep), village.Emigration, 3);
        Assert.True(belief.Doubt < 0.4f);
    }

    [Fact]
    public void AnAiGod_GivesWaterOnlyWhereItIsNeeded()
    {
        var world = RivalWorlds.Create(GodPersonality.Guardian, 1000f, 300f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.9f, faith: 0.9f, love: 0.5f, godId: RivalWorlds.RivalId);
        var rival = world.Gods.First(g => g.Id == RivalWorlds.RivalId);

        var watered = RivalWorlds.CreateAi().Evaluate(world, rival);
        village.Water = 0f;
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0.02f;
        }

        var parched = RivalWorlds.CreateAi().Evaluate(world, rival);

        Assert.DoesNotContain(watered, candidate => candidate.MiracleId == "water" && candidate.Utility > 0.05f);
        Assert.Contains(parched, candidate => candidate.MiracleId == "water" && candidate.Utility > 0.05f);
    }

    /// <summary>A Lesser God holding a domain (or none), followed in the first of two villages, with power to spare, and a rival.</summary>
    private static (WorldState World, MiracleCaster Caster) Create(GodDomain? domain)
    {
        var world = DivineWorlds.Create(2000f, 300f, 800f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 0f));
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.8f, love: 0.3f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.2f, faith: 0.8f, love: 0.3f);
        world.Gods[0].Rank = DivineRank.LesserGod;
        if (domain is { } held)
        {
            world.Gods[0].Domains.Add(held);
        }

        return (world, DomainTests.Caster(new DiplomacyLedger(DiplomacyWorlds.Rules)));
    }

    private static void Cast(WorldState world, MiracleCaster caster, string miracleId, GroundPosition target)
    {
        var check = caster.Cast(world, new CastMiracleRequest(1, miracleId, target), 1, []);
        Assert.True(check.IsAccepted, miracleId + ": " + check.Rejection);
    }

    private static void Boons(WorldState world) =>
        new DomainSystem(Domains, DivineWorlds.Rules, SettlementWorlds.Rules, WeatherWorlds.Rules).Update(new TickContext(world, new SimulationRandom(1), []));
}
