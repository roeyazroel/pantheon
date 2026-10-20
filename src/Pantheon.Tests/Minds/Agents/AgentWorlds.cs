using System.Linq;
using System.Text.Json.Nodes;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Minds;
using Pantheon.Game.Minds.Agents;
using Pantheon.Tests.Support;

namespace Pantheon.Tests.Minds.Agents;

/// <summary>Worlds and answers for the agents' tests (ADR-072).</summary>
internal static class AgentWorlds
{
    /// <summary>The shipped agents' rules.</summary>
    public static AgentRules Rules => TestContent.Minds.Agents;

    /// <summary>The shipped scribes' lines.</summary>
    public static VoiceLines Voices => TestContent.Minds.Voices;

    /// <summary>A session on the small island at tick 20000, every god met, with its first AI god and the player's god.</summary>
    /// <returns>The session, the rival and the player's god's id.</returns>
    public static (GameSession Session, GodState Rival, int Me) Setup()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        Meetings.All(session.World).Tick = 20_000;
        return (session, session.World.Gods.First(g => g.Controller == GodController.Ai), session.LocalGodId!.Value);
    }

    /// <summary>A god's answer: acts as (tool, target, detail), words as (to, text), and a journal.</summary>
    /// <param name="journal">What it keeps in mind.</param>
    /// <param name="acts">Its acts.</param>
    /// <param name="words">Its words.</param>
    /// <returns>The answer.</returns>
    public static ModelAnswer GodAnswer(string journal, (string Tool, string Target, string Detail)[] acts, params (string To, string Text)[] words) => ModelAnswer.Of(new JsonObject
    {
        ["reason"] = "It must be so.",
        ["actions"] = new JsonArray([.. acts.Select(a => (JsonNode)new JsonObject { ["tool"] = a.Tool, ["target"] = a.Target, ["detail"] = a.Detail })]),
        ["messages"] = new JsonArray([.. words.Select(w => (JsonNode)new JsonObject { ["to"] = w.To, ["text"] = w.Text })]),
        ["journal"] = journal,
    }.ToJsonString(), 1000, 100);

    /// <summary>A Creature's answer.</summary>
    /// <param name="say">What it says.</param>
    /// <param name="choice">Its choice.</param>
    /// <returns>The answer.</returns>
    public static ModelAnswer CreatureAnswer(string say, string choice) => ModelAnswer.Of(new JsonObject { ["say"] = say, ["choice"] = choice }.ToJsonString(), 500, 20);
}
