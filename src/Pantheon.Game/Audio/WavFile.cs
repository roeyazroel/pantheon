using System;
using System.IO;

namespace Pantheon.Game.Audio;

/// <summary>Sound as a WAV file's bytes (16-bit PCM), to listen to the music outside the game (ADR-071).</summary>
public static class WavFile
{
    /// <summary>Samples (-1..1) as 16-bit little-endian PCM.</summary>
    /// <param name="samples">The samples, interleaved when stereo.</param>
    /// <returns>Two bytes a sample.</returns>
    public static byte[] Pcm16(float[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var data = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)Math.Clamp((int)MathF.Round(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
            data[2 * i] = (byte)(value & 0xff);
            data[(2 * i) + 1] = (byte)((value >> 8) & 0xff);
        }

        return data;
    }

    /// <summary>A whole WAV file: its header and the samples as 16-bit PCM.</summary>
    /// <param name="samples">The samples, interleaved when stereo.</param>
    /// <param name="rate">Samples a second.</param>
    /// <param name="channels">1 or 2.</param>
    /// <returns>The file's bytes.</returns>
    public static byte[] Write(float[] samples, int rate, int channels)
    {
        var pcm = Pcm16(samples);
        using var stream = new MemoryStream(44 + pcm.Length);
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + pcm.Length);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(rate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(pcm.Length);
            writer.Write(pcm);
        }

        return stream.ToArray();
    }
}
