using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;

namespace Pantheon.Game.Multiplayer;

/// <summary>
/// Keeps secrets from the players who should not know them (spec §39, §43, §45, ADR-025, ADR-026):
/// the host strips others' secret treaties from the world and diplomacy it sends each player, and
/// from the events, along with others' proxy acts and the casters of veiled miracles.
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

    /// <summary>
    /// The events a god may see: those of a secret treaty only for its parties, a proxy act only for
    /// its author, and a veiled miracle without its caster (ADR-026).
    /// </summary>
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
            ProxyActed proxy => proxy.GodId == godId,
            _ => true,
        }).Select(e => e is MiracleCast { Veiled: true } cast && cast.GodId != godId ? cast with { GodId = 0 } : e).ToList();
    }

    /// <summary>Whether any event must be filtered per player.</summary>
    /// <param name="events">The events.</param>
    /// <returns><c>true</c> when the events must be filtered per player.</returns>
    public static bool HasSecrets(IReadOnlyList<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events.Any(e => e is TreatySigned { Secret: true } or TreatyEnded { Secret: true } or ProxyActed or MiracleCast { Veiled: true });
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
