using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Minds;
using Pantheon.Game.Minds.Agents;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Xunit;

namespace Pantheon.Tests.Minds.Agents;

/// <summary>The gods' and Creatures' agents (ADR-072): who is asked and when, what is done with the answers, and when the scribes speak instead.</summary>
public sealed class AgentDirectorTests
{
    private static AgentRules Rules => AgentWorlds.Rules;

    /// <summary>A model that answers a god's question with a war, words and a journal, and a Creature's with a line and its first choice.</summary>
    private static FakeLanguageModel Model(GameSession session, int me) => new(question => question.Schema == GodAgentPrompt.Schema
        ? AgentWorlds.GodAnswer("Ilyra's beast walked in my fields. I will not forget.", [("declare_war", session.World.Gods.Single(g => g.Id == me).Name, string.Empty)], (session.World.Gods.Single(g => g.Id == me).Name, "Your beast is not welcome."))
        : AgentWorlds.CreatureAnswer("I am hungry!", "c1"));

    /// <summary>A visit of the player's Creature to the rival's home.</summary>
    private static CreatureWatched Visit(GameSession session, GodState rival, int me, WatchAnswer answer = WatchAnswer.Watch) => new()
    {
        Tick = session.World.Tick,
        GodId = rival.Id,
        VisitorGodId = me,
        CreatureId = session.World.Creatures.Single(c => c.GodId == me).Id,
        SettlementId = (GodPlanner.Home(session.World, rival.Id) ?? session.World.Settlements[0]).Id,
        Answer = answer,
    };

    private static AgentOptions With(ILanguageModel? model, int turns = 300, bool voices = true) => new(model, Thinking: true, turns, voices);

    [Fact]
    public void AWokenGod_IsAskedOnceItsHooksGather_AndItsAnswerIsTaken()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var model = Model(session, me);
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);

        agents.Update(session, [Visit(session, rival, me)], 0.1, With(model));
        Assert.Empty(model.Asked);
        agents.Update(session, [], Rules.GatherSeconds + 0.5, With(model));
        agents.Update(session, [], 0.1, With(model));

        var asked = Assert.Single(model.Asked, q => q.Schema == GodAgentPrompt.Schema);
        Assert.Contains(PersonaText.Title(rival), asked.Instructions);
        Assert.Contains("came into", asked.Turns.Single().Text);
        Assert.Equal("Ilyra's beast walked in my fields. I will not forget.", rival.Journal);
        Assert.True(DiplomacyModel.AreAtWar(session.World, rival.Id, me));
        Assert.Contains(session.DrainEvents(), e => e is GodSpoke { Text: "Your beast is not welcome." } spoke && spoke.GodId == rival.Id && spoke.ToGodId == me);
        var mind = agents.Minds.Single(m => m.Mind == Mind.God(rival.Id));
        Assert.Equal(1, mind.Turns);
        Assert.Contains(mind.LastResults, r => r.StartsWith("declare_war", System.StringComparison.Ordinal));
        Assert.True(agents.Tokens.Read >= 1000 && agents.Tokens.Written >= 100);
    }

    [Fact]
    public void AGod_IsNotAskedAgainBeforeItsTurnComes()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var model = Model(session, me);
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);
        agents.Update(session, [Visit(session, rival, me)], Rules.GatherSeconds + 0.5, With(model));
        agents.Update(session, [], Rules.GatherSeconds + 0.5, With(model));
        int Asked() => model.Asked.Count(q => q.Schema == GodAgentPrompt.Schema);
        Assert.Equal(1, Asked());

        agents.Update(session, [Visit(session, rival, me)], 5, With(model));
        agents.Update(session, [], 5, With(model));
        Assert.Equal(1, Asked());
        agents.Update(session, [], Rules.GodTurnSeconds, With(model));

        Assert.Equal(2, Asked());
    }

    [Fact]
    public void WithoutAModel_TheScribesSpeakToThePlayer_AtMostSoOften()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);
        List<GodSpoke> Spoken() => session.DrainEvents().OfType<GodSpoke>().Where(s => s.GodId == rival.Id && s.ToGodId == me).ToList();

        agents.Update(session, [Visit(session, rival, me, WatchAnswer.Repel)], 0.1, With(null));
        var first = Assert.Single(Spoken());
        agents.Update(session, [Visit(session, rival, me, WatchAnswer.Repel)], 0.1, With(null));
        Assert.Empty(Spoken());
        session.World.Tick += (long)(Rules.ScribeGodSeconds * SimulationTime.TicksPerSecond) + 1;
        agents.Update(session, [Visit(session, rival, me, WatchAnswer.Repel)], 0.1, With(null));

        Assert.Single(Spoken());
        Assert.Contains(GodPlanner.Home(session.World, rival.Id)!.Name, first.Text);
        Assert.DoesNotContain("{", first.Text);
    }

    [Fact]
    public void TheScribes_GiveTheCreaturesWords_ButNotWhenTheyAreSilenced()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var creature = session.World.Creatures.Single(c => c.GodId == me);
        var won = new CreatureFightEnded { Tick = session.World.Tick, CreatureId = creature.Id, OtherCreatureId = session.World.Creatures.Single(c => c.GodId == rival.Id).Id, Outcome = Domain.Events.FightOutcome.Spared };

        var silent = new AgentDirector(Rules, AgentWorlds.Voices);
        silent.Update(session, [won], 0.1, With(null, voices: false));
        Assert.Empty(session.DrainEvents().OfType<CreatureSpoke>());
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);
        agents.Update(session, [won], 0.1, With(null));

        var said = Assert.Single(session.DrainEvents().OfType<CreatureSpoke>(), s => s.CreatureId == creature.Id);
        Assert.DoesNotContain("{", said.Text);
    }

    [Fact]
    public void WhenAnHoursQuestionsAreSpent_TheScribesSpeakInstead()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var model = Model(session, me);
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);
        agents.Update(session, [Visit(session, rival, me)], Rules.GatherSeconds + 0.5, With(model, turns: 1));
        agents.Update(session, [], Rules.GatherSeconds + 0.5, With(model, turns: 1));
        var asked = model.Asked.Count;
        session.DrainEvents();

        var other = session.World.Gods.Last(g => g.Controller == GodController.Ai);
        agents.Update(session, [Visit(session, other, me, WatchAnswer.Repel)], Rules.GatherSeconds + 0.5, With(model, turns: 1));

        Assert.Equal(asked, model.Asked.Count);
        Assert.Equal(1, agents.AskedThisHour);
        Assert.Contains(session.DrainEvents(), e => e is GodSpoke spoke && spoke.GodId == other.Id);
    }

    [Fact]
    public void AfterThreeFailures_TheAgentsRest_AndTheScribesSpeak()
    {
        var (session, _, me) = AgentWorlds.Setup();
        var model = new FakeLanguageModel(_ => ModelAnswer.Without(ModelOutcome.Failed, "the network is down"));
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);
        var gods = session.World.Gods.Where(g => g.Controller == GodController.Ai).ToList();

        foreach (var god in gods)
        {
            agents.Update(session, [Visit(session, god, me)], Rules.GatherSeconds + 0.5, With(model));
            agents.Update(session, [], 0.1, With(model));
            agents.Update(session, [], 0.1, With(model));
        }

        Assert.True(gods.Count >= 3);
        Assert.True(agents.Resting);
        Assert.Equal("the network is down", agents.Problem);
        var asked = model.Asked.Count;
        agents.Update(session, [Visit(session, gods[0], me)], Rules.GatherSeconds + 0.5, With(model));
        Assert.Equal(asked, model.Asked.Count);
    }

    [Fact]
    public void ACreaturesAnswer_SpeaksAndChooses()
    {
        var (session, _, me) = AgentWorlds.Setup();
        var model = Model(session, me);
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);
        var creature = session.World.Creatures.Single(c => c.GodId == me);
        var first = session.Inspect(a => a.Will!.Options(session.World, creature))[0];
        creature.Needs.Hunger = 1f;

        agents.Update(session, [], 0.1, With(model));
        agents.Update(session, [], Rules.GatherSeconds + 0.5, With(model));
        agents.Update(session, [], 0.1, With(model));

        Assert.Contains("You grew hungry.", Assert.Single(model.Asked, q => q.Schema == CreatureAgentPrompt.Schema).Turns.Single().Text);
        Assert.Contains(session.DrainEvents(), e => e is CreatureSpoke { Text: "I am hungry!" } said && said.CreatureId == creature.Id);
        Assert.StartsWith("chose: ", agents.Minds.Single(m => m.Mind == Mind.Creature(creature.Id)).LastResults.Single());
        Assert.NotEqual(Domain.Creatures.CreatureAction.GoTo, first.Action);
    }

    [Fact]
    public void WordsBetweenAIGods_WaitForSomethingThatMatters()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var model = Model(session, me);
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);
        var other = session.World.Gods.Last(g => g.Controller == GodController.Ai);
        int Asked() => model.Asked.Count(q => q.Schema == GodAgentPrompt.Schema);

        agents.Update(session, [new GodSpoke { Tick = 1, GodId = other.Id, ToGodId = rival.Id, Text = "Join me against Ilyra." }], Rules.GatherSeconds + 0.5, With(model));
        agents.Update(session, [], Rules.GatherSeconds + 0.5, With(model));
        Assert.Equal(0, Asked());
        agents.Update(session, [Visit(session, rival, me)], Rules.GatherSeconds + 0.5, With(model));
        agents.Update(session, [], Rules.GatherSeconds + 0.5, With(model));

        Assert.Contains("Join me against Ilyra.", Assert.Single(model.Asked, q => q.Schema == GodAgentPrompt.Schema).Turns.Single().Text);
    }

    [Fact]
    public void AGod_LooksAboutItUnprompted_WhenItsTimeComes()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var model = Model(session, me);
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);
        agents.Update(session, [], 0.1, With(model));
        Assert.DoesNotContain(model.Asked, q => q.Schema == GodAgentPrompt.Schema);

        session.World.Tick += (long)(Rules.ReflectSeconds * SimulationTime.TicksPerSecond * 2);
        agents.Update(session, [], 0.1, With(model));

        Assert.Contains(model.Asked, q => q.Schema == GodAgentPrompt.Schema && q.Turns.Single().Text.Contains("Nothing in particular", System.StringComparison.Ordinal) && q.Instructions.Contains(rival.Name, System.StringComparison.Ordinal));
    }

    [Fact]
    public void ThePlayersGod_HasNoMind_AndAnAnswerAboutAWorldSinceReplaced_IsDropped()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var model = Model(session, me);
        var agents = new AgentDirector(Rules, AgentWorlds.Voices);
        var mine = new DiplomacyChanged { Tick = 1, ActorGodId = me, OtherGodId = rival.Id, Kind = Domain.Diplomacy.DiplomaticCommandKind.DeclareWar, Status = Domain.Diplomacy.DiplomaticStatus.War, Betrayal = false };

        agents.Update(session, [mine], Rules.GatherSeconds + 0.5, With(model));
        agents.Update(session, [], Rules.GatherSeconds + 0.5, With(model));
        Assert.DoesNotContain(model.Asked, q => q.Instructions.Contains(session.World.Gods.Single(g => g.Id == me).Name + ",", System.StringComparison.Ordinal) && q.Schema == GodAgentPrompt.Schema && q.Instructions.StartsWith("You are " + session.World.Gods.Single(g => g.Id == me).Name, System.StringComparison.Ordinal));
        Assert.Contains(model.Asked, q => q.Schema == GodAgentPrompt.Schema && q.Instructions.Contains(rival.Name, System.StringComparison.Ordinal));
        session.StartNewWorld(new WorldSeed(78));
        agents.Update(session, [], 0.1, With(model));

        Assert.All(session.World.Gods, god => Assert.Null(god.Journal));
        Assert.DoesNotContain(agents.Minds, m => m.Turns > 0);
    }
}
