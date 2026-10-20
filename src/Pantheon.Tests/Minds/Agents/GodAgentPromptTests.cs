using System.Linq;
using System.Text.Json;
using Pantheon.Game.Minds;
using Pantheon.Game.Minds.Agents;
using Xunit;

namespace Pantheon.Tests.Minds.Agents;

/// <summary>What a god's mind is told and how its answer is read (ADR-072).</summary>
public sealed class GodAgentPromptTests
{
    [Fact]
    public void TheInstructions_SayWhoTheGodIs_AndEveryTool()
    {
        var (_, rival, _) = AgentWorlds.Setup();

        var instructions = GodAgentPrompt.Instructions(rival, AgentWorlds.Rules);

        Assert.Contains(PersonaText.Title(rival), instructions);
        Assert.All(GodTools.Ids, tool => Assert.Contains(tool, instructions));
        Assert.All(GodTools.Treaties.Keys, treaty => Assert.Contains(treaty, instructions));
        Assert.All(GodTools.Aims.Keys, aim => Assert.Contains(aim, instructions));
    }

    [Fact]
    public void ATurn_TellsWhatWokeTheGod_HowThingsStand_ItsJournal_AndWhatCameOfItsActs()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        rival.Journal = "Ilyra is not to be trusted.";
        var player = session.World.Gods.Single(g => g.Id == me);

        var turn = GodAgentPrompt.Turn(session, rival, [new Hook(HookKind.Diplomacy, 1, "Ilyra declared war on you.")], ["cast heal: done"], AgentWorlds.Rules);

        Assert.Contains("Ilyra declared war on you.", turn);
        Assert.Contains("Ilyra is not to be trusted.", turn);
        Assert.Contains("cast heal: done", turn);
        Assert.Contains(player.Name, turn);
        Assert.Contains("Villages", turn);
        Assert.Contains("Your instincts would", turn);
        Assert.DoesNotContain("Nothing in particular", turn);
    }

    [Fact]
    public void AReflection_SaysNothingInParticularWokeIt_AndAFirstThoughtHasNoJournal()
    {
        var (session, rival, _) = AgentWorlds.Setup();

        var turn = GodAgentPrompt.Turn(session, rival, [new Hook(HookKind.Reflect, 1, "It is time to look about you.")], [], AgentWorlds.Rules);

        Assert.Contains("Nothing in particular", turn);
        Assert.Contains("this is your first thought", turn);
    }

    [Fact]
    public void AnAnswer_IsReadAndTrimmedToTheRules()
    {
        var rules = AgentWorlds.Rules;
        var answer = AgentWorlds.GodAnswer(new string('j', rules.JournalLength + 50), [.. Enumerable.Repeat(("cast", "Duskwater", "heal"), rules.MaxActions + 2)], ("Ilyra", "Beware."), ("Seren", "Join me."), ("Kaio", "Kneel."));

        var turn = GodAgentPrompt.Read(answer.Json, rules)!;

        Assert.Equal(rules.MaxActions, turn.Acts.Count);
        Assert.Equal(rules.MaxMessages, turn.Words.Count);
        Assert.Equal(rules.JournalLength, turn.Journal.Length);
        Assert.Equal("It must be so.", turn.Reason);
        Assert.Null(GodAgentPrompt.Read("not json", rules));
        Assert.Null(GodAgentPrompt.Read("[1, 2]", rules));
    }

    [Fact]
    public void TheSchema_AsksForEveryPropertyAndNoOthers()
    {
        using var schema = JsonDocument.Parse(GodAgentPrompt.Schema);
        var root = schema.RootElement;

        Assert.Equal(["reason", "actions", "messages", "journal"], root.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["tool", "target", "detail"], root.GetProperty("properties").GetProperty("actions").GetProperty("items").GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }
}
