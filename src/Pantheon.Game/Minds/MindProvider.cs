using System.Text.Json.Serialization;

namespace Pantheon.Game.Minds;

/// <summary>Who gives the rival gods their minds (ADR-065).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MindProvider>))]
public enum MindProvider
{
    /// <summary>The scribes, from the seed: no model and no network.</summary>
    Scribes,

    /// <summary>Claude, with the player's own Anthropic key.</summary>
    Claude,

    /// <summary>A model on the player's own computer, served by Ollama.</summary>
    Local,

    /// <summary>OpenAI's GPT, with the player's own OpenAI key (ADR-067).</summary>
    OpenAI,

    /// <summary>Google's Gemini, with the player's own Gemini key (ADR-067).</summary>
    Gemini,
}
