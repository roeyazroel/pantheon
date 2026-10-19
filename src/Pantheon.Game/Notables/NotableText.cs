using System;
using System.Globalization;
using Pantheon.Domain.Notables;
using Pantheon.Domain.World;

namespace Pantheon.Game.Notables;

/// <summary>Words for prophets and elders (ADR-032). Pure and culture-invariant.</summary>
public static class NotableText
{
    /// <summary>A notable's title.</summary>
    /// <param name="notable">The notable.</param>
    /// <param name="world">World state, for names.</param>
    /// <returns>For example "Prophet Asha of Seren" or "Elder Bram of Elar".</returns>
    public static string Title(NotableState notable, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(notable);
        ArgumentNullException.ThrowIfNull(world);
        return notable.Kind switch
        {
            NotableKind.Prophet => Format("Prophet {0} of {1}", notable.Name, God(world, notable.GodId)),
            NotableKind.Elder => Format("Elder {0} of {1}", notable.Name, Village(world, notable.SettlementId)),
        };
    }

    /// <summary>What a notable is doing.</summary>
    /// <param name="notable">The notable.</param>
    /// <param name="world">World state, for names.</param>
    /// <returns>For example "walking to Elar".</returns>
    public static string Doing(NotableState notable, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(notable);
        ArgumentNullException.ThrowIfNull(world);
        return notable.Kind switch
        {
            NotableKind.Prophet => notable.TargetSettlementId is { } target ? "walking to " + Village(world, target) : "preaching in " + Village(world, notable.SettlementId),
            NotableKind.Elder => Format("keeping the faith of {0} in {1}", Village(world, notable.SettlementId), God(world, notable.GodId)),
        };
    }

    /// <summary>One thing a notable remembers.</summary>
    /// <param name="memory">The memory.</param>
    /// <param name="world">World state, for names.</param>
    /// <returns>For example "preached in Elar and won 4".</returns>
    public static string Memory(NotableMemory memory, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);
        var place = memory.SettlementId is { } id ? Village(world, id) : "the wilds";
        return memory.Deed switch
        {
            NotableDeed.Rose => "rose in " + place,
            NotableDeed.Preached => Format("preached in {0} and won {1}", place, memory.Count),
            NotableDeed.Endured => "lived through danger in " + place,
            NotableDeed.Turned => "turned from their god in " + place,
        };
    }

    /// <summary>A god's name.</summary>
    internal static string God(WorldState world, int godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a forgotten god";

    /// <summary>A village's name.</summary>
    internal static string Village(WorldState world, int settlementId) => world.Settlements.Find(s => s.Id == settlementId)?.Name ?? "a lost village";

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
