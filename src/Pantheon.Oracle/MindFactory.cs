using System;
using Pantheon.Game.Minds;
using Pantheon.Game.Settings;

namespace Pantheon.Oracle;

/// <summary>Opens the language model the player's settings choose (ADR-065).</summary>
public static class MindFactory
{
    /// <summary>The environment variable Anthropic's tools keep a key in, used when the player has not given one.</summary>
    public const string KeyVariable = "ANTHROPIC_API_KEY";

    /// <summary>The model the settings choose: none for the scribes, or when Claude is chosen without a key.</summary>
    /// <param name="settings">The player's settings.</param>
    /// <param name="anthropicKey">The player's Anthropic key, if they gave one.</param>
    /// <param name="minds">The minds' content.</param>
    /// <param name="problem">Why there is no model when one was chosen; <c>null</c> otherwise.</param>
    /// <returns>The model, or <c>null</c>.</returns>
    public static ILanguageModel? Open(GameSettings settings, string? anthropicKey, MindContent minds, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(minds);
        problem = null;
        var timeout = TimeSpan.FromSeconds(minds.TimeoutSeconds);
        switch (settings.Minds)
        {
            case MindProvider.Claude:
                var key = KeyOf(anthropicKey);
                if (key is null)
                {
                    problem = "Claude needs your Anthropic key: add it in Settings, under Minds";
                    return null;
                }

                var choice = minds.ClaudeModel(settings.ClaudeModel);
                return new AnthropicLanguageModel(key, choice.Id, choice.Name, timeout);
            case MindProvider.Local:
                return new LocalLanguageModel(new Uri(settings.LocalUrl), settings.LocalModel, timeout);
            case MindProvider.Scribes:
            default:
                return null;
        }
    }

    /// <summary>The key to use: the player's, else the environment's; <c>null</c> when there is neither.</summary>
    /// <param name="anthropicKey">The player's key, if they gave one.</param>
    /// <returns>The key.</returns>
    public static string? KeyOf(string? anthropicKey) =>
        !string.IsNullOrWhiteSpace(anthropicKey) ? anthropicKey.Trim()
        : Environment.GetEnvironmentVariable(KeyVariable) is { Length: > 0 } variable ? variable.Trim()
        : null;
}
