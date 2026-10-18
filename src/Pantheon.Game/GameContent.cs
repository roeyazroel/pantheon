using System;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World.Generation;
using Pantheon.Persistence;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Game;

/// <summary>Validated simulation content loaded from <c>game/Data</c>.</summary>
/// <param name="WorldGen">World generation settings.</param>
/// <param name="SettlementRules">Statistical settlement tuning.</param>
/// <param name="DivineRules">Belief, worship, Divine Power and influence tuning.</param>
/// <param name="Miracles">The miracles.</param>
/// <param name="Pantheon">The gods of a new world.</param>
public sealed record GameContent(WorldGenSettings WorldGen, SettlementRules SettlementRules, DivineRules DivineRules, MiracleBook Miracles, PantheonSetup Pantheon)
{
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

        Check(ContentJson.WorldGenFileName, () =>
        {
            WorldGenSettingsValidator.Validate(worldGen);
            _ = new BiomeClassifier(worldGen.Biomes);
        });
        Check(ContentJson.SettlementRulesFileName, () => SettlementRulesValidator.Validate(rules));
        Check(ContentJson.DivineRulesFileName, () => DivineRulesValidator.Validate(divine));
        Check(ContentJson.GodsFileName, () => PantheonFounder.Validate(pantheon));
        MiracleBook? book = null;
        Check(ContentJson.MiraclesFileName, () => book = new MiracleBook(catalog));
        return new GameContent(worldGen, rules, divine, book!, pantheon);
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
public sealed record GameContentFiles(string WorldGen, string SettlementRules, string DivineRules, string Miracles, string Gods)
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
            read(ContentJson.GodsFileName));
    }
}
