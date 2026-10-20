using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Pantheon.Persistence.Profile;

/// <summary>
/// The profile in one SQLite file (ADR-060): a table of values (settings, the tutorial's progress), a
/// table of worlds and a table of their saves. The schema carries its version, so later builds can
/// change it in steps.
/// </summary>
public sealed class SqliteProfileStore : IProfileStore
{
    /// <summary>The schema this build writes.</summary>
    public const int SchemaVersion = 1;

    private static readonly string[] Schema =
    [
        "CREATE TABLE IF NOT EXISTS profile_values (key TEXT PRIMARY KEY, value TEXT NOT NULL)",
        """
        CREATE TABLE IF NOT EXISTS worlds (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL, god TEXT NOT NULL, seed TEXT NOT NULL, size TEXT NOT NULL,
            victory TEXT NOT NULL, seasons INTEGER NOT NULL, species TEXT NOT NULL,
            created_utc TEXT NOT NULL, played_utc TEXT NOT NULL, play_seconds REAL NOT NULL,
            tick INTEGER NOT NULL, followers INTEGER NOT NULL, rank TEXT NOT NULL, outcome TEXT NOT NULL)
        """,
        """
        CREATE TABLE IF NOT EXISTS saves (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            world_id INTEGER NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
            kind TEXT NOT NULL, name TEXT NOT NULL, tick INTEGER NOT NULL, saved_utc TEXT NOT NULL,
            version INTEGER NOT NULL, data BLOB NOT NULL)
        """,
        "CREATE INDEX IF NOT EXISTS saves_by_world ON saves (world_id, saved_utc)",
    ];

    private const string WorldColumns = "id, name, god, seed, size, victory, seasons, species, created_utc, played_utc, play_seconds, tick, followers, rank, outcome";

    private readonly SqliteConnection _connection;

    private SqliteProfileStore(SqliteConnection connection, string version)
    {
        _connection = connection;
        Kind = "SQLite " + version;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <summary>Opens (or creates) the profile at a file, bringing its schema up to date.</summary>
    /// <param name="path">The database file; <c>:memory:</c> for a profile that is never written.</param>
    /// <returns>The store.</returns>
    /// <exception cref="SqliteException">The file cannot be opened as a database.</exception>
    public static SqliteProfileStore Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        try
        {
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys = ON");
            Execute(connection, "PRAGMA journal_mode = WAL");
            foreach (var statement in Schema)
            {
                Execute(connection, statement);
            }

            using var version = connection.CreateCommand();
            version.CommandText = "SELECT sqlite_version()";
            var store = new SqliteProfileStore(connection, Convert.ToString(version.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "?");
            store.WriteValue("schema_version", SchemaVersion.ToString(CultureInfo.InvariantCulture));
            return store;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public string? ReadValue(string key)
    {
        using var command = Command("SELECT value FROM profile_values WHERE key = $key", ("$key", key));
        return command.ExecuteScalar() as string;
    }

    /// <inheritdoc />
    public void WriteValue(string key, string value)
    {
        using var command = Command("INSERT INTO profile_values (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value", ("$key", key), ("$value", value));
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public WorldRecord AddWorld(WorldRecord world)
    {
        ArgumentNullException.ThrowIfNull(world);
        using var command = Command(
            "INSERT INTO worlds (name, god, seed, size, victory, seasons, species, created_utc, played_utc, play_seconds, tick, followers, rank, outcome) "
            + "VALUES ($name, $god, $seed, $size, $victory, $seasons, $species, $created, $played, $seconds, $tick, $followers, $rank, $outcome) RETURNING id",
            WorldParameters(world));
        return world with { Id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) };
    }

    /// <inheritdoc />
    public void UpdateWorld(WorldRecord world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var parameters = new List<(string, object)>(WorldParameters(world)) { ("$id", world.Id) };
        using var command = Command(
            "UPDATE worlds SET name = $name, god = $god, seed = $seed, size = $size, victory = $victory, seasons = $seasons, species = $species, "
            + "created_utc = $created, played_utc = $played, play_seconds = $seconds, tick = $tick, followers = $followers, rank = $rank, outcome = $outcome WHERE id = $id",
            [.. parameters]);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<WorldRecord> Worlds()
    {
        using var command = Command($"SELECT {WorldColumns} FROM worlds ORDER BY played_utc DESC, id DESC");
        using var reader = command.ExecuteReader();
        var worlds = new List<WorldRecord>();
        while (reader.Read())
        {
            worlds.Add(new WorldRecord
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                God = reader.GetString(2),
                Seed = ulong.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                Size = reader.GetString(4),
                Victory = reader.GetString(5),
                Seasons = reader.GetInt64(6) != 0,
                Species = reader.GetString(7),
                CreatedUtc = Time(reader.GetString(8)),
                PlayedUtc = Time(reader.GetString(9)),
                PlaySeconds = reader.GetDouble(10),
                Tick = reader.GetInt64(11),
                Followers = (int)reader.GetInt64(12),
                Rank = reader.GetString(13),
                Outcome = reader.GetString(14),
            });
        }

        return worlds;
    }

    /// <inheritdoc />
    public void DeleteWorld(long worldId)
    {
        using var command = Command("DELETE FROM worlds WHERE id = $id", ("$id", worldId));
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public SaveRecord WriteSave(long worldId, SaveKind kind, string name, long tick, DateTime savedUtc, int version, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var transaction = _connection.BeginTransaction();
        if (kind != SaveKind.Manual)
        {
            using var clear = Command("DELETE FROM saves WHERE world_id = $world AND kind = $kind", ("$world", worldId), ("$kind", kind.ToString()));
            clear.Transaction = transaction;
            clear.ExecuteNonQuery();
        }

        using var insert = Command(
            "INSERT INTO saves (world_id, kind, name, tick, saved_utc, version, data) VALUES ($world, $kind, $name, $tick, $saved, $version, $data) RETURNING id",
            ("$world", worldId), ("$kind", kind.ToString()), ("$name", name), ("$tick", tick), ("$saved", Stamp(savedUtc)), ("$version", version), ("$data", data));
        insert.Transaction = transaction;
        var id = Convert.ToInt64(insert.ExecuteScalar(), CultureInfo.InvariantCulture);
        transaction.Commit();
        return new SaveRecord(id, worldId, kind, name, tick, savedUtc, version, data.LongLength);
    }

    /// <inheritdoc />
    public IReadOnlyList<SaveRecord> Saves(long worldId)
    {
        using var command = Command("SELECT id, world_id, kind, name, tick, saved_utc, version, length(data) FROM saves WHERE world_id = $world ORDER BY saved_utc DESC, id DESC", ("$world", worldId));
        using var reader = command.ExecuteReader();
        var saves = new List<SaveRecord>();
        while (reader.Read())
        {
            saves.Add(new SaveRecord(
                reader.GetInt64(0),
                reader.GetInt64(1),
                Enum.TryParse<SaveKind>(reader.GetString(2), out var kind) ? kind : SaveKind.Manual,
                reader.GetString(3),
                reader.GetInt64(4),
                Time(reader.GetString(5)),
                (int)reader.GetInt64(6),
                reader.GetInt64(7)));
        }

        return saves;
    }

    /// <inheritdoc />
    public byte[]? ReadSave(long saveId)
    {
        using var command = Command("SELECT data FROM saves WHERE id = $id", ("$id", saveId));
        return command.ExecuteScalar() as byte[];
    }

    /// <inheritdoc />
    public void DeleteSave(long saveId)
    {
        using var command = Command("DELETE FROM saves WHERE id = $id", ("$id", saveId));
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();

    /// <summary>A world's columns as parameters.</summary>
    private static (string, object)[] WorldParameters(WorldRecord world) =>
    [
        ("$name", world.Name), ("$god", world.God), ("$seed", world.Seed.ToString(CultureInfo.InvariantCulture)), ("$size", world.Size),
        ("$victory", world.Victory), ("$seasons", world.Seasons ? 1 : 0), ("$species", world.Species), ("$created", Stamp(world.CreatedUtc)),
        ("$played", Stamp(world.PlayedUtc)), ("$seconds", world.PlaySeconds), ("$tick", world.Tick), ("$followers", world.Followers),
        ("$rank", world.Rank), ("$outcome", world.Outcome),
    ];

    /// <summary>A command with its parameters.</summary>
    private SqliteCommand Command(string sql, params (string Name, object Value)[] parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }

    /// <summary>Runs a statement on a connection.</summary>
    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>A time as stored: ISO 8601 in UTC, which sorts as text.</summary>
    private static string Stamp(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

    /// <summary>A stored time.</summary>
    private static DateTime Time(string text) => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}
