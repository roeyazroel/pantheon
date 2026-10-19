using System;
using System.Text.Json;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
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

    /// <summary>File name of the divine rules (belief, worship, power and influence).</summary>
    public const string DivineRulesFileName = "divine_rules.json";

    /// <summary>File name of the miracle catalog.</summary>
    public const string MiraclesFileName = "miracles.json";

    /// <summary>File name of the gods of a new world.</summary>
    public const string GodsFileName = "gods.json";

    /// <summary>File name of the Creature rules.</summary>
    public const string CreaturesFileName = "creatures.json";

    /// <summary>File name of the god AI rules.</summary>
    public const string GodAiFileName = "god_ai.json";

    /// <summary>File name of the diplomacy rules.</summary>
    public const string DiplomacyFileName = "diplomacy.json";

    /// <summary>File name of the weather rules.</summary>
    public const string WeatherFileName = "weather.json";

    /// <summary>File name of the disaster rules.</summary>
    public const string DisastersFileName = "disasters.json";

    /// <summary>File name of the migration rules.</summary>
    public const string MigrationFileName = "migration.json";

    /// <summary>File name of the history rules.</summary>
    public const string HistoryFileName = "history.json";

    /// <summary>File name of the god domains (ADR-021).</summary>
    public const string DomainsFileName = "domains.json";

    /// <summary>File name of the holy sites (ADR-021).</summary>
    public const string HolySitesFileName = "holy_sites.json";

    /// <summary>File name of the rivers and floods (ADR-022).</summary>
    public const string WaterFileName = "water.json";

    /// <summary>File name of settlements' society (ADR-023).</summary>
    public const string SocietyFileName = "society.json";

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

    /// <summary>Parses <c>divine_rules.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The divine rules.</returns>
    public static DivineRules ParseDivineRules(string json) => Parse<DivineRules>(json, DivineRulesFileName);

    /// <summary>Parses <c>miracles.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The miracle catalog.</returns>
    public static MiracleCatalog ParseMiracles(string json) => Parse<MiracleCatalog>(json, MiraclesFileName);

    /// <summary>Parses <c>gods.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The pantheon setup.</returns>
    public static PantheonSetup ParseGods(string json) => Parse<PantheonSetup>(json, GodsFileName);

    /// <summary>Parses <c>creatures.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The Creature rules.</returns>
    public static CreatureRules ParseCreatures(string json) => Parse<CreatureRules>(json, CreaturesFileName);

    /// <summary>Parses <c>god_ai.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The god AI rules.</returns>
    public static GodAiRules ParseGodAi(string json) => Parse<GodAiRules>(json, GodAiFileName);

    /// <summary>Parses <c>diplomacy.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The diplomacy rules.</returns>
    public static DiplomacyRules ParseDiplomacy(string json) => Parse<DiplomacyRules>(json, DiplomacyFileName);

    /// <summary>Parses <c>weather.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The weather rules.</returns>
    public static WeatherRules ParseWeather(string json) => Parse<WeatherRules>(json, WeatherFileName);

    /// <summary>Parses <c>disasters.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The disaster rules.</returns>
    public static DisasterRules ParseDisasters(string json) => Parse<DisasterRules>(json, DisastersFileName);

    /// <summary>Parses <c>migration.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The migration rules.</returns>
    public static MigrationRules ParseMigration(string json) => Parse<MigrationRules>(json, MigrationFileName);

    /// <summary>Parses <c>history.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The history rules.</returns>
    public static Domain.History.HistoryRules ParseHistory(string json) => Parse<Domain.History.HistoryRules>(json, HistoryFileName);

    /// <summary>Parses <c>domains.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The domain rules.</returns>
    public static DomainRules ParseDomains(string json) => Parse<DomainRules>(json, DomainsFileName);

    /// <summary>Parses <c>society.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The society rules.</returns>
    public static Domain.Society.SocietyRules ParseSociety(string json) => Parse<Domain.Society.SocietyRules>(json, SocietyFileName);

    /// <summary>Parses <c>water.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The water rules.</returns>
    public static Domain.Water.WaterRules ParseWater(string json) => Parse<Domain.Water.WaterRules>(json, WaterFileName);

    /// <summary>Parses <c>holy_sites.json</c>.</summary>
    /// <param name="json">File text.</param>
    /// <returns>The holy site rules.</returns>
    public static Domain.HolySites.HolySiteRules ParseHolySites(string json) => Parse<Domain.HolySites.HolySiteRules>(json, HolySitesFileName);
}
