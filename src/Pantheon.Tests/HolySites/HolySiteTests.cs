using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.History;
using Pantheon.Domain.HolySites;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Domains;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.HolySites;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.HolySites;

/// <summary>Holy sites (spec §51, ADR-021): where they are, who takes them, and what keeping them gives.</summary>
public sealed class HolySiteTests
{
    private static HolySiteRules Rules => TestContent.Shipped.HolySites;

    [Fact]
    public void ANewWorld_FindsOneSiteOfEachKind_InTheLandThatSuitsIt_ApartFromEachOtherAndTheVillages()
    {
        var world = WorldGenerator.Generate(new WorldSeed(1234), TestContent.Shipped.WorldGen, TestContent.Shipped.Pantheon, TestContent.Shipped.Creatures, Rules).State;
        var again = HolySitePlacer.Place(Rules, world);
        var grid = world.Regions;

        Assert.Equal(Rules.Sites.Select(s => s.Kind), world.HolySites.Select(s => s.Kind));
        Assert.Equal(world.HolySites.Select(s => (s.Kind, s.RegionId, s.Name)), again.Select(s => (s.Kind, s.RegionId, s.Name)));
        Assert.All(world.HolySites, site => Assert.NotEqual(Biome.Sea, grid.Cells[site.RegionId].Biome));
        Assert.All(world.HolySites, site => Assert.All(world.Settlements, s => Assert.True(Chebyshev(grid, site.RegionId, s.RegionId) >= 3)));
        Assert.All(world.HolySites, a => Assert.All(world.HolySites.Where(b => b != a), b => Assert.True(Chebyshev(grid, a.RegionId, b.RegionId) >= Rules.MinimumSpacing)));
        var peak = world.HolySites.Single(s => s.Kind == HolySiteKind.SacredPeak);
        Assert.Equal(Biome.Highland, grid.Cells[peak.RegionId].Biome);
        Assert.Equal(Biome.Forest, grid.Cells[world.HolySites.Single(s => s.Kind == HolySiteKind.AncientGrove).RegionId].Biome);
        Assert.StartsWith("The Sacred Peak of ", peak.Name, StringComparison.Ordinal);
        Assert.All(world.HolySites, site => Assert.Null(site.KeeperGodId));
    }

    [Fact]
    public void TheGodWhoseInfluenceHoldsASite_TakesItAfterTheClaim_AndTheFormerKeeperRemembers()
    {
        var world = Create(out var site);
        site.KeeperGodId = 2;
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);
        var events = new List<WorldEvent>();
        var steps = (int)(Rules.ClaimSeconds * SimulationTime.TicksPerSecond / Rules.StepIntervalTicks);

        Run(world, steps - 1, events, ledger);
        var claiming = (site.ClaimantGodId, site.KeeperGodId);
        Run(world, 1, events, ledger);

        Assert.Equal((1, 2), (claiming.ClaimantGodId!.Value, claiming.KeeperGodId!.Value));
        Assert.Equal(1, site.KeeperGodId);
        Assert.Null(site.ClaimantGodId);
        var contest = Assert.Single(events.OfType<HolySiteContested>());
        Assert.Equal((1, 2), (contest.GodId, contest.KeeperGodId));
        var claim = Assert.Single(events.OfType<HolySiteClaimed>());
        Assert.Equal((site.Id, 1, 2), (claim.SiteId, claim.GodId, claim.PreviousGodId!.Value));
        Assert.Contains(DiplomacyModel.FindRelation(world, 2, 1)!.Memories, m => m.Act == DiplomaticAct.TookHolySite);
    }

    [Fact]
    public void NoGodTakesASite_WithoutEnoughInfluence_OrAClearLead_AndAClaimStartsOverWhenItBreaks()
    {
        var world = Create(out var site);
        BeliefModel.Find(world.Settlements[0], 1)!.Adherence = 0.45f;
        var contested = DivineWorlds.Believe(world.Settlements[0], adherence: 0.45f, faith: 0.9f, godId: 2);
        var events = new List<WorldEvent>();

        Run(world, 3, events);
        var tied = (site.ClaimantGodId, site.ClaimTicks);
        contested.Adherence = 0f;
        contested.Faith = 0f;
        Run(world, 3, events);
        var ticks = site.ClaimTicks;
        foreach (var belief in world.Settlements[0].Beliefs)
        {
            belief.Faith = 0.01f;
        }

        Run(world, 1, events);

        Assert.Equal(((int?)null, 0L), tied);
        Assert.Equal(3L * Rules.StepIntervalTicks, ticks);
        Assert.Equal(((int?)null, 0L), (site.ClaimantGodId, site.ClaimTicks));
        Assert.Empty(events.OfType<HolySiteClaimed>());
    }

    [Fact]
    public void AKeeper_GainsTheSitesPowerAndInfluence_AndItsBoon()
    {
        var world = Create(out var site);
        site.KeeperGodId = 1;
        var power = world.Gods[0].DivinePower;
        var without = InfluenceField.Build(DivineWorlds.Rules, world, 2).At(site.Position);

        Run(world, 1, []);

        var definition = HolySiteModel.Find(Rules, site.Kind)!;
        Assert.Equal(power + definition.PowerPerStep, world.Gods[0].DivinePower, 3);
        Assert.True(InfluenceField.Build(DivineWorlds.Rules, world, 1).At(site.Position) >= DivineWorlds.Rules.HolySiteInfluenceStrength);
        Assert.Equal(0f, without);
        Assert.Equal(1f - definition.BoonAmount, HolySiteModel.CostFactor(Rules, world, 1), 4);
        Assert.Equal(1f, HolySiteModel.CostFactor(Rules, world, 2));
    }

    [Fact]
    public void TheGrove_BlessesTheLand_TheSpring_HealsItsKeepersVillages_AndTheStones_TeachItsCreature()
    {
        var (world, creature) = CreatureWorlds.Create(300f, 900f);
        var settlement = world.Settlements[0];
        settlement.Health = 0.5f;
        world.Settlements[1].Health = 0.5f;
        var near = new GroundPosition(settlement.Position.X, settlement.Position.Z + 150f);
        world.HolySites.AddRange([Site(1, HolySiteKind.AncientGrove, near, 1), Site(2, HolySiteKind.HolySpring, near, 1), Site(3, HolySiteKind.StandingStones, near, 1)]);

        Run(world, 1, [], creatures: true);

        Assert.True(world.Regions.Cells[settlement.RegionId].Blessing >= HolySiteModel.Find(Rules, HolySiteKind.AncientGrove)!.BoonAmount);
        Assert.True(settlement.Health > 0.5f);
        Assert.Equal(HolySiteModel.Find(Rules, HolySiteKind.StandingStones)!.BoonAmount, creature.Experience, 4);
        Assert.Equal(0.5f, world.Settlements[1].Health);
    }

    [Fact]
    public void AnAiGod_SendsItsCreatureToClaimASite_AndToHoldItsOwn()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        var god = world.Gods[0];
        god.Controller = GodController.Ai;
        var profiled = new GodState
        {
            Id = god.Id, Name = god.Name, Controller = GodController.Ai, Personality = GodPersonality.Zealot, DivinePower = 0f, Rank = god.Rank,
            DominantSinceTick = null, CreatureId = god.CreatureId, RefugeePolicy = god.RefugeePolicy, Cooldowns = [], Domains = [],
            Conduct = Pantheon.Domain.Culture.GodConduct.None(), Traits = [],
        };
        world.Gods[0] = profiled;
        world.HolySites.Add(Site(1, HolySiteKind.StandingStones, new GroundPosition(900f, 900f), null));
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles);
        var ai = new GodAi(RivalWorlds.AiRules, DivineWorlds.Rules, DivineWorlds.Miracles, caster, new Pantheon.Simulation.Creatures.CreatureAuthority(CreatureWorlds.Rules, new Pantheon.Simulation.Creatures.CreatureBrain(CreatureWorlds.Rules, DivineWorlds.Rules, SettlementWorlds.Rules)), CreatureWorlds.Rules);

        var claim = ai.Evaluate(world, profiled).Single(c => c.Reason.StartsWith("claim ", StringComparison.Ordinal));
        world.HolySites[0].KeeperGodId = 1;
        var quiet = ai.Evaluate(world, profiled).Any(c => c.Reason.StartsWith("hold ", StringComparison.Ordinal));
        world.HolySites[0].ClaimantGodId = 2;
        var hold = ai.Evaluate(world, profiled).Single(c => c.Reason.StartsWith("hold ", StringComparison.Ordinal));

        Assert.Equal(AiActionKind.SendCreature, claim.Kind);
        Assert.Equal(new GroundPosition(900f, 900f), claim.Target);
        Assert.False(quiet);
        Assert.True(hold.Utility > claim.Utility);
        Assert.NotNull(creature);
    }

    [Fact]
    public void TakingASite_IsHistory_AndThePlayerHearsOfIt()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(42));
        var site = session.World.HolySites[0];
        var feed = new AlertFeed();
        var events = new List<WorldEvent>
        {
            new HolySiteContested { Tick = 0, SiteId = site.Id, GodId = 2, KeeperGodId = 1 },
            new HolySiteClaimed { Tick = 0, SiteId = site.Id, GodId = 2, PreviousGodId = 1 },
        };

        session.Content.CreateEngine().Chronicle(session.World, events, 0);
        var alerts = feed.Push(events, session.World, 1, 1.0);

        var record = session.World.History.Single(r => r.Kind == HistoryKind.Claimed);
        Assert.Equal($"Year 0 — Vael claims {DomainText.SiteName(site)} (taken from Ilyra)", Pantheon.Game.History.HistoryText.Line(record, session.World, session.Content.History));
        Assert.Contains(alerts, a => a.Text.StartsWith("Vael is taking ", StringComparison.Ordinal));
        Assert.Contains(alerts, a => a.Text == $"Vael has taken {DomainText.SiteName(site)} from you.");
    }

    /// <summary>
    /// Two villages 600 m apart, the first following god 1 and near a Sacred Peak 150 m away, and a
    /// second god followed nowhere yet.
    /// </summary>
    private static WorldState Create(out HolySite site)
    {
        var world = DivineWorlds.Create(100f, 300f, 900f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 0f));
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.9f, faith: 0.9f);
        site = Site(1, HolySiteKind.SacredPeak, new GroundPosition(450f, 512f), null);
        world.HolySites.Add(site);
        return world;
    }

    /// <summary>A holy site at a point.</summary>
    private static HolySite Site(int id, HolySiteKind kind, GroundPosition position, int? keeper) =>
        new() { Id = id, Kind = kind, Name = "The " + kind, RegionId = 0, Position = position, KeeperGodId = keeper, ClaimantGodId = null, ClaimTicks = 0 };

    /// <summary>Runs holy site steps.</summary>
    private static void Run(WorldState world, int steps, List<WorldEvent> events, IActRecorder? ledger = null, bool creatures = false)
    {
        var system = new HolySiteSystem(Rules, DivineWorlds.Rules, creatures ? CreatureWorlds.Rules : null, ledger);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events));
            world.Tick += Rules.StepIntervalTicks;
        }
    }

    /// <summary>Chebyshev distance between two regions.</summary>
    private static int Chebyshev(RegionGrid grid, int a, int b)
    {
        var (ac, ar) = grid.GetCoordinates(a);
        var (bc, br) = grid.GetCoordinates(b);
        return Math.Max(Math.Abs(ac - bc), Math.Abs(ar - br));
    }
}
