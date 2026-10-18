using System;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World.Generation;
using Pantheon.Persistence;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Game;

/// <summary>Validated simulation content loaded from <c>game/Data</c>.</summary>
/// <param name="WorldGen">World generation settings.</param>
/// <param name="SettlementRules">Statistical settlement tuning.</param>
public sealed record GameContent(WorldGenSettings WorldGen, SettlementRules SettlementRules)
{
    /// <summary>Parses and validates the content files.</summary>
    /// <param name="worldGenJson">Text of <c>worldgen.json</c>.</param>
    /// <param name="settlementRulesJson">Text of <c>settlement_rules.json</c>.</param>
    /// <returns>The validated content.</returns>
    /// <exception cref="ContentFormatException">A file is malformed or holds out-of-range values.</exception>
    public static GameContent Parse(string worldGenJson, string settlementRulesJson)
    {
        var worldGen = ContentJson.ParseWorldGenSettings(worldGenJson);
        var rules = ContentJson.ParseSettlementRules(settlementRulesJson);

        try
        {
            WorldGenSettingsValidator.Validate(worldGen);
            _ = new BiomeClassifier(worldGen.Biomes);
        }
        catch (ArgumentException exception)
        {
            throw new ContentFormatException($"{ContentJson.WorldGenFileName}: {exception.Message}", exception);
        }

        try
        {
            SettlementRulesValidator.Validate(rules);
        }
        catch (ArgumentException exception)
        {
            throw new ContentFormatException($"{ContentJson.SettlementRulesFileName}: {exception.Message}", exception);
        }

        return new GameContent(worldGen, rules);
    }
}
