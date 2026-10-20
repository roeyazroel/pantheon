using System;
using Pantheon.Game.Minds;
using Pantheon.Game.Settings;

namespace Pantheon.Oracle;

/// <summary>Opens the language model the player's settings choose (ADR-065, ADR-067).</summary>
public static class MindFactory
{
    /// <summary>The model the settings choose: none for the scribes, or when an online model is chosen without its key.</summary>
    /// <param name="settings">The player's settings.</param>
    /// <param name="keys">The player's keys.</param>
    /// <param name="minds">The minds' content.</param>
    /// <param name="problem">Why there is no model when one was chosen; <c>null</c> otherwise.</param>
    /// <returns>The model, or <c>null</c>.</returns>
    public static ILanguageModel? Open(GameSettings settings, MindKeys keys, MindContent minds, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(minds);
        problem = null;
        var timeout = TimeSpan.FromSeconds(minds.TimeoutSeconds);
        if (settings.Minds is MindProvider.Claude or MindProvider.OpenAI or MindProvider.Gemini && KeyOf(settings.Minds, keys.For(settings.Minds)) is null)
        {
            problem = $"{ProviderName(settings.Minds)} needs your {KeyName(settings.Minds)}: add it in Settings, under Minds";
            return null;
        }

        var key = KeyOf(settings.Minds, keys.For(settings.Minds));
        switch (settings.Minds)
        {
            case MindProvider.Claude:
                var claude = minds.ClaudeModel(settings.ClaudeModel);
                return new AnthropicLanguageModel(key, claude.Id, claude.Name, timeout);
            case MindProvider.OpenAI:
                var gpt = minds.OpenAIModel(settings.OpenAIModel);
                return new OpenAILanguageModel(key!, gpt.Id, gpt.Name, timeout);
            case MindProvider.Gemini:
                var gemini = minds.GeminiModel(settings.GeminiModel);
                return new GeminiLanguageModel(key!, gemini.Id, gemini.Name, timeout);
            case MindProvider.Local:
                return new LocalLanguageModel(new Uri(settings.LocalUrl), settings.LocalModel, timeout);
            case MindProvider.Scribes:
            default:
                return null;
        }
    }

    /// <summary>The environment variables a provider's key may be kept in, the first used first.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>Their names; none for a provider that needs no key.</returns>
    public static string[] KeyVariables(MindProvider provider) => provider switch
    {
        MindProvider.Claude => ["ANTHROPIC_API_KEY"],
        MindProvider.OpenAI => ["OPENAI_API_KEY"],
        MindProvider.Gemini => ["GEMINI_API_KEY", "GOOGLE_API_KEY"],
        MindProvider.Scribes or MindProvider.Local => [],
    };

    /// <summary>The key to use: the player's, else the environment's; <c>null</c> when there is neither.</summary>
    /// <param name="provider">The provider.</param>
    /// <param name="given">The key the player gave, if any.</param>
    /// <returns>The key.</returns>
    public static string? KeyOf(MindProvider provider, string? given)
    {
        if (!string.IsNullOrWhiteSpace(given))
        {
            return given.Trim();
        }

        foreach (var variable in KeyVariables(provider))
        {
            if (Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value)
            {
                return value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// A warning when a key plainly is not the provider's API key, such as a Claude.ai or Claude Code
    /// sign-in given for an Anthropic key. Only the beginnings the providers have long used are checked,
    /// so a key is never refused for its shape: the provider decides.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="key">The key the player gave.</param>
    /// <returns>The warning, for the player; <c>null</c> when the key looks right, is empty, or its provider's keys are not checked.</returns>
    public static string? KeyAdvice(MindProvider provider, string? key)
    {
        var (name, begins, signIn, where) = provider switch
        {
            MindProvider.Claude => ("Anthropic", "sk-ant-api", "A Claude.ai or Claude Code sign-in", "console.anthropic.com, under API Keys"),
            MindProvider.OpenAI => ("OpenAI", "sk-", "A ChatGPT sign-in", "platform.openai.com, under API keys"),
            MindProvider.Gemini or MindProvider.Scribes or MindProvider.Local => (null, null, null, null),
        };
        return begins is null || string.IsNullOrWhiteSpace(key) || key.Trim().StartsWith(begins, StringComparison.Ordinal)
            ? null
            : $"This is not an {name} API key: those begin with {begins}. {signIn} will not work here; create a key at {where}.";
    }

    /// <summary>A provider's name, for the player.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>For example "Claude" or "Gemini".</returns>
    public static string ProviderName(MindProvider provider) => provider switch
    {
        MindProvider.Scribes => "The scribes",
        MindProvider.Claude => "Claude",
        MindProvider.OpenAI => "GPT",
        MindProvider.Gemini => "Gemini",
        MindProvider.Local => "A local model",
    };

    /// <summary>What a provider's key is called, for the player.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>For example "Anthropic key".</returns>
    public static string KeyName(MindProvider provider) => provider switch
    {
        MindProvider.Claude => "Anthropic key",
        MindProvider.OpenAI => "OpenAI key",
        MindProvider.Gemini => "Gemini API key",
        MindProvider.Scribes or MindProvider.Local => "key",
    };
}
