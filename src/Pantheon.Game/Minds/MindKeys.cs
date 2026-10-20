namespace Pantheon.Game.Minds;

/// <summary>The player's own keys for the online models (ADR-065, ADR-067), each empty when they gave none.</summary>
/// <param name="Anthropic">The Anthropic key, for Claude.</param>
/// <param name="OpenAI">The OpenAI key, for GPT.</param>
/// <param name="Gemini">The Gemini API key, for Gemini.</param>
public sealed record MindKeys(string? Anthropic = null, string? OpenAI = null, string? Gemini = null)
{
    /// <summary>No keys at all.</summary>
    public static readonly MindKeys None = new();

    /// <summary>The key a provider needs; <c>null</c> for one that needs none.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The key the player gave, if any.</returns>
    public string? For(MindProvider provider) => provider switch
    {
        MindProvider.Claude => Anthropic,
        MindProvider.OpenAI => OpenAI,
        MindProvider.Gemini => Gemini,
        MindProvider.Scribes or MindProvider.Local => null,
    };

    /// <summary>The keys with one provider's changed.</summary>
    /// <param name="provider">The provider.</param>
    /// <param name="key">Its key.</param>
    /// <returns>The keys.</returns>
    public MindKeys With(MindProvider provider, string? key) => provider switch
    {
        MindProvider.Claude => this with { Anthropic = key },
        MindProvider.OpenAI => this with { OpenAI = key },
        MindProvider.Gemini => this with { Gemini = key },
        MindProvider.Scribes or MindProvider.Local => this,
    };
}
