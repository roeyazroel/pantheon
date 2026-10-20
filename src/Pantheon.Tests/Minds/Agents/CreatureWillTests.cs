using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game.Minds.Agents;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Gods;
using Xunit;

namespace Pantheon.Tests.Minds.Agents;

/// <summary>A Creature's own will at the moments that matter (ADR-072): it chooses among what its brain would weigh, never against its god's errand.</summary>
public sealed class CreatureWillTests
{
    [Fact]
    public void ACreature_IsOfferedItsBrainsOptions_EachOnce_NoErrands()
    {
        var (session, _, me) = AgentWorlds.Setup();
        var creature = session.World.Creatures.Single(c => c.GodId == me);

        var options = session.Inspect(a => a.Will!.Options(session.World, creature));

        Assert.InRange(options.Count, 1, CreatureWill.MaxOptions);
        Assert.DoesNotContain(options, o => o.Action == CreatureAction.GoTo);
        Assert.Equal(options.Count, options.Select(o => (o.Action, o.SettlementId, o.CreatureId)).Distinct().Count());
    }

    [Fact]
    public void AChoiceAmongItsOptions_IsCarriedOut_TheSameWayForTheSameWorld()
    {
        var (session, _, me) = AgentWorlds.Setup();
        var creature = session.World.Creatures.Single(c => c.GodId == me);
        var choice = session.Inspect(a => a.Will!.Options(session.World, creature)).Last();
        var twin = Pantheon.Persistence.WorldSaveSerializer.Deserialize(Pantheon.Persistence.WorldSaveSerializer.Serialize(session.World));

        var rejection = session.Command((a, _) => a.Will!.Choose(session.World, creature.Id, choice.Action, choice.SettlementId, choice.CreatureId));
        var again = session.Inspect(a => a.Will!.Choose(twin, creature.Id, choice.Action, choice.SettlementId, choice.CreatureId));

        Assert.Equal(WillRejection.None, rejection);
        Assert.Equal(WillRejection.None, again);
        Assert.Equal(choice.Action, creature.Activity.Action);
        Assert.Equal(creature.Activity.Target, twin.Creatures.Single(c => c.Id == creature.Id).Activity.Target);
    }

    [Fact]
    public void ACreature_DoesNotOverruleItsGodsErrand_NorChooseWhatItCannot()
    {
        var (session, _, me) = AgentWorlds.Setup();
        var world = session.World;
        var creature = world.Creatures.Single(c => c.GodId == me);
        var choice = session.Inspect(a => a.Will!.Options(world, creature))[0];

        Assert.Equal(WillRejection.NotAnOption, session.Inspect(a => a.Will!.Choose(world, creature.Id, CreatureAction.Attack, -5, null)));
        Assert.Equal(WillRejection.UnknownCreature, session.Inspect(a => a.Will!.Choose(world, 999, choice.Action, choice.SettlementId, choice.CreatureId)));
        session.CommandCreatureAs(me, CreatureCommandKind.GoTo, GodPlanner.Home(world, world.Gods.First(g => g.Id != me).Id)!.Position);
        Assert.Equal(WillRejection.Obeying, CreatureWill.CanChoose(world, creature));
        Assert.Empty(session.Inspect(a => a.Will!.Options(world, creature)));
        Assert.Equal(WillRejection.Obeying, session.Inspect(a => a.Will!.Choose(world, creature.Id, choice.Action, choice.SettlementId, choice.CreatureId)));
        creature.Activity.Phase = CreaturePhase.Banished;
        Assert.Equal(WillRejection.NotFree, CreatureWill.CanChoose(world, creature));
    }

    [Fact]
    public void ACreaturesTurn_TellsItsLifeAndChoices_AndItsAnswerIsRead()
    {
        var (session, _, me) = AgentWorlds.Setup();
        var creature = session.World.Creatures.Single(c => c.GodId == me);
        var options = session.Inspect(a => a.Will!.Options(session.World, creature));

        var question = CreatureAgentPrompt.Question(session, creature, [new Hook(HookKind.Need, 1, "You grew hungry.", "Need.Hungry")], options, AgentWorlds.Rules);
        var turn = question.Turns.Single().Text;

        Assert.Contains(creature.Name, question.Instructions);
        Assert.Contains("You grew hungry.", turn);
        Assert.Contains("c1:", turn);
        Assert.Contains(CreatureAgentPrompt.Id(options.Count - 1) + ":", turn);
        Assert.Contains("none: go on as you are", turn);
        Assert.Equal(new CreatureTurn("Bramble hungry!", "c2"), CreatureAgentPrompt.Read(AgentWorlds.CreatureAnswer("“Bramble hungry!”", "c2").Json));
        Assert.Null(CreatureAgentPrompt.Read("{oops"));
    }

    [Fact]
    public void AGodsOwnAims_AreHeldFromThePlanner_UntilTheirTimeIsUp()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var engine = session.Content.CreateEngine();
        var profile = engine.Gods!.Ai.ProfileOf(rival)!;
        var hold = 600L;

        var taken = session.Command((_, events) => GodAims.Set(session.World, rival, engine.Gods.Ai.Planner, profile, [new GodAim(Domain.Gods.GodGoalKind.Weaken, null, me)], 0.9f, hold, events));
        engine.Gods.Ai.Planner.PlanIfDue(session.World, rival, profile, []);

        Assert.Equal([true], taken);
        Assert.Equal(Domain.Gods.GodGoalKind.Weaken, Assert.Single(rival.Goals).Kind);
        Assert.Equal(0.9f, rival.Goals[0].Weight, 3);
        Assert.Equal(session.World.Tick + hold, rival.NextPlanTick);
    }
}
