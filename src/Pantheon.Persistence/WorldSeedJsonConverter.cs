using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Writes a <see cref="WorldSeed"/> as a plain JSON number, for example <c>"Seed": 1234</c>.</summary>
public sealed class WorldSeedJsonConverter : JsonConverter<WorldSeed>
{
    /// <inheritdoc />
    public override WorldSeed Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt64(out var value))
        {
            throw new JsonException("A world seed must be an unsigned 64-bit integer.");
        }

        return new WorldSeed(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, WorldSeed value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.Value);
    }
}
