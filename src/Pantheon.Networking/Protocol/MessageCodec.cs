using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pantheon.Networking.Protocol;

/// <summary>
/// Turns messages into packets and back (ADR-018): UTF-8 JSON after a one-byte header that says
/// whether the rest is compressed. Messages from <see cref="NetworkProtocol.CompressFromBytes"/>
/// bytes on, such as world snapshots, travel gzipped.
/// </summary>
public static class MessageCodec
{
    private const byte Plain = 0;
    private const byte Gzipped = 1;

    /// <summary>The JSON options of every message: enums by name, as in saves.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Encodes a message as a packet.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The packet.</returns>
    public static byte[] Encode(NetMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var json = JsonSerializer.SerializeToUtf8Bytes(message, Options);
        if (json.Length < NetworkProtocol.CompressFromBytes)
        {
            var plain = new byte[json.Length + 1];
            plain[0] = Plain;
            json.CopyTo(plain, 1);
            return plain;
        }

        using var buffer = new MemoryStream();
        buffer.WriteByte(Gzipped);
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(json);
        }

        return buffer.ToArray();
    }

    /// <summary>Decodes a packet.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The message.</returns>
    /// <exception cref="InvalidDataException">The packet is not a message of this protocol.</exception>
    public static NetMessage Decode(byte[] packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Length < 2)
        {
            throw new InvalidDataException("A packet holds a header byte and a message.");
        }

        try
        {
            var json = packet[0] switch
            {
                Plain => packet.AsSpan(1).ToArray(),
                Gzipped => Inflate(packet),
                _ => throw new InvalidDataException($"Unknown packet header {packet[0]}."),
            };
            return JsonSerializer.Deserialize<NetMessage>(json, Options) ?? throw new InvalidDataException("The packet holds no message.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // A message without its Type tag cannot be built, which System.Text.Json reports as not supported.
            throw new InvalidDataException("The packet is not a valid message: " + exception.Message, exception);
        }
    }

    /// <summary>A short description of a packet's message for logs, without decoding a snapshot's world.</summary>
    /// <param name="message">The message.</param>
    /// <returns>Its type name.</returns>
    public static string Describe(NetMessage message) => message?.GetType().Name ?? "nothing";

    /// <summary>Unzips a compressed packet's JSON.</summary>
    private static byte[] Inflate(byte[] packet)
    {
        using var input = new MemoryStream(packet, 1, packet.Length - 1);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Strict options: enums as strings, no comments, case-sensitive names.</summary>
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    /// <summary>UTF-8 text of a packet's JSON, for tests and logs.</summary>
    /// <param name="message">The message.</param>
    /// <returns>Its JSON.</returns>
    public static string ToJson(NetMessage message) => Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(message, Options));
}
