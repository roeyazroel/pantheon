using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;

namespace Pantheon.Game.Multiplayer;

/// <summary>
/// Keeps secret treaties from the players who are not party to them (spec §39, §43, ADR-025): the
/// host strips them from the world and diplomacy it sends each player, and from the events.
/// </summary>
public static class SecretFilter
{
    /// <summary>Whether the world holds a secret treaty, so what goes out must be filtered per player.</summary>
    /// <param name="world">The host's world.</param>
    /// <returns><c>true</c> with a secret.</returns>
    public static bool HasSecrets(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Treaties.Exists(t => t.Secret);
    }

    /// <summary>A whole world as JSON, without the secret treaties a god is not party to.</summary>
    /// <param name="json">The world's save JSON (with its version).</param>
    /// <param name="godId">The player's god.</param>
    /// <returns>The filtered JSON.</returns>
    public static string World(string json, int godId)
    {
        var root = JsonNode.Parse(json)!;
        if (root["World"]?["Diplomacy"] is JsonObject diplomacy)
        {
            Strip(diplomacy, godId);
        }

        return root.ToJsonString();
    }

    /// <summary>The diplomacy section as JSON, without the secret treaties a god is not party to.</summary>
    /// <param name="json">The section's JSON.</param>
    /// <param name="godId">The player's god.</param>
    /// <returns>The filtered JSON.</returns>
    public static string Diplomacy(string json, int godId)
    {
        var root = JsonNode.Parse(json)!;
        if (root is JsonObject diplomacy)
        {
            Strip(diplomacy, godId);
        }

        return root.ToJsonString();
    }

    /// <summary>The events a god may see: those of a secret treaty only for its parties.</summary>
    /// <param name="events">The events.</param>
    /// <param name="godId">The player's god.</param>
    /// <returns>The events it may see.</returns>
    public static IReadOnlyList<WorldEvent> Events(IReadOnlyList<WorldEvent> events, int godId)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events.Where(e => e switch
        {
            TreatySigned { Secret: true } signed => signed.FirstGodId == godId || signed.SecondGodId == godId,
            TreatyEnded { Secret: true } ended => ended.FirstGodId == godId || ended.SecondGodId == godId,
            _ => true,
        }).ToList();
    }

    /// <summary>Whether any event belongs to a secret treaty.</summary>
    /// <param name="events">The events.</param>
    /// <returns><c>true</c> when the events must be filtered per player.</returns>
    public static bool HasSecrets(IReadOnlyList<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events.Any(e => e is TreatySigned { Secret: true } or TreatyEnded { Secret: true });
    }

    /// <summary>Removes the secret treaties a god is not party to.</summary>
    private static void Strip(JsonObject diplomacy, int godId)
    {
        if (diplomacy["Treaties"] is not JsonArray treaties)
        {
            return;
        }

        for (var i = treaties.Count - 1; i >= 0; i--)
        {
            if (treaties[i] is JsonObject treaty && treaty["Secret"]?.GetValue<bool>() == true
                && treaty["FirstGodId"]?.GetValue<int>() != godId && treaty["SecondGodId"]?.GetValue<int>() != godId)
            {
                treaties.RemoveAt(i);
            }
        }
    }
}
