using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Domains;

/// <summary>The domains' signature miracles (ADR-021): what each one does, and what it needs to find.</summary>
public sealed class SignatureMiracleTests
{
    [Fact]
    public void Regrowth_PutsOutFires_HealsBurnedLand_AndWatersIt()
    {
        var (world, caster) = Create(GodDomain.Nature);
        var region = world.Regions.Cells[world.Settlements[0].RegionId];
        (region.Fire, region.Scorch, region.SoilMoisture) = (0.7f, 0.9f, 0.1f);

        Cast(world, caster, "regrowth", world.Settlements[0].Position);

        Assert.Equal(0f, region.Fire);
        Assert.Equal(0.9f * (1f - 0.8f), region.Scorch, 4);
        Assert.Equal(0.8f, region.SoilMoisture, 4);
        Assert.True(region.Blessing >= 0.5f);
    }

    [Fact]
    public void GiftOfLife_BringsNewcomersWhoShareTheVillagesBeliefs_WhereThereAreHomes()
    {
        var (world, caster) = Create(GodDomain.Life);
        var village = world.Settlements[0];
        var own = BeliefModel.Find(village, 1)!;
        var rival = DivineWorlds.Believe(village, adherence: 0.3f, faith: 0.5f, godId: 2);

        Cast(world, caster, "gift_of_life", village.Position);
        var (grown, followed, shared) = (village.Population, own.Adherence, rival.Adherence);
        village.Housing = village.Population;
        world.Gods[0].Cooldowns.Clear();
        Cast(world, caster, "gift_of_life", village.Position);

        Assert.Equal(108, grown);
        Assert.Equal(0.5f, followed);
        Assert.Equal(0.3f, shared);
        Assert.True(own.Love > 0.3f);
        Assert.Equal(108, village.Population);
    }

    [Fact]
    public void RedirectStorm_CarriesTheStrongestCloudInReach_ToTheTarget()
    {
        var (world, caster) = Create(GodDomain.Storm);
        var target = world.Settlements[0].Position;
        var weak = WeatherWorlds.AddCell(world, new GroundPosition(target.X + 300f, target.Z), 150f, humidity: 0.4f, pressure: 0.2f);
        var strong = WeatherWorlds.AddCell(world, new GroundPosition(target.X + 600f, target.Z), 200f, humidity: 0.9f, pressure: 0.9f);

        Cast(world, caster, "redirect_storm", target);

        Assert.Equal((target.X, target.Z), (strong.X, strong.Z));
        Assert.Equal(target.X + 300f, weak.X);
    }

    [Fact]
    public void SpearOfHeaven_WoundsRivalCreatures_ButSparesAnAllys_AndTheRivalRemembers()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create(gap: 10f);
        world.Gods[0].Domains.Add(GodDomain.War);
        world.Gods[0].Rank = DivineRank.LesserGod;
        world.Gods[0].DivinePower = 1000f;
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.8f);
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);
        var caster = DomainTests.Caster(ledger);

        var check = caster.Cast(world, new CastMiracleRequest(1, "spear_of_heaven", theirs.Position), 1, []);
        var wounded = theirs.Health;
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Alliance);
        theirs.Health = 1f;
        world.Gods[0].Cooldowns.Clear();
        var spared = caster.Check(world, new CastMiracleRequest(1, "spear_of_heaven", theirs.Position), 1);

        Assert.True(check.IsAccepted, check.Rejection.ToString());
        Assert.Equal(1f - (0.4f / CreatureModel.SpeciesOf(CreatureBondWorlds.Rules, theirs).Toughness), wounded, 4);
        Assert.Equal(1f, mine.Health);
        Assert.Contains(DiplomacyModel.FindRelation(world, 2, 1)!.Memories, m => m.Act == DiplomaticAct.Harmed);
        Assert.Equal(CastRejection.NothingThere, spared.Rejection);
    }

    [Fact]
    public void Dread_TurnsPeopleFromEveryRivalGod_ButNotFromAnAlly()
    {
        var (world, caster) = Create(GodDomain.Death);
        world.Gods.Add(DivineWorlds.God(3, "Allia", GodController.Ai, GodPersonality.Guardian, 0f));
        var village = world.Settlements[0];
        var rival = DivineWorlds.Believe(village, adherence: 0.2f, faith: 0.6f, godId: 2);
        var ally = DivineWorlds.Believe(village, adherence: 0.2f, faith: 0.6f, godId: 3);
        PantheonWorlds.SetStatus(world, 1, 3, DiplomaticStatus.Alliance);

        Cast(world, caster, "dread", village.Position);

        Assert.True(rival.Faith < 0.6f * 0.75f);
        Assert.True(rival.Doubt >= 0.3f);
        Assert.Equal(0.6f, ally.Faith);
    }

    [Fact]
    public void Sanctuary_KeepsTheGodsFollowersFromFallingAway_UntilItEnds()
    {
        var (world, caster) = Create(GodDomain.Protection);
        var village = world.Settlements[0];
        var own = BeliefModel.Find(village, 1)!;
        (own.Doubt, own.Fear) = (0.9f, 0.6f);
        var belief = new BeliefSystem(DivineWorlds.Rules, SettlementWorlds.Rules);

        Cast(world, caster, "sanctuary", village.Position);
        var held = own.Adherence;
        belief.Update(new TickContext(world, new SimulationRandom(1), []));
        var kept = own.Adherence;
        world.Tick = village.SanctuaryUntilTick;
        own.Doubt = 0.9f;
        belief.Update(new TickContext(world, new SimulationRandom(1), []));

        Assert.True(kept >= held);
        Assert.True(own.Adherence < kept);
        Assert.Null(village.SanctuaryGodId);
    }

    [Fact]
    public void Enlighten_TeachesTheGodsOwnCreature_WhenItIsThere()
    {
        var (world, creature) = CreatureWorlds.Create(300f, 800f);
        var god = world.Gods[0];
        (god.Rank, god.DivinePower) = (DivineRank.LesserGod, 1000f);
        god.Domains.Add(GodDomain.Knowledge);
        var caster = DomainTests.Caster();
        var events = new List<WorldEvent>();

        var away = caster.Check(world, new CastMiracleRequest(1, "enlighten", new GroundPosition(creature.Position.X + 150f, creature.Position.Z)), 1);
        caster.Cast(world, new CastMiracleRequest(1, "enlighten", creature.Position), 1, events);

        Assert.Equal(CastRejection.NothingThere, away.Rejection);
        Assert.Equal(80f, creature.Experience, 3);
        Assert.Contains(events, e => e is CreatureChanged { Change: CreatureChange.LevelledUp });
    }

    [Fact]
    public void SowDiscord_MakesTheGodsFollowedThere_RememberWrongsNoneOfThemDid()
    {
        var (world, caster) = Create(GodDomain.Trickery);
        world.Gods[0].Rank = DivineRank.God;
        world.Gods.Add(DivineWorlds.God(3, "Tertia", GodController.Ai, GodPersonality.Guardian, 0f));
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.2f, faith: 0.6f, godId: 2);
        DivineWorlds.Believe(village, adherence: 0.2f, faith: 0.6f, godId: 3);
        PantheonWorlds.SetStatus(world, 2, 3, DiplomaticStatus.Alliance);

        Cast(world, caster, "sow_discord", village.Position);

        Assert.Equal(2, DiplomacyModel.FindRelation(world, 2, 3)!.Memories.Count(m => m.Act == DiplomaticAct.Harmed));
        Assert.Equal(2, DiplomacyModel.FindRelation(world, 3, 2)!.Memories.Count(m => m.Act == DiplomaticAct.Harmed));
        Assert.Null(DiplomacyModel.FindRelation(world, 2, 1));
        Assert.Equal(0f, BeliefModel.Find(village, 2)!.Doubt);
    }

    [Fact]
    public void Upheaval_ShakesTheGround_LongBeforeAnEarthquake()
    {
        var (world, caster) = Create(GodDomain.Chaos);
        var events = new List<WorldEvent>();

        var quake = caster.Check(world, new CastMiracleRequest(1, "earthquake", world.Settlements[0].Position), 1);
        caster.Cast(world, new CastMiracleRequest(1, "upheaval", world.Settlements[0].Position), 1, events);

        Assert.Equal(CastRejection.RankTooLow, quake.Rejection);
        Assert.Equal(5.8f + TestContent.Shipped.Domains.Boons.ChaosMagnitude, events.OfType<EarthquakeStruck>().Single().Magnitude, 3);
    }

    /// <summary>A Lesser God holding a domain, followed in the first of two villages, with power to spare, and a caster with domains.</summary>
    private static (WorldState World, MiracleCaster Caster) Create(GodDomain domain)
    {
        var world = DivineWorlds.Create(2000f, 300f, 800f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 0f));
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.8f, love: 0.3f);
        world.Gods[0].Rank = DivineRank.LesserGod;
        world.Gods[0].Domains.Add(domain);
        return (world, DomainTests.Caster(new DiplomacyLedger(DiplomacyWorlds.Rules)));
    }

    /// <summary>Casts a miracle as god 1 and insists it was accepted.</summary>
    private static void Cast(WorldState world, MiracleCaster caster, string miracleId, GroundPosition target)
    {
        var check = caster.Cast(world, new CastMiracleRequest(1, miracleId, target), 1, []);
        Assert.True(check.IsAccepted, miracleId + ": " + check.Rejection);
    }
}
