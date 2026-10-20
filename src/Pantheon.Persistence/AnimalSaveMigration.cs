using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 40 (ADR-061), run by <see cref="SaveMigrations"/>.</summary>
internal static class AnimalSaveMigration
{
    /// <summary>The four species of before and the animals that take their places, by their natures.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Animals = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Mossback"] = "Cow",
        ["Stonehide"] = "Tortoise",
        ["Swiftclaw"] = "Tiger",
        ["Lumen"] = "Ape",
    };

    /// <summary>
    /// Version 33 to 34 (ADR-061): the Creatures become upright animals. A Mossback (patient and
    /// steady) is a Cow, a Stonehide (hard to hurt, strong and slow) a Tortoise, a Swiftclaw (fast and
    /// quick to anger) a Tiger and a Lumen (quick to learn, frail) an Ape, with the same factors.
    /// </summary>
    internal static void UpgradeFrom33(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Creatures"] is not JsonArray creatures)
        {
            return;
        }

        foreach (var node in creatures)
        {
            if (node is JsonObject creature && creature["Species"]?.GetValue<string>() is { } species && Animals.TryGetValue(species, out var animal))
            {
                creature["Species"] = animal;
            }
        }
    }
}
