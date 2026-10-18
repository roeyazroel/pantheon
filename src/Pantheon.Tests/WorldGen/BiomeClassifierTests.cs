using System;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.WorldGen;

/// <summary>The data-driven biome rules classify samples in order, with a final catch-all.</summary>
public sealed class BiomeClassifierTests
{
    private static readonly BiomeClassifier Shipped = new(TestContent.Shipped.WorldGen.Biomes);

    [Theory]
    [InlineData(-0.01f, 0.6f, 0.9f, Biome.Sea)]
    [InlineData(-30f, 0.5f, 0.2f, Biome.Sea)]
    [InlineData(70f, 0.3f, 0.9f, Biome.Highland)]
    [InlineData(150f, 0.1f, 0.1f, Biome.Highland)]
    [InlineData(20f, 0.6f, 0.55f, Biome.Forest)]
    [InlineData(0f, 0.7f, 0.95f, Biome.Forest)]
    [InlineData(20f, 0.6f, 0.54f, Biome.Grassland)]
    [InlineData(69.9f, 0.4f, 0.1f, Biome.Grassland)]
    public void Classifier_UsesShippedRulesInOrder(float elevation, float temperature, float moisture, Biome expected)
    {
        Assert.Equal(expected, Shipped.Classify(elevation, temperature, moisture));
    }

    [Fact]
    public void Classifier_ExposesBiomeTraits()
    {
        Assert.Equal(0f, Shipped.GetRule(Biome.Sea).BaseFertility);
        Assert.True(Shipped.GetRule(Biome.Grassland).BaseFertility > Shipped.GetRule(Biome.Highland).BaseFertility);
        Assert.True(Shipped.GetRule(Biome.Forest).BaseVegetation > Shipped.GetRule(Biome.Grassland).BaseVegetation);
    }

    [Fact]
    public void Classifier_RejectsRulesWithoutCatchAll()
    {
        var rules = new[]
        {
            new BiomeRule { Biome = Biome.Sea, MaxElevation = 0f, BaseFertility = 0f, BaseVegetation = 0f },
            new BiomeRule { Biome = Biome.Grassland, MinElevation = 0f, BaseFertility = 1f, BaseVegetation = 1f },
        };

        Assert.Throws<ArgumentException>(() => new BiomeClassifier(rules));
    }

    [Fact]
    public void Classifier_RejectsDuplicateBiomes()
    {
        var rules = new[]
        {
            new BiomeRule { Biome = Biome.Grassland, MaxElevation = 0f, BaseFertility = 0f, BaseVegetation = 0f },
            new BiomeRule { Biome = Biome.Grassland, BaseFertility = 1f, BaseVegetation = 1f },
        };

        Assert.Throws<ArgumentException>(() => new BiomeClassifier(rules));
    }
}
