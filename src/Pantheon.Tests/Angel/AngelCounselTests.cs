using System;
using System.Linq;
using System.Text.Json.Nodes;
using Pantheon.Domain.Gods;
using Pantheon.Game;
using Pantheon.Game.Angel;
using Pantheon.Game.Minds;
using Pantheon.Simulation.Gods;
using Pantheon.Tests.Minds.Agents;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Angel;

/// <summary>What the angel may urge, what its model is told and how its answer is read, and the scribes' words for it (ADR-074).</summary>
public sealed class AngelCounselTests
{
    private static AngelContent Content => TestContent.Angel;

    private static (GameSession Session, GodState God, GodState Rival, int Home) Setup()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var god = session.World.Gods.Single(g => g.Id == me);
        god.DivinePower = 5000f;
        return (session, god, rival, GodPlanner.Home(session.World, me)!.Id);
    }

    [Fact]
    public void TheDeedsOffered_AreWhatTheGodCanDoNow_KindAndHarsh_EachWithItsId()
    {
        var (session, god, _, _) = Setup();

        var choices = AngelChoices.Of(session, god, [], Content.MaxChoices);

        Assert.Contains(choices, c => c.Deed == AngelDeed.Cast && c.Kindness > 0);
        Assert.Contains(choices, c => c.Deed == AngelDeed.Cast && c.Kindness < 0);
        Assert.Equal(Enumerable.Range(0, choices.Count).Select(AngelChoices.IdOf), choices.Select(c => c.Id));
        Assert.True(choices.Count <= Content.MaxChoices);
        Assert.All(choices.Where(c => c.Deed == AngelDeed.Cast), c => Assert.True(c.MiracleId is not null && c.SettlementId is not null && session.Content.Miracles.Find(c.MiracleId) is not null));
    }

    [Fact]
    public void AHungryVillage_TheGodsVisitor_AndAnOffer_ComeFirst()
    {
        var (session, god, rival, home) = Setup();
        var visitor = session.World.Creatures.Single(c => c.GodId == rival.Id);
        AngelHook[] hooks =
        [
            new(AngelMoment.VillageHungry, 1, "hungry", home),
            new(AngelMoment.Visitor, 1, "visitor", home, rival.Id, visitor.Id),
            new(AngelMoment.Offer, 1, "offer", GodId: rival.Id),
        ];

        var choices = AngelChoices.Of(session, god, hooks, Content.MaxChoices);

        Assert.Equal(home, choices[0].SettlementId);
        Assert.True(choices[0].Kindness >= 0);
        Assert.Contains(choices.Take(4), c => c.Deed == AngelDeed.CreatureFight && c.CreatureId == visitor.Id);
        Assert.Contains(choices.Take(5), c => c.Deed == AngelDeed.Answer && c.GodId == rival.Id);
    }

    [Fact]
    public void TheQuestion_SaysWhoTheAngelIs_WhatItsGodsDeedsMadeIt_AndOffersTheDeedsByTheirIds()
    {
        var (session, god, _, home) = Setup();
        god.Conduct.Help = 40f;
        var look = AngelLooks.Of(god, Content);
        var hooks = new[] { new AngelHook(AngelMoment.DeedKind, 1, "You cast Food on Duskwater.", home) };
        var choices = AngelChoices.Of(session, god, hooks, Content.MaxChoices);

        var question = AngelPrompt.Question(session, god, "Lumiel", look, hooks, choices, ["Feed them."], Content);

        Assert.Equal(AngelPrompt.Schema, question.Schema);
        Assert.Contains("You are Lumiel, the angel of " + god.Name, question.Instructions, StringComparison.Ordinal);
        Assert.Contains(Content.Stage(AngelStage.Radiant).Voice, question.Instructions, StringComparison.Ordinal);
        var turn = question.Turns.Single().Text;
        Assert.Contains("You cast Food on Duskwater.", turn, StringComparison.Ordinal);
        Assert.Contains("“Feed them.”", turn, StringComparison.Ordinal);
        Assert.All(choices, c => Assert.Contains($"- {c.Id}: {c.Text}", turn, StringComparison.Ordinal));
    }

    [Fact]
    public void AnAnswer_IsRead_OnlyForTheDeedsOffered_AtMostTwo()
    {
        var (session, god, _, _) = Setup();
        var choices = AngelChoices.Of(session, god, [], Content.MaxChoices);
        string Answer(string say, params string[] ids) => new JsonObject { ["say"] = say, ["suggestions"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]) }.ToJsonString();

        var read = AngelPrompt.Read(Answer("Feed Brightwater.", "D1", "d99", "d2", "d3"), choices)!;

        Assert.Equal("Feed Brightwater.", read.Say);
        Assert.Equal([choices[0], choices[1]], read.Suggestions);
        Assert.Empty(AngelPrompt.Read(Answer("Nothing now."), choices)!.Suggestions);
        Assert.Null(AngelPrompt.Read(Answer("  "), choices));
        Assert.Null(AngelPrompt.Read("not json", choices));
        Assert.True(AngelPrompt.Read(Answer(new string('a', 2000)), choices)!.Say.Length <= 420);
    }

    [Fact]
    public void TheScribes_SpeakInTheAngelsVoice_FillingEveryName()
    {
        var (session, god, rival, home) = Setup();
        var visitor = session.World.Creatures.Single(c => c.GodId == rival.Id);
        var hook = new AngelHook(AngelMoment.Visitor, 12, "visitor", home, rival.Id, visitor.Id);

        foreach (var stage in Enum.GetValues<AngelStage>())
        {
            var line = AngelScribe.Line(Content, AngelLooks.Forced(stage), hook, session.World, god, "Lumiel");
            Assert.DoesNotContain("{", line, StringComparison.Ordinal);
            Assert.Equal(line, AngelScribe.Line(Content, AngelLooks.Forced(stage), hook, session.World, god, "Lumiel"));
        }

        foreach (var moment in Enum.GetValues<AngelMoment>())
        {
            Assert.DoesNotContain("{", AngelScribe.Line(Content, AngelLook.Newborn, new AngelHook(moment, 3, "x", home, rival.Id, visitor.Id, "kind"), session.World, god, "Lumiel"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheScribes_UrgeKindDeedsForALightAngel_HarshOnesForADarkOne_AndBothForATornOne()
    {
        var kind = new AngelSuggestion("d1", AngelDeed.Cast, "Food on A", 1, default, "helps");
        var harsh = new AngelSuggestion("d2", AngelDeed.Cast, "Lightning on B", -1, default, "harms");
        var look = new AngelSuggestion("d3", AngelDeed.Look, "Look at C", 0, default, "trouble");
        AngelSuggestion[] all = [harsh, look, kind];

        Assert.Equal([kind], AngelScribe.Pick(all, AngelLooks.Forced(AngelStage.Radiant)));
        Assert.Equal([harsh], AngelScribe.Pick(all, AngelLooks.Forced(AngelStage.Fallen)));
        Assert.Equal([kind, harsh], AngelScribe.Pick(all, AngelLooks.Forced(AngelStage.Torn)));
        Assert.Equal([look], AngelScribe.Pick(all, AngelLooks.Forced(AngelStage.Unformed)));
        Assert.Equal([look], AngelScribe.Pick([look], AngelLooks.Forced(AngelStage.Stern)));
    }
}
