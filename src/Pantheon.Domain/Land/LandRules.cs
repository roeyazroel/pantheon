namespace Pantheon.Domain.Land;

/// <summary>
/// The Divine Acts that reshape the land (spec §14, ADR-045), from <c>divine_rules.json</c>'s
/// <c>Land</c>. <see cref="None"/> reshapes nothing.
/// </summary>
public sealed record LandRules
{
    /// <summary>No reshaping.</summary>
    public static LandRules None { get; } = new();

    /// <summary>Whether the land can be reshaped.</summary>
    public bool Enabled => IslandRadius > 0f;

    /// <summary>A raised island's radius, in metres.</summary>
    public float IslandRadius { get; init; }

    /// <summary>A raised island's peak above the sea, in metres.</summary>
    public float IslandPeak { get; init; } = 14f;

    /// <summary>A split mountain's gorge, end to end, in metres.</summary>
    public float GorgeLength { get; init; } = 320f;

    /// <summary>The gorge's half width, in metres.</summary>
    public float GorgeWidth { get; init; } = 40f;

    /// <summary>The height, in metres, of ground that can be split.</summary>
    public float MountainHeight { get; init; } = 45f;

    /// <summary>How much lower, in metres, both ends of a gorge must lie than the mountain it splits.</summary>
    public float PassDrop { get; init; } = 15f;

    /// <summary>A new river channel's half width, in metres.</summary>
    public float ChannelWidth { get; init; } = 12f;

    /// <summary>How deep, in metres, the channel starts below the river it takes.</summary>
    public float ChannelDepth { get; init; } = 3f;

    /// <summary>How high, in metres, the dam across the old course stands above the river.</summary>
    public float DamHeight { get; init; } = 6f;

    /// <summary>How far downstream, in metres, the dam stands from where the channel leaves.</summary>
    public float DamOffset { get; init; } = 30f;

    /// <summary>The farthest a river may be, in metres, from where the god points it.</summary>
    public float DivertReach { get; init; } = 900f;

    /// <summary>How much lower, in metres, the new mouth must lie than the river where it leaves.</summary>
    public float MinFall { get; init; } = 3f;

    /// <summary>The shortest new course, in metres.</summary>
    public float MinChannel { get; init; } = 60f;
}
