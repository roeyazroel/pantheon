using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Randomness;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Gods;

/// <summary>The AI gods' major planning and long-term goals (spec §47, §87, ADR-036).</summary>
public sealed class PlannerTests
{
    private static readonly PlanningRules Rules = RivalWorlds.AiRules.Planning;
    private static readonly GodPlanner Planner = new(RivalWorlds.AiRules, DiplomacyWorlds.Rules with { Acquaintance = DiplomacyWorlds.Acquaintance });

    [Fact]
    public void TheBeastAtItsDoor_MakesAGodProtectItsHome_AndSetOutToSlayIt_HoldingAtMostMaxGoals()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.8f, 0.6f);
        var guardian = world.Gods[2];
        Wake(world, world.Settlements[2]);
        world.Settlements[2].Beliefs.Single(b => b.GodId == 3).Doubt = 0.6f;
        var events = new List<WorldEvent>();

        var options = Planner.Options(world, guardian, PantheonWorlds.Profile(GodPersonality.Guardian));
        Planner.PlanIfDue(world, guardian, PantheonWorlds.Profile(GodPersonality.Guardian), events);

        Assert.Equal((GodGoalKind.ProtectHome, world.Settlements[2].Id), (options[0].Kind, options[0].SettlementId));
        Assert.Contains("from Gharos", options[0].Reason, System.StringComparison.Ordinal);
        Assert.Equal(Rules.MaxGoals, guardian.Goals.Count);
        Assert.Equal([GodGoalKind.ProtectHome, GodGoalKind.SlayBeast, GodGoalKind.Recover], guardian.Goals.Select(g => g.Kind).Order());
        Assert.DoesNotContain(guardian.Goals, g => g.Kind == GodGoalKind.Convert);
        Assert.Equal(Rules.MaxGoals, events.OfType<GodGoalTaken>().Count());
        Assert.Equal(world.Tick + (long)(Rules.PlanSeconds * 10), guardian.NextPlanTick);
    }

    [Fact]
    public void AGodSetsOutToConvert_TheFullestVillageWithinReach_ThatNeitherItNorAnAllyLeads()
    {
        // The Trickster's village is at x = 950: x = 700 and x = 450 are within reach, x = 200 is not.
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        var trickster = world.Gods[3];
        var profile = PantheonWorlds.Profile(GodPersonality.Trickster);
        world.Settlements[2].Population = 25;

        var first = Planner.Options(world, trickster, profile).Single(o => o.Kind == GodGoalKind.Convert);
        PantheonWorlds.SetStatus(world, 2, 4, DiplomaticStatus.Alliance);
        var allied = Planner.Options(world, trickster, profile).Single(o => o.Kind == GodGoalKind.Convert);

        Assert.Equal(world.Settlements[1].Id, first.SettlementId);
        Assert.Equal(world.Settlements[2].Id, allied.SettlementId);
        Assert.True(allied.Weight < first.Weight);
        Assert.Null(Planner.Score(world, trickster, profile, GodGoalKind.Convert, world.Settlements[0].Id, null));
        Assert.Null(Planner.Score(world, trickster, profile, GodGoalKind.Convert, world.Settlements[3].Id, null));
    }

    [Fact]
    public void AGodBefriendsOrWeakens_OnlyTheGodsItKnows_AndWeakensTheOneItFights()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.9f, 0.6f);
        var tyrant = world.Gods[1];
        var profile = PantheonWorlds.Profile(GodPersonality.Tyrant);

        Assert.DoesNotContain(Planner.Options(world, tyrant, profile), o => o.Kind is GodGoalKind.Befriend or GodGoalKind.Weaken);
        Meetings.All(world);
        var leader = Planner.Options(world, tyrant, profile).Single(o => o.Kind == GodGoalKind.Weaken);
        PantheonWorlds.SetStatus(world, 2, 4, DiplomaticStatus.War);
        var options = Planner.Options(world, tyrant, profile);

        Assert.Equal(3, leader.GodId);
        Assert.Equal(4, options.Single(o => o.Kind == GodGoalKind.Weaken).GodId);

        // Gods at rest are cool to each other (starting rivalry and suspicion): that is neither friendship nor hate.
        var guardian = world.Gods[2];
        var cooperation = PantheonWorlds.Profile(GodPersonality.Guardian).Cooperation;
        var stranger = Planner.Score(world, guardian, PantheonWorlds.Profile(GodPersonality.Guardian), GodGoalKind.Befriend, null, 1)!.Weight;
        Pantheon.Simulation.Diplomacy.DiplomacyModel.EnsureRelation(DiplomacyWorlds.Rules, world, 3, 1);
        Assert.Equal((cooperation * 0.5f, stranger), (stranger, Planner.Score(world, guardian, PantheonWorlds.Profile(GodPersonality.Guardian), GodGoalKind.Befriend, null, 1)!.Weight));
        Assert.Null(Planner.Score(world, guardian, PantheonWorlds.Profile(GodPersonality.Guardian), GodGoalKind.Weaken, null, 1));
        Assert.NotEqual(4, options.Single(o => o.Kind == GodGoalKind.Befriend).GodId);
        Assert.Contains("at war", options.Single(o => o.Kind == GodGoalKind.Weaken).Reason, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ThePlagueAndADrought_MakeAGodPrepare_AndDoubtMakesItWinBackItsFollowers()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.8f, 0.6f);
        var guardian = world.Gods[2];
        var profile = PantheonWorlds.Profile(GodPersonality.Guardian);
        Assert.DoesNotContain(Planner.Options(world, guardian, profile), o => o.Kind is GodGoalKind.Prepare or GodGoalKind.Recover);

        world.Settlements[0].Plague = 0.3f;
        world.Settlements[2].Beliefs.Single(b => b.GodId == 3).Doubt = 0.5f;
        var options = Planner.Options(world, guardian, profile);

        Assert.Equal(profile.Protect, options.Single(o => o.Kind == GodGoalKind.Prepare).Weight, 3);
        Assert.Contains("plague", options.Single(o => o.Kind == GodGoalKind.Prepare).Reason, System.StringComparison.Ordinal);
        Assert.Equal(1f, options.Single(o => o.Kind == GodGoalKind.Recover).Weight, 3);

        world.Settlements[0].Plague = 0f;
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0f;
        }

        var (plague, drought) = GodPlanner.Hardships(world, Rules);
        Assert.Equal((0f, 1f), (plague, drought));
        Assert.Contains("drought", Planner.Options(world, guardian, profile).Single(o => o.Kind == GodGoalKind.Prepare).Reason, System.StringComparison.Ordinal);
    }

    [Fact]
    public void APlan_HoldsAYoungGoal_DropsAMootOne_AndRethinksAnOldOne()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.8f, 0.6f);
        var trickster = world.Gods[3];
        var profile = PantheonWorlds.Profile(GodPersonality.Trickster);
        Wake(world, world.Settlements[3]);
        Planner.PlanIfDue(world, trickster, profile, []);
        Assert.Contains(trickster.Goals, g => g.Kind == GodGoalKind.SlayBeast);
        var convert = trickster.Goals.Single(g => g.Kind == GodGoalKind.Convert);

        // Half a minute on: the beast fell, and another village is now the better one to convert.
        world.Tick = trickster.NextPlanTick;
        world.GreatEvents.Beast!.Phase = BeastPhase.Slain;
        var target = world.Settlements.Single(s => s.Id == convert.SettlementId);
        var other = world.Settlements[1].Id == target.Id ? world.Settlements[2] : world.Settlements[1];
        target.Population = 20;
        Planner.PlanIfDue(world, trickster, profile, []);

        Assert.DoesNotContain(trickster.Goals, g => g.Kind == GodGoalKind.SlayBeast);
        Assert.Same(convert, trickster.Goals.Single(g => g.Kind == GodGoalKind.Convert));

        world.Tick = convert.SinceTick + (long)(Rules.MinGoalSeconds * 10);
        trickster.NextPlanTick = world.Tick;
        var events = new List<WorldEvent>();
        Planner.PlanIfDue(world, trickster, profile, events);

        Assert.Equal(other.Id, trickster.Goals.Single(g => g.Kind == GodGoalKind.Convert).SettlementId);
        Assert.Contains(events.OfType<GodGoalTaken>(), e => e.Kind == GodGoalKind.Convert && e.SettlementId == other.Id);
        Planner.PlanIfDue(world, trickster, profile, events);
        Assert.Single(events);
    }

    [Fact]
    public void Goals_BendTheGodsChoices_ByTheirWeight()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.8f, 0.6f);
        var god = world.Gods[3];
        var (target, other) = (world.Settlements[1], world.Settlements[2]);
        god.Goals.Add(new GodGoal { Kind = GodGoalKind.Convert, SettlementId = target.Id, SinceTick = 0, Weight = 0.5f });
        god.Goals.Add(new GodGoal { Kind = GodGoalKind.Befriend, GodId = 3, SinceTick = 0, Weight = 0.8f });
        var candidates = new List<AiCandidate>
        {
            new(AiActionKind.CastMiracle, "heal", target.Id, target.Position, 1f, 10f, 0.4f, "convert"),
            new(AiActionKind.CastMiracle, "heal", other.Id, other.Position, 1f, 10f, 0.4f, "convert"),
            new(AiActionKind.Wait, null, null, default, 0.12f, 0f, 0.12f, "save power"),
        };

        GoalFocus.Apply(Rules, world, god, DivineWorlds.Miracles, candidates);

        Assert.Equal(0.4f * (1f + ((Rules.Boost - 1f) * 0.5f)), candidates[0].Utility, 4);
        Assert.EndsWith(", goal: convert " + target.Name, candidates[0].Reason, System.StringComparison.Ordinal);
        Assert.Equal((0.4f, 0.12f), (candidates[1].Utility, candidates[2].Utility));
        Assert.Equal(Rules.DiplomacyBias * 0.8f, GoalFocus.Diplomacy(Rules, god, 3, DiplomaticCommandKind.OfferAlliance), 4);
        Assert.Equal(-Rules.DiplomacyBias * 0.8f, GoalFocus.Diplomacy(Rules, god, 3, DiplomaticCommandKind.DeclareWar), 4);
        Assert.Equal(0f, GoalFocus.Diplomacy(Rules, god, 2, DiplomaticCommandKind.DeclareWar));
        Assert.Null(GoalFocus.Preparing(Rules, world, god));

        god.Goals.Add(new GodGoal { Kind = GodGoalKind.Prepare, SinceTick = 0, Weight = 0.6f });
        world.Settlements[0].Plague = 0.2f;
        Assert.Equal(SettlementPriority.Health, GoalFocus.Preparing(Rules, world, god));
    }

    [Fact]
    public void AnAiGod_PlansOnItsTurn_AndSmitesTheBeastItMeansToSlay()
    {
        var world = RivalWorlds.Create(GodPersonality.Warlord, 3000f, 200f, 450f);
        var rival = world.Gods.Single(g => g.Id == RivalWorlds.RivalId);
        rival.Rank = DivineRank.God;
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.9f, faith: 0.9f, godId: RivalWorlds.RivalId);
        Wake(world, world.Settlements[1]);
        var ai = RivalWorlds.CreateAi();
        Assert.DoesNotContain(ai.Evaluate(world, rival), c => c.Reason.StartsWith("smite", System.StringComparison.Ordinal));
        var events = new List<WorldEvent>();

        ai.Act(world, rival, new SimulationRandom(1), events);

        Assert.Contains(rival.Goals, g => g.Kind == GodGoalKind.SlayBeast);
        Assert.Contains(events.OfType<GodGoalTaken>(), e => e.GodId == RivalWorlds.RivalId && e.Kind == GodGoalKind.SlayBeast);
        var smote = events.OfType<MiracleCast>().Single();
        Assert.Equal(("lightning", world.GreatEvents.Beast!.Position), (smote.MiracleId, smote.Target));
    }

    /// <summary>Wakes a beast that hunts a village, standing at it.</summary>
    private static void Wake(WorldState world, Pantheon.Domain.Settlements.SettlementState prey) =>
        world.GreatEvents.Beast = new BeastState
        {
            Name = "Gharos the Ancient", LairRegionId = 0, Phase = BeastPhase.Awake, Position = prey.Position, Health = 100f, MaxHealth = 100f,
            TargetSettlementId = prey.Id, WokeTick = world.Tick, RavagedTicks = 0,
        };
}
