using System;
using System.Linq;
using System.Text.RegularExpressions;
using Pantheon.Domain.Events;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Launch;
using Pantheon.Game.Minds;
using Pantheon.Game.Minds.Agents;
using Pantheon.Game.Settings;
using Pantheon.Oracle;
using Pantheon.Persistence;
using Pantheon.Simulation.Gods;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Minds.Agents;

/// <summary>The agents' content, settings, launch options, words to the player and debug lines (ADR-072).</summary>
public sealed class AgentContentTests
{
    [Fact]
    public void TheShippedVoices_HaveLinesForEveryMoment_UsingOnlyKnownNames()
    {
        var voices = AgentWorlds.Voices;
        var names = new[] { "{name}", "{god}", "{other}", "{village}", "{creature}" };

        Assert.Equal(VoiceLines.GodMoments.Order(StringComparer.Ordinal), voices.Gods.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(VoiceLines.CreatureMoments.Order(StringComparer.Ordinal), voices.Creatures.Keys.Order(StringComparer.Ordinal));
        Assert.All(voices.Gods.Values.Concat(voices.Creatures.Values).SelectMany(l => l), line =>
            Assert.All(Regex.Matches(line, "\\{[a-z]+\\}").Select(m => m.Value), used => Assert.Contains(used, names)));
    }

    [Theory]
    [InlineData("\"GodTurnSeconds\": 40.0", "\"GodTurnSeconds\": 1.0")]
    [InlineData("\"MaxActions\": 3", "\"MaxActions\": 12")]
    [InlineData("\"QuickClaudeModel\": \"claude-haiku-5-5\"", "\"QuickClaudeModel\": \"claude-nonsense\"")]
    [InlineData("\"Grew\": [\"{name} bigger now!", "\"Growing\": [\"{name} bigger now!")]
    [InlineData("\"Met\": [", "\"Met\": [], \"Unused\": [")]
    public void BrokenAgents_AreRefused(string shipped, string broken)
    {
        var text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestContent.DataDirectory, MindContent.FileName));
        Assert.Contains(shipped, text, StringComparison.Ordinal);

        Assert.Throws<ContentFormatException>(() => MindContent.Parse(text.Replace(shipped, broken, StringComparison.Ordinal)));
    }

    [Fact]
    public void TheScribes_FillTheNames_TheSameWayForTheSameMoment()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var world = session.World;
        var visitor = world.Creatures.Single(c => c.GodId == me);
        var village = GodPlanner.Home(world, rival.Id)!;
        var hook = new Hook(HookKind.Visitor, 5, "visit", "Visitor.Watch", me, visitor.Id, village.Id);

        var line = AgentScribe.GodLine(AgentWorlds.Voices, world, rival, hook)!;

        Assert.Equal(line, AgentScribe.GodLine(AgentWorlds.Voices, world, rival, hook));
        Assert.DoesNotContain("{", line);
        Assert.True(line.Contains(visitor.Name, StringComparison.Ordinal) || line.Contains(village.Name, StringComparison.Ordinal));
        Assert.Null(AgentScribe.GodLine(AgentWorlds.Voices, world, rival, hook with { Voice = string.Empty }));
        Assert.DoesNotContain("{", AgentScribe.CreatureLine(AgentWorlds.Voices, world, visitor, new Hook(HookKind.Need, 5, "hungry", "Need.Hungry"))!);
    }

    [Fact]
    public void TheAgentsSettings_DefaultOn_AndComeBackIntoRange()
    {
        var settings = new GameSettings();

        Assert.True(settings.Agents && settings.AgentsQuick && settings.Voices);
        Assert.Equal(GameSettings.DefaultAgentTurns, settings.AgentTurnsPerHour);
        Assert.Equal(GameSettings.MaxAgentTurns, (settings with { AgentTurnsPerHour = 999_999 }).Clamped().AgentTurnsPerHour);
        var changed = settings with { Agents = false, AgentsQuick = false, AgentTurnsPerHour = 60, Voices = false };
        Assert.Equal(changed, GameSettings.Parse(changed.Serialize()));
    }

    [Fact]
    public void TheLaunchOptions_TurnTheAgentsOnOrOff_AndWakeAGod()
    {
        Assert.True(LaunchOptions.Parse(["--agents=on"]).Agents);
        Assert.False(LaunchOptions.Parse(["--agents=off"]).Agents);
        Assert.Null(LaunchOptions.Parse([]).Agents);
        Assert.Equal(3, LaunchOptions.Parse(["--wake=3"]).Wake);
        Assert.Equal(0, LaunchOptions.Parse(["--wake=0"]).Wake);
        Assert.Contains("--agents", Assert.Single(LaunchOptions.Parse(["--agents=maybe"]).Warnings), StringComparison.Ordinal);
        Assert.Contains("--wake", Assert.Single(LaunchOptions.Parse(["--wake=Vael"]).Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void TheAgents_AskTheQuickModel_UnlessThePlayerPrefersTheirOwn_AndNoneWhenOff()
    {
        var minds = TestContent.Minds;
        var keys = new MindKeys("sk-ant-test", "sk-openai-test", "AIza-test");
        var claude = new GameSettings { Minds = MindProvider.Claude };

        using var quick = (AnthropicLanguageModel)MindFactory.OpenAgents(claude, keys, minds, out _)!;
        using var own = (AnthropicLanguageModel)MindFactory.OpenAgents(claude with { AgentsQuick = false }, keys, minds, out _)!;
        using var gpt = (OpenAILanguageModel)MindFactory.OpenAgents(claude with { Minds = MindProvider.OpenAI }, keys, minds, out _)!;

        Assert.Equal("Claude Haiku 5.5", quick.Name);
        Assert.Equal(minds.ClaudeModels[0].Name, own.Name);
        Assert.Equal("GPT-6 Luna", gpt.Name);
        Assert.Null(MindFactory.OpenAgents(claude with { Agents = false }, keys, minds, out var none));
        Assert.Null(none);
        Assert.Null(MindFactory.OpenAgents(new GameSettings(), keys, minds, out _));
    }

    [Fact]
    public void AGodsWordsToThePlayer_AreAnAlert_AndTheirTalkHearsThem()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var feed = new Pantheon.Game.Alerts.AlertFeed();
        var words = new GodSpoke { Tick = 3, GodId = rival.Id, ToGodId = me, Text = "Your beast is not welcome." };

        var alert = Assert.Single(feed.Push([words, words with { ToGodId = rival.Id + 1 }], session.World, me, 1.0));
        var talks = new Talks(TestContent.Minds.Talk);
        talks.Heard(session, rival, me, words.Text);
        var talk = talks.Open(session, rival, me);
        rival.Journal = "Ilyra is a threat.";
        talks.Ask(session, talk, me, "Why so cold?");

        Assert.Contains(rival.Name + ": “Your beast is not welcome.”", alert.Text, StringComparison.Ordinal);
        Assert.Contains(talk.Lines, l => l.Speaker == TalkSpeaker.God && l.Text.Contains("not welcome", StringComparison.Ordinal));
        Assert.Contains("you sent them word: “Your beast is not welcome.”", talk.Turns[^1].Text, StringComparison.Ordinal);
        Assert.Contains("What you keep in mind: Ilyra is a threat.", talk.Turns[^1].Text, StringComparison.Ordinal);
        Assert.Empty(talk.Sent);
    }

    [Fact]
    public void TheDebugLines_TellWhoThinks_AndEachMindsLastTurn()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var model = new FakeLanguageModel(_ => AgentWorlds.GodAnswer("Watch Ilyra.", [("aim", session.World.Gods.Single(g => g.Id == me).Name, "weaken")]));
        var agents = new AgentDirector(AgentWorlds.Rules, AgentWorlds.Voices);
        agents.Wake(Mind.God(rival.Id), new Hook(HookKind.Reflect, 1, "It is time to look about you."));
        agents.Update(session, [], 0.1, new AgentOptions(model, true, 300, true));
        agents.Update(session, [], 0.1, new AgentOptions(model, true, 300, true));

        var lines = AgentReadout.Lines(agents, session.World, model.Name, 300);

        Assert.True(lines[0].StartsWith("Minds: Fake Model; 1/300 questions this hour", StringComparison.Ordinal), lines[0]);
        Assert.Contains(lines, l => l.Contains(rival.Name + " (God): idle, 1 turns", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("aim", StringComparison.Ordinal) && l.Contains("taken", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.EndsWith("keeps in mind: Watch Ilyra.", StringComparison.Ordinal));
        Assert.Equal(["Minds: the scribes; 0/300 questions this hour; tokens 0 read, 0 written"], AgentReadout.Lines(new AgentDirector(AgentWorlds.Rules, AgentWorlds.Voices), session.World, null, 300));
    }
}
