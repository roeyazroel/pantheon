using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pantheon.Persistence;

/// <summary>Helpers shared by the save migration steps for editing a save's JSON tree.</summary>
internal static class SaveJson
{
    /// <summary>Adds a member that the older version could not contain; a present one means the save was edited or corrupted.</summary>
    internal static void AddNode(JsonObject target, string name, JsonNode value, int fromVersion)
    {
        if (target.ContainsKey(name))
        {
            throw new SaveFormatException($"A version {fromVersion} save must not contain '{name}'.");
        }

        target[name] = value;
    }

    /// <summary>Adds a member that the older version could not contain; a present one means the save was edited or corrupted.</summary>
    internal static void AddMember(JsonObject target, string name, float value, int fromVersion)
    {
        if (target.ContainsKey(name))
        {
            throw new SaveFormatException($"A version {fromVersion} save must not contain '{name}'.");
        }

        target[name] = value;
    }

    /// <summary>Adds a member, set to null, that the older version could not contain.</summary>
    internal static void AddNullMember(JsonObject target, string name, int fromVersion)
    {
        if (target.ContainsKey(name))
        {
            throw new SaveFormatException($"A version {fromVersion} save must not contain '{name}'.");
        }

        target[name] = null;
    }

    /// <summary>
    /// A number in the tree, whether it was parsed from text or written by an earlier step (which
    /// may hold a float, an int or a double); NaN when the node is not a number.
    /// </summary>
    internal static double ReadNumber(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number
            ? double.Parse(value.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture)
            : double.NaN;
}
