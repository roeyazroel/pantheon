using System.Text.Json.Serialization;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>
/// Envelope written to disk. Only <see cref="WorldState"/> is stored; the SceneTree never is
/// (ADR-006).
/// </summary>
public sealed class SaveGame
{
    /// <summary>Format version. It is written first so it can be read before the rest of the file.</summary>
    [JsonPropertyOrder(-1)]
    public required int SaveVersion { get; init; }

    /// <summary>The saved world.</summary>
    public required WorldState World { get; init; }
}
