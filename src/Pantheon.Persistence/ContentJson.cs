using System;
using System.Text.Json;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World.Generation;

namespace Pantheon.Persistence;

/// <summary>
/// Parses the data-driven content files shipped in <c>game/Data</c>. Parsing is strict about
/// member names and types (comments and trailing commas are allowed); value ranges are checked
/// by the simulation validators.
/// </summary>
public static class ContentJson
{
    /// <summary>File name of the world generation settings.</summary>
    public const string WorldGenFileName = "worldgen.json";

    /// <summary>File name of the settlement rules.</summary>
    public const string SettlementRulesFileName = "settlement_rules.json";

    /// <summary>Parses any content type.</summary>
    /// <typeparam name="T">Content contract type.</typeparam>
    /// <param name="json">File text.</param>
    /// <param name="sourceName">File name used in error messages.</param>
    /// <returns>The parsed content.</returns>
    /// <exception cref="ContentFormatException">The JSON does not match the contract.</exception>
    public static T Parse<T>(string json, string sourceName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(sourceName);

        T? content;
        try
        {
            content = JsonSerializer.Deserialize<T>(json, PantheonJson.ContentOptions);
        }
        catch (JsonException exception)
        {
            throw new ContentFormatException($"{sourceName} is invalid: {exception.Message}", exception);
        }

        return content ?? throw new ContentFormatException($"{sourceName} must contain a JSON object.");
    }

    /// <summary>Parses <c>worldgen.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The generation settings.</returns>
    public static WorldGenSettings ParseWorldGenSettings(string json) => Parse<WorldGenSettings>(json, WorldGenFileName);

    /// <summary>Parses <c>settlement_rules.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The settlement rules.</returns>
    public static SettlementRules ParseSettlementRules(string json) => Parse<SettlementRules>(json, SettlementRulesFileName);
}
