using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Pantheon.Persistence;

/// <summary>
/// Shared System.Text.Json configuration. Property names match the C# contracts (PascalCase),
/// enums are written as names, and unknown, missing or null-for-non-nullable members are errors.
/// </summary>
public static class PantheonJson
{
    /// <summary>Options for save files: strict and indented.</summary>
    public static JsonSerializerOptions SaveOptions { get; } = CreateOptions(allowComments: false);

    /// <summary>Options for hand-edited content files: like <see cref="SaveOptions"/>, but comments and trailing commas are allowed.</summary>
    public static JsonSerializerOptions ContentOptions { get; } = CreateOptions(allowComments: true);

    /// <summary>Options for the network (ADR-019): like <see cref="SaveOptions"/>, but compact.</summary>
    public static JsonSerializerOptions WireOptions { get; } = CreateOptions(allowComments: false, indented: false);

    /// <summary>Builds a read-only options instance.</summary>
    private static JsonSerializerOptions CreateOptions(bool allowComments, bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            ReadCommentHandling = allowComments ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow,
            AllowTrailingCommas = allowComments,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { RejectNullForNonNullableMembers } },
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.Converters.Add(new WorldSeedJsonConverter());
        options.MakeReadOnly();
        return options;
    }

    /// <summary>
    /// .NET 8 ignores nullable annotations while reading, so an explicit <c>null</c> would slip
    /// into a non-nullable member. This modifier makes such values a <see cref="JsonException"/>.
    /// </summary>
    private static void RejectNullForNonNullableMembers(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        var nullability = new NullabilityInfoContext();
        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is not PropertyInfo member || member.PropertyType.IsValueType || property.Set is not { } setter)
            {
                continue;
            }

            if (nullability.Create(member).WriteState != NullabilityState.NotNull)
            {
                continue;
            }

            var name = property.Name;
            property.Set = (target, value) => setter(target, value ?? throw new JsonException($"'{name}' must not be null."));
        }
    }
}
