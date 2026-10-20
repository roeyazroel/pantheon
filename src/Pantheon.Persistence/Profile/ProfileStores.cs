using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Pantheon.Persistence.Profile;

/// <summary>Opens the player's profile (ADR-060), and packs saves for it.</summary>
public static class ProfileStores
{
    /// <summary>The database's file name in the profile's directory.</summary>
    public const string DatabaseName = "pantheon.db";

    /// <summary>
    /// Opens the profile in a directory: the SQLite database, or, when SQLite's native library or
    /// the file cannot be opened, plain files beside it.
    /// </summary>
    /// <param name="directory">The profile's directory (the game's user directory).</param>
    /// <param name="problem">Why SQLite was not used, or <c>null</c> when it was.</param>
    /// <returns>The store.</returns>
    public static IProfileStore Open(string directory, out string? problem)
    {
        Directory.CreateDirectory(directory);
        try
        {
            problem = null;
            return SqliteProfileStore.Open(Path.Combine(directory, DatabaseName));
        }
        catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or DllNotFoundException or TypeInitializationException or EntryPointNotFoundException or BadImageFormatException or IOException or UnauthorizedAccessException)
        {
            problem = exception.GetBaseException().Message;
            return FileProfileStore.Open(Path.Combine(directory, "profile"));
        }
    }

    /// <summary>A save's JSON, compressed for the profile.</summary>
    /// <param name="json">The save's JSON.</param>
    /// <returns>Its gzip.</returns>
    public static byte[] Pack(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            gzip.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    /// <summary>A save's JSON from its packed form.</summary>
    /// <param name="data">The gzip.</param>
    /// <returns>The JSON.</returns>
    /// <exception cref="SaveFormatException">The data is not a packed save.</exception>
    public static string Unpack(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            using var input = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
            using var reader = new StreamReader(input, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (InvalidDataException exception)
        {
            throw new SaveFormatException("The stored save is damaged.", exception);
        }
    }
}
