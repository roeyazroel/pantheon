using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Game.Presentation;

namespace Pantheon.Game.Diagnostics;

/// <summary>What streams near the camera (spec §84, ADR-050), as the client measured it.</summary>
/// <param name="ForestLoaded">Forest chunks planted.</param>
/// <param name="ForestChunks">All forest chunks.</param>
/// <param name="Terrain">Terrain chunks in full detail and all of them, or <c>null</c> when Terrain3D brings its own detail.</param>
/// <param name="FiresShown">Burning regions drawn.</param>
/// <param name="FiresBurning">All burning regions.</param>
/// <param name="Settings">The streaming settings, for the radii.</param>
public sealed record StreamingStatus(int ForestLoaded, int ForestChunks, (int Detailed, int Chunks)? Terrain, int FiresShown, int FiresBurning, StreamingSettings Settings);

/// <summary>The F1 panel's streaming line (ADR-050). Pure.</summary>
public static class StreamingReadout
{
    /// <summary>Adds the streaming line when the client measured it.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Streaming is { } status)
        {
            lines.Add(Describe(status));
        }
    }

    /// <summary>The line.</summary>
    /// <param name="status">What streams now.</param>
    /// <returns>For example "Streaming: forests planted in 192 of 2209 chunks within 2100 m; terrain in detail in 6 of 2209 chunks within 650 m; fires drawn 3 of 5 within 2500 m".</returns>
    public static string Describe(StreamingStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var settings = status.Settings;
        var terrain = status.Terrain is { } detail
            ? string.Create(CultureInfo.InvariantCulture, $"terrain in detail in {detail.Detailed} of {detail.Chunks} chunks within {settings.TerrainDetailRadius:0} m")
            : "terrain in detail by Terrain3D's own distance";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Streaming: forests planted in {status.ForestLoaded} of {status.ForestChunks} chunks within {settings.ForestLoadRadius:0} m; {terrain}; fires drawn {status.FiresShown} of {status.FiresBurning} within {settings.EffectRadius:0} m");
    }
}
