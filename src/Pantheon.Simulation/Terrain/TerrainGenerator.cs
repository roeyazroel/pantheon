using System;
using System.Collections.Generic;
using Pantheon.Domain.Land;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Noise;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Simulation.Terrain;

/// <summary>
/// Generates the world heightfield, climate fields and per-sample biomes. The output is a pure
/// function of the seed, the settings and the land's reshapings (ADR-045).
/// </summary>
public static class TerrainGenerator
{
    /// <summary>Generates the terrain for a seed, as no Divine Act has reshaped it.</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="settings">Data-driven generation settings.</param>
    /// <returns>The generated terrain field.</returns>
    public static TerrainField Generate(WorldSeed seed, WorldGenSettings settings) => Generate(seed, settings, []);

    /// <summary>
    /// Generates the terrain for a seed with the land's reshapings applied, in order, to the ground
    /// before the rivers are found, so the water and the biomes follow the new land (ADR-045).
    /// </summary>
    /// <param name="seed">World seed.</param>
    /// <param name="settings">Data-driven generation settings.</param>
    /// <param name="reshapes">The reshapings, oldest first.</param>
    /// <returns>The generated terrain field.</returns>
    public static TerrainField Generate(WorldSeed seed, WorldGenSettings settings, IReadOnlyList<TerrainReshape> reshapes)
    {
        ArgumentNullException.ThrowIfNull(reshapes);
        WorldGenSettingsValidator.Validate(settings);
        var noise = new TerrainNoise(seed, settings);

        var resolution = settings.HeightmapResolution;
        var count = resolution * resolution;
        var heights = new float[count];
        var moisture = new float[count];
        var temperature = new float[count];

        for (var z = 0; z < resolution; z++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var index = (z * resolution) + x;
                var worldX = x * settings.VertexSpacing;
                var worldZ = z * settings.VertexSpacing;

                var height = ComputeHeight(noise, settings.Shape, resolution, x, z, worldX, worldZ);
                heights[index] = height;
                moisture[index] = ComputeMoisture(noise, settings.Climate, worldX, worldZ, height);
                temperature[index] = ComputeTemperature(noise, settings, worldX, worldZ, height);
            }
        }

        return Finish(seed, settings, noise, heights, moisture, temperature, reshapes);
    }

    /// <summary>
    /// The terrain of the same seed with these reshapings, made from an earlier terrain's ground
    /// without sampling the noise again: only the reshaped ground's climate, the rivers, the biomes
    /// and the landforms are found anew. The same as <see cref="Generate(WorldSeed, WorldGenSettings, IReadOnlyList{TerrainReshape})"/>.
    /// </summary>
    /// <param name="previous">The terrain of the same seed and settings, with any reshapings.</param>
    /// <param name="seed">World seed.</param>
    /// <param name="settings">Data-driven generation settings.</param>
    /// <param name="reshapes">The reshapings, oldest first.</param>
    /// <returns>The reshaped terrain.</returns>
    public static TerrainField Reshape(TerrainField previous, WorldSeed seed, WorldGenSettings settings, IReadOnlyList<TerrainReshape> reshapes)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(settings);
        var noise = new TerrainNoise(seed, settings);
        var ground = previous.Ground.ToArray();
        var moisture = previous.Moisture.ToArray();
        var temperature = previous.Temperature.ToArray();
        var resolution = settings.HeightmapResolution;
        for (var index = 0; index < ground.Length; index++)
        {
            if (previous.Heights[index] != ground[index])
            {
                // Ground a channel or an earlier reshaping moved starts again from the climate of the ground as it was made.
                var (worldX, worldZ) = (index % resolution * settings.VertexSpacing, index / resolution * settings.VertexSpacing);
                moisture[index] = ComputeMoisture(noise, settings.Climate, worldX, worldZ, ground[index]);
                temperature[index] = ComputeTemperature(noise, settings, worldX, worldZ, ground[index]);
            }
        }

        return Finish(seed, settings, noise, ground, moisture, temperature, reshapes);
    }

    /// <summary>Reshapes the ground, finds the water, classifies the biomes and marks the landforms.</summary>
    private static TerrainField Finish(WorldSeed seed, WorldGenSettings settings, TerrainNoise noise, float[] ground, float[] moisture, float[] temperature, IReadOnlyList<TerrainReshape> reshapes)
    {
        var resolution = settings.HeightmapResolution;
        var count = resolution * resolution;
        var heights = (float[])ground.Clone();
        var classifier = new BiomeClassifier(settings.Biomes);
        var biomes = new Biome[count];
        if (reshapes.Count > 0)
        {
            TerrainReshaper.Apply(reshapes, resolution, settings.VertexSpacing, heights);
            for (var index = 0; index < count; index++)
            {
                if (heights[index] != ground[index])
                {
                    var (worldX, worldZ) = (index % resolution * settings.VertexSpacing, index / resolution * settings.VertexSpacing);
                    moisture[index] = ComputeMoisture(noise, settings.Climate, worldX, worldZ, heights[index]);
                    temperature[index] = ComputeTemperature(noise, settings, worldX, worldZ, heights[index]);
                }
            }
        }

        // Rivers are cut into the land before the biomes are classified, so a channel that reaches below the sea is sea (ADR-022).
        var hydrology = settings.Hydrology is { } water
            ? HydrologyGenerator.Generate(seed, water, resolution, settings.VertexSpacing, heights, moisture)
            : HydrologyMap.Dry(count);
        for (var index = 0; index < count; index++)
        {
            biomes[index] = classifier.Classify(heights[index], temperature[index], moisture[index]);
        }

        // Islands, volcanic land, river valleys and coasts are where they are, whatever the climate (ADR-038).
        if (settings.Landforms is { } landforms)
        {
            LandformMarker.Mark(landforms, settings.RegionsPerSide, resolution, settings.VertexSpacing, heights, hydrology, biomes);
        }

        return new TerrainField(resolution, settings.VertexSpacing, heights, moisture, temperature, biomes, hydrology, ground);
    }

    /// <summary>Combines continent and mountain noise under the island mask, then converts to metres.</summary>
    private static float ComputeHeight(TerrainNoise noise, TerrainShapeSettings shape, int resolution, int x, int z, float worldX, float worldZ)
    {
        var continent = noise.Elevation.Sample01(worldX, worldZ);
        var ridges = noise.Ridges.Sample01(worldX, worldZ);

        // Mountains only rise from already high ground, so ranges sit inland instead of anywhere.
        var ridgeMask = SimMath.SmoothStep(shape.RidgeMaskStart, shape.RidgeMaskEnd, continent);
        var elevation = (continent + (shape.RidgeWeight * ridges * ridgeMask)) / (1f + shape.RidgeWeight);

        elevation *= IslandFalloff(shape, resolution, x, z);
        return ToMetres(shape, elevation);
    }

    /// <summary>Radial mask that keeps the centre at full height and sinks the edges into the sea.</summary>
    private static float IslandFalloff(TerrainShapeSettings shape, int resolution, int x, int z)
    {
        // Normalised offsets in -1..1, so the distance is 1 at the midpoint of each edge.
        var u = (x / (float)(resolution - 1) * 2f) - 1f;
        var v = (z / (float)(resolution - 1) * 2f) - 1f;
        var distance = MathF.Sqrt((u * u) + (v * v));
        return 1f - SimMath.SmoothStep(shape.IslandFalloffStart, shape.IslandFalloffEnd, distance);
    }

    /// <summary>Maps normalised elevation to metres: land is 0..MaxElevation and sea floor is 0..-MaxDepth.</summary>
    private static float ToMetres(TerrainShapeSettings shape, float elevation)
    {
        if (elevation >= shape.SeaLevel)
        {
            var land = (elevation - shape.SeaLevel) / (1f - shape.SeaLevel);
            return MathF.Pow(land, shape.LandExponent) * shape.MaxElevation;
        }

        var depth = (shape.SeaLevel - elevation) / shape.SeaLevel;
        return -depth * shape.MaxDepth;
    }

    /// <summary>Moisture noise plus a coastal bonus that fades with altitude. Water samples are saturated.</summary>
    private static float ComputeMoisture(TerrainNoise noise, ClimateSettings climate, float worldX, float worldZ, float height)
    {
        if (height < 0f)
        {
            return 1f;
        }

        var coastal = climate.CoastalMoistureBonus * (1f - SimMath.SmoothStep(0f, climate.CoastalMoistureRange, height));
        return SimMath.Clamp01(noise.Moisture.Sample01(worldX, worldZ) + coastal);
    }

    /// <summary>Base temperature with noise variation, minus a lapse rate that cools high ground.</summary>
    private static float ComputeTemperature(TerrainNoise noise, WorldGenSettings settings, float worldX, float worldZ, float height)
    {
        var climate = settings.Climate;
        var variation = climate.TemperatureVariation * ((noise.Temperature.Sample01(worldX, worldZ) * 2f) - 1f);
        var cooling = climate.LapseRate * MathF.Max(0f, height) / settings.Shape.MaxElevation;
        return SimMath.Clamp01(climate.BaseTemperature + variation - cooling);
    }

    /// <summary>The four independently seeded noise layers used by terrain generation.</summary>
    private sealed class TerrainNoise
    {
        /// <summary>Creates every layer with its own sub-seed, so the layers are uncorrelated.</summary>
        public TerrainNoise(WorldSeed seed, WorldGenSettings settings)
        {
            Elevation = new NoiseLayer(settings.ElevationNoise, SeedMixer.DeriveNoiseSeed(seed, "terrain.elevation"), NoiseFractal.Fbm);
            Ridges = new NoiseLayer(settings.RidgeNoise, SeedMixer.DeriveNoiseSeed(seed, "terrain.ridges"), NoiseFractal.Ridged);
            Moisture = new NoiseLayer(settings.MoistureNoise, SeedMixer.DeriveNoiseSeed(seed, "climate.moisture"), NoiseFractal.Fbm);
            Temperature = new NoiseLayer(settings.TemperatureNoise, SeedMixer.DeriveNoiseSeed(seed, "climate.temperature"), NoiseFractal.Fbm);
        }

        public NoiseLayer Elevation { get; }

        public NoiseLayer Ridges { get; }

        public NoiseLayer Moisture { get; }

        public NoiseLayer Temperature { get; }
    }
}
