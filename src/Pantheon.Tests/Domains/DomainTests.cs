using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.History;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Hud;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Domains;

/// <summary>God domains (spec §12, ADR-021): slots by rank, the authority, cheaper miracles, signatures and the boons.</summary>
public sealed class DomainTests
{
    private static DomainRules Rules => TestContent.Shipped.Domains;

    [Fact]
    public void Slots_GrowWithRank_AndTheAuthorityKeepsToThem()
    {
        var world = DivineWorlds.Create(100f, 300f);
        var authority = new DomainAuthority(Rules);
        var events = new List<WorldEvent>();

        Assert.Equal((1, 2, 3, 3), (DomainModel.SlotsFor(Rules, DivineRank.MinorSpirit), DomainModel.SlotsFor(Rules, DivineRank.LesserGod), DomainModel.SlotsFor(Rules, DivineRank.God), DomainModel.SlotsFor(Rules, DivineRank.Ascendant)));
        Assert.Equal(DomainRejection.None, authority.Choose(world, 1, GodDomain.Storm, 1, events));
        Assert.Equal(DomainRejection.NoFreeSlot, authority.Choose(world, 1, GodDomain.War, 1, events));
        world.Gods[0].Rank = DivineRank.LesserGod;
        Assert.Equal(DomainRejection.AlreadyHeld, authority.Check(world, 1, GodDomain.Storm, 1));
        Assert.Equal(DomainRejection.UnknownDomain, authority.Check(world, 1, GodDomain.Ocean, 1));
        Assert.Equal(DomainRejection.NotAuthorized, authority.Check(world, 1, GodDomain.War, 2));
        Assert.Equal(DomainRejection.UnknownGod, authority.Check(world, 9, GodDomain.War, null));
        Assert.Equal(DomainRejection.None, authority.Choose(world, 1, GodDomain.War, null, events));

        Assert.Equal([GodDomain.Storm, GodDomain.War], world.Gods[0].Domains);
        Assert.Equal([GodDomain.Storm, GodDomain.War], events.OfType<DomainChosen>().Select(e => e.Domain));
    }

    [Fact]
    public void OwnDomainMiracles_CostLess_AndASignatureNeedsItsDomain_AndSomethingToActOn()
    {
        var world = DivineWorlds.Create(1000f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.8f);
        var caster = Caster();
        var target = world.Settlements[0].Position;
        var before = caster.Check(world, new CastMiracleRequest(1, "lightning", target), 1).Cost;

        Assert.Equal(CastRejection.DomainRequired, caster.Check(world, new CastMiracleRequest(1, "redirect_storm", target), 1).Rejection);
        world.Gods[0].Domains.Add(GodDomain.Storm);
        Assert.Equal(before * Rules.DomainCostFactor, caster.Check(world, new CastMiracleRequest(1, "lightning", target), 1).Cost, 3);
        Assert.Equal(CastRejection.NothingThere, caster.Check(world, new CastMiracleRequest(1, "redirect_storm", target), 1).Rejection);
        WeatherWorlds.AddCell(world, new GroundPosition(target.X + 500f, target.Z), 200f, humidity: 0.9f, pressure: 0.8f);

        Assert.Equal(CastRejection.None, caster.Check(world, new CastMiracleRequest(1, "redirect_storm", target), 1).Rejection);
    }

    [Fact]
    public void TheHud_OffersTheSignaturesOfHeldDomainsOnly_AtTheirLowerCost()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(42));
        var god = session.LocalGod!;

        var before = HudReadout.Build(session, null, null);
        god.Domains.Add(GodDomain.Storm);
        var after = HudReadout.Build(session, null, null);

        Assert.DoesNotContain(before.Miracles, slot => slot.MiracleId == "redirect_storm");
        Assert.True(before.DomainToChoose);
        Assert.Contains("choose your first [G]", before.Domains, StringComparison.Ordinal);
        var redirect = after.Miracles.Single(slot => slot.MiracleId == "redirect_storm");
        Assert.Equal(10, redirect.Hotkey);
        Assert.Equal(TestContent.Shipped.Miracles.Find("redirect_storm")!.PowerCost * Rules.DomainCostFactor, redirect.BaseCost, 3);
        Assert.Equal(TestContent.Shipped.Miracles.Find("rain")!.PowerCost * Rules.DomainCostFactor, after.Miracles.Single(s => s.MiracleId == "rain").BaseCost, 3);
        Assert.False(after.DomainToChoose);
        Assert.StartsWith("Domains: Storm", after.Domains, StringComparison.Ordinal);
    }

    [Fact]
    public void Nature_KeepsItsFarmlandBlessed_AndLife_HealsItsVillages()
    {
        var world = DivineWorlds.Create(100f, 300f, 800f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        world.Settlements[0].Health = 0.5f;
        world.Settlements[1].Health = 0.5f;
        world.Gods[0].Domains.AddRange([GodDomain.Nature, GodDomain.Life]);

        Step(world);

        var own = world.Regions.Cells[world.Settlements[0].RegionId];
        var other = world.Regions.Cells[world.Settlements[1].RegionId];
        Assert.Equal(Rules.Boons.NatureBlessing, own.Blessing);
        Assert.Equal(0f, other.Blessing);
        Assert.Equal(0.5f + (0.5f * Rules.Boons.LifeHealthPerStep), world.Settlements[0].Health, 5);
        Assert.Equal(0.5f, world.Settlements[1].Health);
    }

    [Fact]
    public void Storm_TurnsAStormOverItsFollowersIntoAwe_AndFire_DiesDownOnItsLand()
    {
        var world = DivineWorlds.Create(100f, 300f, 800f);
        var awed = DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.5f, fear: 0.1f);
        var calm = DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: 0.5f, fear: 0.1f);
        world.Gods[0].Domains.AddRange([GodDomain.Storm, GodDomain.Fire]);
        var storm = WeatherWorlds.AddCell(world, world.Settlements[0].Position, 200f, humidity: 1f, pressure: 1f);
        world.Regions.Cells[world.Settlements[0].RegionId].Fire = 0.8f;

        Step(world);

        Assert.True(storm.Wind > 0f);
        Assert.True(awed.Faith > 0.5f && awed.Fear > 0.1f);
        Assert.Equal((0.5f, 0.1f), (calm.Faith, calm.Fear));
        Assert.Equal(0.8f * (1f - Rules.Boons.FireQuenchPerStep), world.Regions.Cells[world.Settlements[0].RegionId].Fire, 5);
    }

    [Fact]
    public void Prosperity_RaisesThePowerWorshipBrings()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        world.Gods[0].Domains.Add(GodDomain.Prosperity);
        var worship = GodStanding.ComputePowerPerStep(DivineWorlds.Rules, SettlementWorlds.Rules, world, 1);

        Step(world);

        Assert.Equal(worship * Rules.Boons.ProsperityPower, world.Gods[0].DivinePower, 3);
    }

    [Fact]
    public void Protection_SavesHousesThatAQuakeWouldBring_Down()
    {
        var (spared, ruined) = (Quake(protect: true), Quake(protect: false));

        Assert.True(spared < ruined, $"{spared} houses lost under Protection against {ruined}");
    }

    [Fact]
    public void Death_GainsPowerFromTheLivesItsMiraclesTake_AndTrickery_PlantsMoreDoubt_AndChaos_ShakesHarder()
    {
        var world = DivineWorlds.Create(2000f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.3f, faith: 0.8f);
        var rival = DivineWorlds.Believe(world.Settlements[0], adherence: 0.3f, faith: 0.5f, godId: 2);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 0f));
        world.Gods[0].Rank = DivineRank.God;
        var caster = Caster();
        caster.Cast(world, new CastMiracleRequest(1, "lightning", world.Settlements[0].Position), 1, []);
        var plainDoubt = rival.Doubt;

        world.Gods[0].Domains.AddRange([GodDomain.Death, GodDomain.Trickery, GodDomain.Chaos]);
        world.Gods[0].Cooldowns.Clear();
        rival.Doubt = 0f;
        var power = world.Gods[0].DivinePower;
        var deadly = caster.Cast(world, new CastMiracleRequest(1, "lightning", world.Settlements[0].Position), 1, []);
        var gained = world.Gods[0].DivinePower - (power - deadly.Cost);
        var events = new List<WorldEvent>();
        caster.Cast(world, new CastMiracleRequest(1, "earthquake", world.Settlements[0].Position), 1, events);

        Assert.Equal(3f * Rules.Boons.DeathPowerPerDeath, gained, 3);
        Assert.True(rival.Doubt > plainDoubt, "a trickster's miracle plants more doubt about the rival");
        Assert.Equal(6.8f + Rules.Boons.ChaosMagnitude, events.OfType<EarthquakeStruck>().Single().Magnitude, 3);
    }

    [Fact]
    public void War_StrikesHarder_AndKnowledge_LearnsMoreFromEachJudgement()
    {
        var (plain, warlike) = (Blows(war: false), Blows(war: true));
        var creature = CreatureWorlds.Create(300f).Creature;
        var pupil = CreatureWorlds.Create(300f).Creature;

        var slow = CreatureModel.Learn(CreatureBondWorlds.Rules, creature, CreatureSituation.VillageInNeed, CreatureAction.Feed, CreatureFeedback.Rewarded);
        var quick = CreatureModel.Learn(CreatureBondWorlds.Rules, pupil, CreatureSituation.VillageInNeed, CreatureAction.Feed, CreatureFeedback.Rewarded, Rules.Boons.KnowledgeLearning);

        Assert.Equal(plain * (1f + Rules.Boons.WarCreatureDamage), warlike, 3);
        Assert.True(quick > slow);
    }

    [Fact]
    public void AnAiGod_TakesUpItsPreferredDomains_AsItRises()
    {
        var world = RivalWorlds.Create(GodPersonality.Warlord, 100f, 300f, 700f);
        var authority = new DomainAuthority(Rules);
        var caster = Caster();
        var ai = new GodAi(RivalWorlds.AiRules, DivineWorlds.Rules, DivineWorlds.Miracles, caster, null, null, null, authority);
        var rival = world.Gods[1];
        var preferred = PantheonWorlds.Profile(GodPersonality.Warlord).Domains;
        var events = new List<WorldEvent>();

        ai.Act(world, rival, new SimulationRandom(1), events);
        var first = rival.Domains.ToList();
        ai.Act(world, rival, new SimulationRandom(2), events);
        rival.Rank = DivineRank.LesserGod;
        ai.Act(world, rival, new SimulationRandom(3), events);

        Assert.Equal([preferred[0]], first);
        Assert.Equal([preferred[0], preferred[1]], rival.Domains);
        Assert.Null(ai.NextDomain(rival));
        Assert.Equal(2, events.OfType<DomainChosen>().Count());
    }

    [Fact]
    public void ChoosingADomain_IsHistory_AndRivalsHearOfIt()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(42));
        var feed = new AlertFeed();

        var outcome = Pantheon.Game.Domains.DomainCommands.ChooseAs(session, 2, GodDomain.Death);
        var alerts = feed.Push(session.DrainEvents(), session.World, session.LocalGodId, 1.0);

        Assert.True(outcome.Accepted);
        Assert.Equal("You take up Death", outcome.Text);
        var record = session.World.History.Single(r => r.Kind == HistoryKind.Domain);
        Assert.Equal("Year 0 — Vael takes up Death", Pantheon.Game.History.HistoryText.Line(record, session.World, session.Content.History));
        Assert.Contains(alerts, alert => alert.Text == "Vael takes up Death.");
        Assert.Equal("Rise in rank to take up another domain", Pantheon.Game.Domains.DomainCommands.ChooseAs(session, 2, GodDomain.War).Text);
    }

    /// <summary>A miracle caster with every rule, domains and holy sites included.</summary>
    internal static MiracleCaster Caster(IActRecorder? ledger = null) =>
        new(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, ledger, WeatherWorlds.Rules, DisasterWorlds.Rules, CreatureBondWorlds.Rules, Rules, TestContent.Shipped.HolySites);

    /// <summary>One step of the domains' boons.</summary>
    private static void Step(WorldState world) =>
        new DomainSystem(Rules, DivineWorlds.Rules, SettlementWorlds.Rules, WeatherWorlds.Rules).Update(new TickContext(world, new SimulationRandom(1), []));

    /// <summary>Houses a strong quake brings down in a big village, with or without its god's Protection.</summary>
    private static int Quake(bool protect)
    {
        var world = DivineWorlds.Create(100f, 300f);
        var village = world.Settlements[0];
        (village.Population, village.Housing) = (400, 600);
        DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.6f);
        if (protect)
        {
            world.Gods[0].Domains.Add(GodDomain.Protection);
        }

        Pantheon.Simulation.Disasters.DisasterModel.StartQuake(DisasterWorlds.Rules, world.Disasters, village.Position, 7.5f, 0, null);
        var system = new Pantheon.Simulation.Disasters.DisasterSystem(DisasterWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Rules, WeatherWorlds.Rules, Rules);
        system.Update(new TickContext(world, new SimulationRandom(1), []));
        return village.Ruins;
    }

    /// <summary>Health the second Creature loses in ten ticks of a fight with the first, whose god holds War or not.</summary>
    private static float Blows(bool war)
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        if (war)
        {
            world.Gods[0].Domains.Add(GodDomain.War);
        }

        foreach (var (creature, other) in new[] { (mine, theirs), (theirs, mine) })
        {
            creature.Activity.Action = CreatureAction.Fight;
            creature.Activity.Phase = CreaturePhase.Performing;
            creature.Activity.TargetCreatureId = other.Id;
            creature.Activity.TicksLeft = 400;
        }

        var combat = new CreatureCombat(CreatureBondWorlds.Rules, null, Rules);
        var before = theirs.Health;
        for (var i = 0; i < 10; i++)
        {
            combat.Update(world, []);
            world.Tick++;
        }

        return before - theirs.Health;
    }
}
