using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pantheon.Game.Presentation;

/// <summary>Reads and writes <see cref="RgbColor"/> as <c>"#rrggbb"</c>.</summary>
public sealed class RgbColorJsonConverter : JsonConverter<RgbColor>
{
    /// <inheritdoc />
    public override RgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A colour must be a \"#rrggbb\" string.");
        }

        try
        {
            return RgbColor.ParseHex(reader.GetString() ?? string.Empty);
        }
        catch (FormatException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RgbColor value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToHex());
    }
}
