using System;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Persistence;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Weather;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Game;

/// <summary>Validated simulation content loaded from <c>game/Data</c>.</summary>
/// <param name="WorldGen">World generation settings.</param>
/// <param name="SettlementRules">Statistical settlement tuning.</param>
/// <param name="DivineRules">Belief, worship, Divine Power and influence tuning.</param>
/// <param name="Miracles">The miracles.</param>
/// <param name="Pantheon">The gods of a new world.</param>
/// <param name="Creatures">Creature tuning.</param>
/// <param name="GodAi">Tuning of the gods' utility AI.</param>
/// <param name="Diplomacy">Tuning of relations between gods.</param>
/// <param name="Weather">Tuning of weather and soil water.</param>
/// <param name="Disasters">Tuning of wildfires and earthquakes.</param>
/// <param name="Migration">Tuning of migration.</param>
/// <param name="History">What the world's history keeps.</param>
/// <param name="Domains">The gods' domains (ADR-021).</param>
/// <param name="HolySites">The world's holy sites (ADR-021).</param>
/// <param name="Water">Rivers, floods and irrigation (ADR-022).</param>
/// <param name="Society">Settlements' society: safety, happiness, wealth, learning, technology, buildings and priorities (ADR-023).</param>
public sealed record GameContent(WorldGenSettings WorldGen, SettlementRules SettlementRules, DivineRules DivineRules, MiracleBook Miracles, PantheonSetup Pantheon, CreatureRules Creatures, GodAiRules GodAi, DiplomacyRules Diplomacy, WeatherRules Weather, DisasterRules Disasters, MigrationRules Migration, Domain.History.HistoryRules History, DomainRules Domains, Domain.HolySites.HolySiteRules HolySites, Domain.Water.WaterRules Water, Domain.Society.SocietyRules Society)
{
    /// <summary>
    /// A fingerprint of the content files this content was parsed from (SHA-256, hex), so a host can
    /// refuse players whose game data differs from its own (ADR-018); empty for content built in code.
    /// </summary>
    public string Fingerprint { get; init; } = string.Empty;

    /// <summary>The sizes a new world may be made at (ADR-027); only the default world when none are given.</summary>
    public WorldSizes Sizes { get; init; } = WorldSizes.Default;

    /// <summary>The full simulation pipeline over this content.</summary>
    /// <returns>A new engine.</returns>
    public SimulationEngine CreateEngine() => new(SettlementRules, DivineRules, Creatures, GodAi, Miracles, Diplomacy, Weather, Disasters, Migration, History, Domains, HolySites, Water, Society);

    /// <summary>Parses and validates the content files.</summary>
    /// <param name="files">Text of each content file.</param>
    /// <returns>The validated content.</returns>
    /// <exception cref="ContentFormatException">A file is malformed or holds out-of-range values.</exception>
    public static GameContent Parse(GameContentFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var worldGen = ContentJson.ParseWorldGenSettings(files.WorldGen);
        var rules = ContentJson.ParseSettlementRules(files.SettlementRules);
        var divine = ContentJson.ParseDivineRules(files.DivineRules);
        var catalog = ContentJson.ParseMiracles(files.Miracles);
        var pantheon = ContentJson.ParseGods(files.Gods);
        var creatures = ContentJson.ParseCreatures(files.Creatures);
        var godAi = ContentJson.ParseGodAi(files.GodAi);
        var diplomacy = ContentJson.ParseDiplomacy(files.Diplomacy);
        var weather = ContentJson.ParseWeather(files.Weather);
        var disasters = ContentJson.ParseDisasters(files.Disasters);
        var migration = ContentJson.ParseMigration(files.Migration);
        var history = ContentJson.ParseHistory(files.History);
        var domains = ContentJson.ParseDomains(files.Domains);
        var holySites = ContentJson.ParseHolySites(files.HolySites);
        var water = ContentJson.ParseWater(files.Water);
        var society = ContentJson.ParseSociety(files.Society);
        var sizes = string.IsNullOrWhiteSpace(files.WorldSizes) ? WorldSizes.Default : ContentJson.ParseWorldSizes(files.WorldSizes);

        Check(ContentJson.WorldGenFileName, () =>
        {
            WorldGenSettingsValidator.Validate(worldGen);
            _ = new BiomeClassifier(worldGen.Biomes);
        });
        Check(ContentJson.SettlementRulesFileName, () => SettlementRulesValidator.Validate(rules));
        Check(ContentJson.DivineRulesFileName, () => DivineRulesValidator.Validate(divine));
        Check(ContentJson.GodsFileName, () => PantheonFounder.Validate(pantheon));
        Check(ContentJson.CreaturesFileName, () => CreatureRulesValidator.Validate(creatures));
        Check(ContentJson.GodAiFileName, () => GodAiRulesValidator.Validate(godAi));
        Check(ContentJson.DiplomacyFileName, () => DiplomacyRulesValidator.Validate(diplomacy));
        Check(ContentJson.WeatherFileName, () => WeatherRulesValidator.Validate(weather));
        Check(ContentJson.DisastersFileName, () => DisasterRulesValidator.Validate(disasters));
        Check(ContentJson.MigrationFileName, () => MigrationRulesValidator.Validate(migration));
        Check(ContentJson.HistoryFileName, () => Simulation.History.HistoryRulesValidator.Validate(history));
        Check(ContentJson.DomainsFileName, () => Simulation.Domains.DomainRulesValidator.Validate(domains));
        Check(ContentJson.HolySitesFileName, () => Simulation.HolySites.HolySiteRulesValidator.Validate(holySites));
        Check(ContentJson.WaterFileName, () => Simulation.Water.WaterRulesValidator.Validate(water));
        Check(ContentJson.SocietyFileName, () => Simulation.Society.SocietyRulesValidator.Validate(society));
        Check(ContentJson.WorldSizesFileName, () => WorldMaker.Validate(sizes, worldGen, pantheon));
        MiracleBook? book = null;
        Check(ContentJson.MiraclesFileName, () => book = new MiracleBook(catalog));
        CheckAcrossFiles(pantheon, creatures, godAi, domains, book!);
        return new GameContent(worldGen, rules, divine, book!, pantheon, creatures, godAi, diplomacy, weather, disasters, migration, history, domains, holySites, water, society) { Fingerprint = FingerprintOf(files), Sizes = sizes };
    }

    /// <summary>
    /// What one file names in another (ADR-021): every god's Creature species exists, and every
    /// domain a signature miracle needs or an AI prefers can be chosen.
    /// </summary>
    private static void CheckAcrossFiles(PantheonSetup pantheon, CreatureRules creatures, GodAiRules godAi, DomainRules domains, MiracleBook book)
    {
        Check(ContentJson.GodsFileName, () =>
        {
            foreach (var god in pantheon.Gods.Concat(pantheon.ExtraGods))
            {
                if (CreatureModel.FindSpecies(creatures, god.CreatureSpecies) is null)
                {
                    throw new ArgumentException($"{god.Name}'s Creature is a {god.CreatureSpecies}, which {ContentJson.CreaturesFileName} does not list.");
                }
            }
        });
        Check(ContentJson.MiraclesFileName, () =>
        {
            foreach (var miracle in book.Miracles)
            {
                if (miracle.RequiresDomain && Simulation.Domains.DomainModel.Find(domains, miracle.Domain) is null)
                {
                    throw new ArgumentException($"{miracle.Id} needs {miracle.Domain}, which {ContentJson.DomainsFileName} does not offer.");
                }
            }
        });
        Check(ContentJson.GodAiFileName, () =>
        {
            foreach (var profile in godAi.Profiles ?? [])
            {
                foreach (var domain in profile.Domains)
                {
                    if (Simulation.Domains.DomainModel.Find(domains, domain) is null)
                    {
                        throw new ArgumentException($"{profile.Personality} prefers {domain}, which {ContentJson.DomainsFileName} does not offer.");
                    }
                }
            }
        });
    }

    /// <summary>The SHA-256 of every content file's text, in order, as lower-case hex.</summary>
    private static string FingerprintOf(GameContentFiles files)
    {
        var text = string.Join('\0', files.WorldGen, files.SettlementRules, files.DivineRules, files.Miracles, files.Gods, files.Creatures, files.GodAi, files.Diplomacy, files.Weather, files.Disasters, files.Migration, files.History, files.Domains, files.HolySites, files.Water, files.Society, files.WorldSizes);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    /// <summary>Runs a validator and reports its problems as a content error of one file.</summary>
    private static void Check(string fileName, Action validate)
    {
        try
        {
            validate();
        }
        catch (ArgumentException exception)
        {
            throw new ContentFormatException($"{fileName}: {exception.Message}", exception);
        }
    }
}

/// <summary>Text of the simulation content files, as read from <c>game/Data</c>.</summary>
/// <param name="WorldGen"><c>worldgen.json</c>.</param>
/// <param name="SettlementRules"><c>settlement_rules.json</c>.</param>
/// <param name="DivineRules"><c>divine_rules.json</c>.</param>
/// <param name="Miracles"><c>miracles.json</c>.</param>
/// <param name="Gods"><c>gods.json</c>.</param>
/// <param name="Creatures"><c>creatures.json</c>.</param>
/// <param name="GodAi"><c>god_ai.json</c>.</param>
/// <param name="Diplomacy"><c>diplomacy.json</c>.</param>
/// <param name="Weather"><c>weather.json</c>.</param>
/// <param name="Disasters"><c>disasters.json</c>.</param>
/// <param name="Migration"><c>migration.json</c>.</param>
/// <param name="History"><c>history.json</c>.</param>
/// <param name="Domains"><c>domains.json</c>.</param>
/// <param name="HolySites"><c>holy_sites.json</c>.</param>
/// <param name="Water"><c>water.json</c>.</param>
/// <param name="Society"><c>society.json</c>.</param>
/// <param name="WorldSizes"><c>world_sizes.json</c>; empty for the default world only (ADR-027).</param>
public sealed record GameContentFiles(string WorldGen, string SettlementRules, string DivineRules, string Miracles, string Gods, string Creatures, string GodAi, string Diplomacy, string Weather, string Disasters, string Migration, string History, string Domains, string HolySites, string Water, string Society, string WorldSizes = "")
{
    /// <summary>Reads every file through a function that returns a content file's text by name.</summary>
    /// <param name="read">Reads one file of <c>game/Data</c>.</param>
    /// <returns>The files.</returns>
    public static GameContentFiles Read(Func<string, string> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return new GameContentFiles(
            read(ContentJson.WorldGenFileName),
            read(ContentJson.SettlementRulesFileName),
            read(ContentJson.DivineRulesFileName),
            read(ContentJson.MiraclesFileName),
            read(ContentJson.GodsFileName),
            read(ContentJson.CreaturesFileName),
            read(ContentJson.GodAiFileName),
            read(ContentJson.DiplomacyFileName),
            read(ContentJson.WeatherFileName),
            read(ContentJson.DisastersFileName),
            read(ContentJson.MigrationFileName),
            read(ContentJson.HistoryFileName),
            read(ContentJson.DomainsFileName),
            read(ContentJson.HolySitesFileName),
            read(ContentJson.WaterFileName),
            read(ContentJson.SocietyFileName),
            read(ContentJson.WorldSizesFileName));
    }
}
