using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.GreatEvents;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>A Creature's courage, protectiveness, jealousy and playfulness (spec §26, ADR-037).</summary>
public sealed class TemperamentTests
{
    private static readonly CreatureRules Rules = CreatureBondWorlds.Rules;
    private static readonly TemperamentRules Temperament = Rules.Temperament;

    [Fact]
    public void TheTraitsBehindEachAction_DriveIt_CourageForFightingAndItsLackForFleeing()
    {
        var brave = new CreaturePersonality { Kindness = 0.5f, Aggression = 0.5f, Laziness = 0.5f, Curiosity = 0.5f, Courage = 0.9f, Playfulness = 0.9f };
        var timid = new CreaturePersonality { Kindness = 0.5f, Aggression = 0.5f, Laziness = 0.5f, Curiosity = 0.5f, Courage = 0.1f, Playfulness = 0.1f };

        Assert.True(CreatureTemperament.Drive(brave, CreatureAction.Fight) > CreatureTemperament.Drive(timid, CreatureAction.Fight));
        Assert.True(CreatureTemperament.Drive(timid, CreatureAction.Flee) > CreatureTemperament.Drive(brave, CreatureAction.Flee));
        Assert.Equal(1.4f, CreatureTemperament.Drive(timid, CreatureAction.Flee), 4);
        Assert.Equal(1.2f, CreatureTemperament.Drive(brave, CreatureAction.Befriend), 4);
        Assert.Equal(1f, CreatureTemperament.Drive(brave, CreatureAction.Forage));
        Assert.Equal(CreatureTemperament.Drive(brave, CreatureAction.Feed), CreatureTemperament.Drive(timid, CreatureAction.Feed));
    }

    [Fact]
    public void ANewCreature_IsBornWithAllEightTraits_FromTheSeedAndItsSpecies()
    {
        var world = DivineWorlds.Create(100f, 300f);
        var god = world.Gods[0];
        var stonehide = CreatureFounder.Found(Rules, world.Seed, god, "Rock", world.Settlements[0], world.Regions, "Tortoise");
        var lumen = CreatureFounder.Found(Rules, world.Seed, god, "Glow", world.Settlements[0], world.Regions, "Ape");
        var again = CreatureFounder.Found(Rules, world.Seed, god, "Rock", world.Settlements[0], world.Regions, "Tortoise");

        var species = Rules.Species.ToDictionary(s => s.Name);
        Assert.Equal(species["Tortoise"].Courage - species["Ape"].Courage, stonehide.Personality.Courage - lumen.Personality.Courage, 4);
        Assert.Equal(species["Ape"].Playfulness - species["Tortoise"].Playfulness, lumen.Personality.Playfulness - stonehide.Personality.Playfulness, 4);
        Assert.Equal((stonehide.Personality.Jealousy, stonehide.Personality.Protectiveness), (again.Personality.Jealousy, again.Personality.Protectiveness));
        Assert.Equal(lumen.Personality.Kindness - species["Ape"].Kindness, stonehide.Personality.Kindness - species["Tortoise"].Kindness, 4);
    }

    [Fact]
    public void VictoryEmboldens_DefeatCowsAndEmbitters_TheLoser()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        foreach (var creature in new[] { mine, theirs })
        {
            CreatureBondWorlds.Prefer(creature, CreatureSituation.MeetsEnemyCreature, CreatureAction.Fight);
            (creature.Personality.Courage, creature.Personality.Jealousy) = (0.5f, 0.5f);
        }

        (mine.Level, mine.Experience) = (5, Rules.LevelExperience[3]);
        (theirs.Level, theirs.Experience) = (2, Rules.LevelExperience[0]);

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 400);

        Assert.Equal(FightOutcome.Banished, Assert.Single(events.OfType<CreatureFightEnded>()).Outcome);
        Assert.Equal(0.5f + Temperament.CouragePerVictory, mine.Personality.Courage, 4);
        Assert.True(theirs.Personality.Courage < 0.5f - Temperament.CouragePerDefeat + 0.01f);
        Assert.Equal(0.5f + Temperament.JealousyPerDefeat, theirs.Personality.Jealousy, 4);
    }

    [Fact]
    public void ACowardRunsAndIsCowedFurther_ABraveOneStandsItsGround()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        CreatureBondWorlds.Prefer(mine, CreatureSituation.MeetsEnemyCreature, CreatureAction.Fight);
        CreatureBonds.Ensure(Rules.Bonds, theirs, mine.Id).Fear = 0.8f;
        (theirs.Personality.Courage, theirs.Personality.Protectiveness) = (0.05f, 0f);
        var cowardFlee = CreatureBonds.Drives(Rules, world, theirs, mine).Flee * CreatureTemperament.Drive(theirs.Personality, CreatureAction.Flee);
        theirs.Personality.Courage = 0.95f;
        var braveFlee = CreatureBonds.Drives(Rules, world, theirs, mine).Flee * CreatureTemperament.Drive(theirs.Personality, CreatureAction.Flee);
        theirs.Personality.Courage = 0.05f;
        CreatureBondWorlds.Prefer(theirs, CreatureSituation.MeetsEnemyCreature, CreatureAction.Flee);

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 10);

        Assert.True(cowardFlee > braveFlee);
        Assert.Equal(MeetingOutcome.Fled, Assert.Single(events.OfType<CreaturesMet>()).Outcome);
        Assert.Equal(0.05f - Temperament.CouragePerFlight, theirs.Personality.Courage, 4);
    }

    [Fact]
    public void AProtectiveCreature_StandsItsGroundNearItsGodsPeople_ButNotAgainstAFriend()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Hostile);
        mine.Personality.Protectiveness = 0f;
        var careless = CreatureBonds.Drives(Rules, world, mine, theirs);
        mine.Personality.Protectiveness = 1f;
        var guarding = CreatureBonds.Drives(Rules, world, mine, theirs);
        theirs.Position = new GroundPosition(900f, 512f);
        var away = CreatureBonds.Drives(Rules, world, mine, theirs);
        theirs.Position = new GroundPosition(335f, 512f);
        CreatureBonds.Ensure(Rules.Bonds, mine, theirs.Id).Friendship = 0.9f;
        var friendly = CreatureBonds.Drives(Rules, world, mine, theirs);

        Assert.True(guarding.Fight > careless.Fight);
        Assert.Equal(careless.Fight, away.Fight, 4);
        Assert.Equal(0f, friendly.Fight);
        Assert.Equal(Temperament.GuardFight, CreatureTemperament.Guard(Rules, world, mine, theirs.Position) * Temperament.GuardFight, 4);
    }

    [Fact]
    public void AJealousCreature_EnviesAStrongerOne_ButNotAWeakerOneOrAnAllysCreature()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        (mine.Personality.Jealousy, theirs.Personality.Jealousy) = (1f, 1f);
        (mine.Level, mine.Experience) = (1, 0f);
        (theirs.Level, theirs.Experience) = (6, Rules.LevelExperience[4]);

        CreatureTemperament.Envy(Rules, world, mine, theirs);
        CreatureTemperament.Envy(Rules, world, theirs, mine);

        Assert.Equal(Temperament.EnvyRivalry, CreatureBonds.Find(mine, theirs.Id)!.Rivalry, 4);
        Assert.Null(CreatureBonds.Find(theirs, mine.Id));
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Alliance);
        CreatureTemperament.Envy(Rules, world, mine, theirs);
        Assert.Equal(Temperament.EnvyRivalry, CreatureBonds.Find(mine, theirs.Id)!.Rivalry, 4);
    }

    [Fact]
    public void AProtectiveBraveCreature_GoesByItselfAgainstTheBeast_ACowardDoesNot_NorOneItsGodSentElsewhere()
    {
        var (world, mine, _) = CreatureBondWorlds.Create();
        var village = world.Settlements[0];
        world.GreatEvents.Beast = new BeastState
        {
            Name = "Gharos the Ancient", LairRegionId = 0, Phase = BeastPhase.Awake, Position = village.Position, Health = 100f, MaxHealth = 100f,
            TargetSettlementId = village.Id, WokeTick = 0, RavagedTicks = 0,
        };
        mine.Position = new GroundPosition(village.Position.X + 150f, village.Position.Z);
        mine.Activity.Action = CreatureAction.Rest;
        (mine.Personality.Protectiveness, mine.Personality.Courage) = (0.9f, 0.9f);
        var events = Step(world, 1);

        Assert.Equal((CreatureAction.GoTo, village.Id, CreatureSituation.VillageInNeed), (mine.Activity.Action, mine.Activity.TargetSettlementId, mine.Activity.Situation));
        Assert.Equal(village.Id, Assert.Single(events.OfType<CreatureDefending>()).SettlementId);
        Assert.True(CreatureTemperament.Defends(Rules, mine));

        mine.Activity.Action = CreatureAction.Rest;
        mine.Activity.TargetSettlementId = null;
        mine.Personality.Courage = 0.2f;
        world.GreatEvents.Beast.RavagedTicks = 0;
        Assert.Empty(Step(world, 1).OfType<CreatureDefending>());

        mine.Personality.Courage = 0.9f;
        mine.Activity.Situation = CreatureSituation.Commanded;
        mine.Activity.Action = CreatureAction.GoTo;
        world.GreatEvents.Beast.RavagedTicks = 0;
        Assert.Empty(Step(world, 1).OfType<CreatureDefending>());
    }

    [Fact]
    public void WoundsCowIt_AndJudgementMovesItsJealousy()
    {
        var personality = new CreaturePersonality { Kindness = 0.5f, Aggression = 0.5f, Laziness = 0.5f, Curiosity = 0.5f };
        var (_, creature) = CreatureWorlds.Create(300f);
        creature.Personality.Courage = 0.5f;

        CreatureTemperament.Wounded(Rules, creature, 0.5f);
        CreatureTemperament.Judge(Rules, personality, CreatureAction.Attack, CreatureFeedback.Punished);

        Assert.Equal(0.5f - (0.5f * Temperament.CouragePerWound), creature.Personality.Courage, 4);
        Assert.Equal(0.5f + Temperament.JealousyPerReward, personality.Jealousy, 4);
    }

    /// <summary>Runs one-second beast steps.</summary>
    private static List<WorldEvent> Step(WorldState world, int steps)
    {
        var system = new BeastSystem(TestContent.Shipped.Disasters, SettlementWorlds.Rules, DivineWorlds.Rules, Rules, new CreatureBrain(Rules, DivineWorlds.Rules, SettlementWorlds.Rules), 10);
        var events = new List<WorldEvent>();
        for (var i = 0; i < steps; i++)
        {
            world.Tick += 10;
            system.Update(new TickContext(world, new SimulationRandom(1), events));
        }

        return events;
    }
}
