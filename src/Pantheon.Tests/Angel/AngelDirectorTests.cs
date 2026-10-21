using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Game;
using Pantheon.Game.Angel;
using Pantheon.Game.Minds;
using Pantheon.Simulation.Gods;
using Pantheon.Tests.Minds;
using Pantheon.Tests.Minds.Agents;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Angel;

/// <summary>When the angel speaks and asks its model, what is done with its answers, and when the scribes speak instead (ADR-074).</summary>
public sealed class AngelDirectorTests
{
    private static AngelContent Content => TestContent.Angel;

    private static AngelTiming Timing => Content.Timing;

    private static (GameSession Session, GodState God, AngelDirector Angel, List<AngelWords> Said) Setup()
    {
        var (session, _, me) = AgentWorlds.Setup();
        var god = session.World.Gods.Single(g => g.Id == me);
        god.DivinePower = 5000f;
        var angel = new AngelDirector(Content);
        var said = new List<AngelWords>();
        angel.Spoke += said.Add;
        return (session, god, angel, said);
    }

    private static AngelTerms Scribes(bool hushed = false) => new(null, Shown: true, Thinking: true, Chatter: 1, hushed);

    private static AngelTerms With(ILanguageModel model) => new(model, Shown: true, Thinking: true, Chatter: 1);

    private static MiracleCast Food(GodState god, int village) => new() { Tick = 9, GodId = god.Id, MiracleId = "food", Target = default, Cost = 40f, AffectedSettlementIds = [village], Deaths = 0 };

    private static ModelAnswer Answer(string say, params string[] ids) =>
        ModelAnswer.Of(new JsonObject { ["say"] = say, ["suggestions"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]) }.ToJsonString(), 800, 40);

    [Fact]
    public void ItGreetsItsGod_SoonAfterTheWorldBegins_ByTheScribesWithoutAModel_InItsName()
    {
        var (session, god, angel, said) = Setup();

        angel.Update(session, [], 0.1, Scribes());
        Assert.Empty(said);
        angel.Update(session, [], Timing.GatherSeconds + 0.5, Scribes());

        var words = Assert.Single(said);
        Assert.Equal(AngelMoment.Welcome, words.Hook.Moment);
        Assert.Equal(Content.NameFor(session.World.Seed, god.Id), angel.Name);
        Assert.Contains(angel.Name, words.Text, System.StringComparison.Ordinal);
        Assert.False(words.FromModel);
        Assert.True(words.ShowSeconds >= Timing.ShowSeconds);
    }

    [Fact]
    public void AfterItSpeaks_ItWaitsItsTurn_ThenSpeaksOfTheGodsDeed()
    {
        var (session, god, angel, said) = Setup();
        angel.Update(session, [], 0.1, Scribes());
        angel.Update(session, [], Timing.GatherSeconds + 0.5, Scribes());
        var home = GodPlanner.Home(session.World, god.Id)!.Id;

        angel.Update(session, [Food(god, home)], 1, Scribes());
        angel.Update(session, [], Timing.GatherSeconds + 1, Scribes());
        Assert.Single(said);
        angel.Update(session, [], Timing.WordsSeconds[1], Scribes());

        Assert.Equal(AngelMoment.DeedKind, said[^1].Hook.Moment);
        Assert.NotEmpty(said[^1].Suggestions);
    }

    [Fact]
    public void ALongQuiet_BringsItsCounsel_ButNotWhileTheWorldIsPaused()
    {
        var (session, _, angel, said) = Setup();
        angel.Update(session, [], 0.1, Scribes());
        angel.Update(session, [], Timing.GatherSeconds + 0.5, Scribes());
        session.Scheduler.IsPaused = true;

        angel.Update(session, [], Timing.QuietSeconds[1] + 1, Scribes());
        angel.Update(session, [], Timing.GatherSeconds + 1, Scribes());
        Assert.Single(said);
        session.Scheduler.IsPaused = false;
        angel.Update(session, [], 0.1, Scribes());
        angel.Update(session, [], Timing.GatherSeconds + 1, Scribes());

        Assert.Equal(AngelMoment.Quiet, said[^1].Hook.Moment);
    }

    [Fact]
    public void ItsGodsCall_IsAnsweredAtOnce_EvenPausedOrHushed_AndWithTheGreetingWhenNoneWasGiven()
    {
        var (session, _, angel, said) = Setup();
        session.Scheduler.IsPaused = true;

        angel.Update(session, [], 0.1, Scribes(hushed: true));
        angel.Call(session.World.Tick, "What now?");
        angel.Update(session, [], Timing.CallSeconds + 0.1, Scribes(hushed: true));

        var words = Assert.Single(said);
        Assert.Equal(AngelMoment.Called, words.Hook.Moment);
        angel.Update(session, [], 600, Scribes(hushed: true));
        Assert.Single(said);
    }

    [Fact]
    public void WithAModel_ItAsksIt_AndTakesItsWordsAndTheDeedsItUrges()
    {
        var (session, _, angel, said) = Setup();
        var model = new FakeLanguageModel(q => q.Schema == AngelPrompt.Schema ? Answer("Feed them, little god.", "d1") : ModelAnswer.Without(ModelOutcome.Failed, "not mine"));

        angel.Call(session.World.Tick, "Where do I begin?");
        angel.Update(session, [], Timing.CallSeconds + 0.1, With(model));
        Assert.Empty(said);
        angel.Update(session, [], 0.1, With(model));

        var question = Assert.Single(model.Asked);
        Assert.Contains("Your god asks you: “Where do I begin?”", question.Turns.Single().Text, System.StringComparison.Ordinal);
        var words = Assert.Single(said);
        Assert.True(words.FromModel);
        Assert.Equal("Feed them, little god.", words.Text);
        Assert.Equal("d1", Assert.Single(words.Suggestions).Id);
        Assert.Equal(1, angel.Turns);
        Assert.Equal((800L, 40L), angel.Tokens);
    }

    [Fact]
    public void WhenItsModelFails_TheScribesSpeak_AndAfterThreeFailures_ItRests()
    {
        var (session, _, angel, said) = Setup();
        var model = new FakeLanguageModel(_ => ModelAnswer.Without(ModelOutcome.Failed, "the provider is down"));

        for (var i = 0; i < 3; i++)
        {
            angel.Call(session.World.Tick, null);
            angel.Update(session, [], Timing.CallSeconds + 0.1, With(model));
            angel.Update(session, [], 0.1, With(model));
        }

        Assert.Equal(3, said.Count);
        Assert.All(said, w => Assert.False(w.FromModel));
        Assert.Equal("the provider is down", angel.Problem);
        Assert.True(angel.Resting);
        angel.Call(session.World.Tick, null);
        angel.Update(session, [], Timing.CallSeconds + 0.1, With(model));
        Assert.Equal(3, model.Asked.Count);
        Assert.Equal(4, said.Count);
    }

    [Fact]
    public void AsItsGodsDeedsChangeIt_ItSpeaksOfIt()
    {
        var (session, god, angel, said) = Setup();
        angel.Update(session, [], 0.1, Scribes());
        angel.Update(session, [], Timing.GatherSeconds + 0.5, Scribes());
        Assert.Equal(AngelStage.Unformed, angel.Look.Stage);

        god.Conduct.Help = 40f;
        angel.Update(session, [], 1, Scribes());
        angel.Update(session, [], Timing.WordsSeconds[1], Scribes());

        Assert.Equal(AngelStage.Radiant, angel.Look.Stage);
        Assert.Equal(AngelMoment.StageRose, said[^1].Hook.Moment);
        Assert.Contains("radiant", said[^1].Hook.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void KeptAway_ItSaysNothing_AndHeldAtAStage_ItLooksIt()
    {
        var (session, _, angel, said) = Setup();
        angel.ForcedStage = AngelStage.Fallen;

        angel.Update(session, [], 0.1, Scribes() with { Shown = false });
        angel.Update(session, [], 900, Scribes() with { Shown = false });

        Assert.Empty(said);
        Assert.Empty(angel.Waiting);
        Assert.Equal(AngelStage.Fallen, angel.Look.Stage);
    }

    [Fact]
    public void ForANewWorld_ItForgetsWhatItSaid_AndGreetsItsGodAgain()
    {
        var (session, _, angel, said) = Setup();
        angel.Update(session, [], 0.1, Scribes());
        angel.Update(session, [], Timing.GatherSeconds + 0.5, Scribes());

        angel.Clear();
        angel.Update(session, [], 0.1, Scribes());
        angel.Update(session, [], Timing.GatherSeconds + 0.5, Scribes());

        Assert.Equal([AngelMoment.Welcome, AngelMoment.Welcome], said.Select(w => w.Hook.Moment));
        Assert.Null(angel.Problem);
    }
}
