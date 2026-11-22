using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;

namespace Pantheon.Domain.World;

/// <summary>
/// The sizes a new world may be made at (spec §6, ADR-027), and the choices the New World page offers
/// beside them (ADR-082), in <c>world_sizes.json</c>.
/// </summary>
public sealed record WorldSizes
{
    /// <summary>The sizes, smallest first; the first is the world <c>worldgen.json</c> describes.</summary>
    public required IReadOnlyList<WorldSizePreset> Sizes { get; init; }

    /// <summary>Settlement names added to <c>worldgen.json</c>'s for worlds with more settlements.</summary>
    public required IReadOnlyList<string> ExtraSettlementNames { get; init; }

    /// <summary>Settlement names added after <see cref="ExtraSettlementNames"/> only when a world of many villages needs more (ADR-082).</summary>
    public required IReadOnlyList<string> MoreSettlementNames { get; init; }

    /// <summary>River names added to <c>worldgen.json</c>'s for worlds with more rivers.</summary>
    public required IReadOnlyList<string> ExtraRiverNames { get; init; }

    /// <summary>Villages each god needs: a world of <c>n</c> villages holds at most <c>n / VillagesPerGod</c> gods (ADR-082).</summary>
    public required int VillagesPerGod { get; init; }

    /// <summary>The share of a world's gods, rounded down, that may win a pantheon victory together (ADR-082).</summary>
    public required float PantheonShare { get; init; }

    /// <summary>The share of a size's villages an island of few villages is founded with, rounded.</summary>
    public required float FewVillages { get; init; }

    /// <summary>The share of a size's villages an island of many villages is founded with, rounded.</summary>
    public required float ManyVillages { get; init; }

    /// <summary>The times, in minutes, a victory may be held for (spec §53); the divine rules' <c>VictorySeconds</c> is one of them.</summary>
    public required IReadOnlyList<int> HoldMinutes { get; init; }

    /// <summary>The personalities rivals may be born with for a peaceful or warlike temper; a mixed one draws from all.</summary>
    public required RivalTempers Tempers { get; init; }

    /// <summary>Only the world <c>worldgen.json</c> describes.</summary>
    public static WorldSizes Default { get; } = new()
    {
        Sizes = [],
        ExtraSettlementNames = [],
        MoreSettlementNames = [],
        ExtraRiverNames = [],
        VillagesPerGod = 2,
        PantheonShare = 0.5f,
        FewVillages = 1f,
        ManyVillages = 1f,
        HoldMinutes = [],
        Tempers = new RivalTempers { Peaceful = [], Warlike = [] },
    };

    /// <summary>The personalities a temper lets rivals draw, in the enum's order; every personality but none for a mixed one.</summary>
    /// <param name="temper">The temper.</param>
    /// <returns>The personalities.</returns>
    public IReadOnlyList<GodPersonality> PersonalitiesOf(RivalTemper temper)
    {
        var all = new List<GodPersonality>();
        foreach (var personality in Enum.GetValues<GodPersonality>())
        {
            if (personality != GodPersonality.None)
            {
                all.Add(personality);
            }
        }

        var chosen = temper switch
        {
            RivalTemper.Mixed => null,
            RivalTemper.Peaceful => Tempers.Peaceful,
            RivalTemper.Warlike => Tempers.Warlike,
        };
        return chosen is null || chosen.Count == 0 ? all : all.FindAll(p => chosen.Contains(p));
    }
}

/// <summary>The personalities of the rivals' tempers (ADR-082).</summary>
public sealed record RivalTempers
{
    /// <summary>The kinder personalities.</summary>
    public required IReadOnlyList<GodPersonality> Peaceful { get; init; }

    /// <summary>The harsher personalities.</summary>
    public required IReadOnlyList<GodPersonality> Warlike { get; init; }
}

/// <summary>One world size: how large the island is and how many settlements, gods and rivers it holds.</summary>
public sealed record WorldSizePreset
{
    /// <summary>Its name, for example "Large".</summary>
    public required string Name { get; init; }

    /// <summary>Heightmap samples per side.</summary>
    public required int HeightmapResolution { get; init; }

    /// <summary>Metres between samples.</summary>
    public required float VertexSpacing { get; init; }

    /// <summary>Regions per side.</summary>
    public required int RegionsPerSide { get; init; }

    /// <summary>Settlements founded at the start.</summary>
    public required int Settlements { get; init; }

    /// <summary>Gods offered first: <c>gods.json</c>'s gods, then its extra gods; the player may choose another number (ADR-082).</summary>
    public required int Gods { get; init; }

    /// <summary>The most rivers, and of them the most that run to the sea.</summary>
    public required int Rivers { get; init; }

    /// <summary>The most rivers that run to the sea.</summary>
    public required int MainRivers { get; init; }

    /// <summary>The hydrology's cell size in metres.</summary>
    public required float HydrologyCellSize { get; init; }
}
