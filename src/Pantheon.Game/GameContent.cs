using System;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
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
public sealed record GameContent(WorldGenSettings WorldGen, SettlementRules SettlementRules, DivineRules DivineRules, MiracleBook Miracles, PantheonSetup Pantheon, CreatureRules Creatures, GodAiRules GodAi, DiplomacyRules Diplomacy, WeatherRules Weather, DisasterRules Disasters, MigrationRules Migration)
{
    /// <summary>The full simulation pipeline over this content.</summary>
    /// <returns>A new engine.</returns>
    public SimulationEngine CreateEngine() => new(SettlementRules, DivineRules, Creatures, GodAi, Miracles, Diplomacy, Weather, Disasters, Migration);

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
        MiracleBook? book = null;
        Check(ContentJson.MiraclesFileName, () => book = new MiracleBook(catalog));
        return new GameContent(worldGen, rules, divine, book!, pantheon, creatures, godAi, diplomacy, weather, disasters, migration);
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
public sealed record GameContentFiles(string WorldGen, string SettlementRules, string DivineRules, string Miracles, string Gods, string Creatures, string GodAi, string Diplomacy, string Weather, string Disasters, string Migration)
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
            read(ContentJson.MigrationFileName));
    }
}
