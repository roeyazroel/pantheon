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
}
