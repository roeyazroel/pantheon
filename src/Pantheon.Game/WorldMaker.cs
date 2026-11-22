using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Game;

/// <summary>How a new world is made (ADR-027, ADR-082): the player's god and Creature, the world's size, gods and villages, how it may be won, its climate, its rivals and its calamities.</summary>
/// <param name="Species">The species of the player's Creature; <c>gods.json</c>'s when <c>null</c>.</param>
/// <param name="Size">The world size's name; the first (the world <c>worldgen.json</c> describes) when <c>null</c>.</param>
/// <param name="Victory">How it may be won; ascendancy only when <c>null</c>.</param>
/// <param name="Seasons">Whether the year turns through four seasons (spec §17, ADR-028).</param>
/// <param name="Born">The rival gods as they were born (ADR-065): their names, Creatures, personalities and personas; the content's gods when <c>null</c>.</param>
/// <param name="GodsMet">Whether the gods know one another from the start (ADR-068), rather than meeting as their lands grow (ADR-036).</param>
public sealed record NewWorldOptions(string? Species = null, string? Size = null, VictorySettings? Victory = null, bool Seasons = true, Minds.BornPantheon? Born = null, bool GodsMet = false)
{
    /// <summary>The player's god's name (ADR-082); <c>gods.json</c>'s when <c>null</c>, empty or too long.</summary>
    public string? GodName { get; init; }

    /// <summary>The player's Creature's name; <c>gods.json</c>'s when <c>null</c>, empty or too long.</summary>
    public string? CreatureName { get; init; }

    /// <summary>How many gods the world holds, the player's among them; the size's own when <c>null</c>, within <see cref="WorldChoices.MaxGods"/>.</summary>
    public int? Gods { get; init; }

    /// <summary>How many villages the island is founded with for its size.</summary>
    public VillageCount Villages { get; init; } = VillageCount.Normal;

    /// <summary>The player's god's colour, an index into <c>divine_view.json</c>'s god colours; its id's when <c>null</c>.</summary>
    public int? Color { get; init; }

    /// <summary>Which personalities the rivals may be born with.</summary>
    public RivalTemper Temper { get; init; } = RivalTemper.Mixed;

    /// <summary>How strongly the AI gods play.</summary>
    public RivalStrength Rivals { get; init; } = RivalStrength.Even;

    /// <summary>How often calamities strike.</summary>
    public CalamityLevel Calamities { get; init; } = CalamityLevel.Normal;

    /// <summary>How long a victory must be held, in minutes; the victory's own, or the divine rules', when <c>null</c>.</summary>
    public int? HoldMinutes { get; init; }
}

/// <summary>Makes new worlds at a size, with their gods and victories (spec §6, §53, ADR-027).</summary>
public static class WorldMaker
{
    /// <summary>Generates a world.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="options">How to make it.</param>
    /// <returns>The world and its terrain.</returns>
    public static GeneratedWorld Generate(GameContent content, WorldSeed seed, NewWorldOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);
        var (worldGen, _) = AtSize(content, options.Size, options.Villages);
        var pantheon = PantheonFor(content, options);
        var born = options.Born is { } given && given.Gods.Count == pantheon.Gods.Count ? given : null;
        var generated = WorldGenerator.Generate(seed, worldGen, born?.Apply(pantheon) ?? pantheon, content.Creatures, content.HolySites);
        if (born is not null)
        {
            // The founder makes the gods in the slots' order, from id 1 (ADR-065).
            foreach (var god in generated.State.Gods)
            {
                god.Persona = god.Id >= 1 && god.Id <= born.Gods.Count ? born.Gods[god.Id - 1].Persona : null;
            }
        }

        Settle(content, options, generated.State);
        return generated;
    }

    /// <summary>The choices a made world keeps (ADR-027, ADR-082): its victory and hold, seasons, setup, rivals' power, the player's colour and whether the gods have met.</summary>
    private static void Settle(GameContent content, NewWorldOptions options, WorldState world)
    {
        var victory = options.Victory ?? VictorySettings.Default;
        world.Victory = victory with
        {
            PantheonMaxGods = victory.PantheonMaxGods ?? WorldChoices.PantheonMaxGods(content.Sizes, world.Gods.Count),
            HoldSeconds = options.HoldMinutes is > 0 and var minutes ? minutes * 60f : victory.HoldSeconds ?? content.DivineRules.VictorySeconds,
        };
        world.Seasons = options.Seasons ? new Domain.Weather.SeasonState { OriginTick = 0 } : null;
        world.Setup = new WorldSetup { Calamities = options.Calamities, Rivals = options.Rivals, Temper = options.Temper, Villages = options.Villages };
        var power = content.GodAi.Strengths.Of(options.Rivals).Power;
        foreach (var god in world.Gods)
        {
            god.DivinePower = god.Controller == GodController.Ai ? god.DivinePower * power : god.DivinePower;
        }

        Paint(world, options.Color);
        if (options.GodsMet)
        {
            // Known to one another from the start (ADR-068): met before the world's history begins, so it records no meetings.
            foreach (var god in world.Gods)
            {
                foreach (var other in world.Gods)
                {
                    Simulation.Diplomacy.Acquaintance.Introduce(world, god.Id, other.Id, Domain.Events.MeetingWay.Council, null, []);
                }
            }
        }
    }

    /// <summary>
    /// Gives the player's god the colour chosen (ADR-082), and the god that wore it by its id the
    /// player's own, so no two gods share one. Gods wear their ids' colours otherwise.
    /// </summary>
    private static void Paint(WorldState world, int? color)
    {
        var player = world.Gods.Find(g => g.Controller == GodController.Human);
        if (player is null || color is not { } chosen || chosen < 0 || chosen == player.Id - 1)
        {
            return;
        }

        player.Color = chosen;
        if (world.Gods.Find(g => g.Id - 1 == chosen) is { } wearer)
        {
            wearer.Color = player.Id - 1;
        }
    }

    /// <summary>
    /// The gods of a world as the content gives them: at its size, and with the player's Creature of
    /// the species chosen (a rival with that animal takes the player's, so every god keeps an animal
    /// of its own, ADR-061).
    /// </summary>
    /// <param name="content">Validated content.</param>
    /// <param name="options">How the world is made.</param>
    /// <returns>The gods, one slot each.</returns>
    public static PantheonSetup PantheonFor(GameContent content, NewWorldOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);
        var (_, sized) = AtSize(content, options.Size);
        var count = WorldChoices.GodsFor(content, options);
        var gods = count == sized.Gods.Count ? sized.Gods.ToList() : Take(content.Pantheon, count);
        gods = WorldChoices.Tempered(content.Sizes, gods, options.Temper);
        gods = WithSpecies(gods, options.Species);
        gods = WorldChoices.Named(gods, options.GodName, options.CreatureName);
        return gods.SequenceEqual(sized.Gods) ? sized : sized with { Gods = gods };
    }

    /// <summary>The first gods of <c>gods.json</c>'s gods and then its extra gods, the player's among them.</summary>
    private static List<GodSlot> Take(PantheonSetup pantheon, int count)
    {
        var all = pantheon.Gods.Concat(pantheon.ExtraGods).ToList();
        var gods = all.Take(count).ToList();
        var player = all.FindIndex(g => g.Controller == GodController.Human);
        if (player >= count && gods.Count > 0)
        {
            gods[^1] = all[player];
        }

        return gods;
    }

    /// <summary>The player's Creature of a species; a rival with that animal takes the player's (ADR-061).</summary>
    private static List<GodSlot> WithSpecies(List<GodSlot> gods, string? species)
    {
        if (species is null || gods.FindIndex(g => g.Controller == GodController.Human) is not (var player and >= 0))
        {
            return gods;
        }

        var given = gods[player].CreatureSpecies;
        for (var i = 0; i < gods.Count; i++)
        {
            if (i != player && string.Equals(gods[i].CreatureSpecies, species, StringComparison.Ordinal))
            {
                gods[i] = gods[i] with { CreatureSpecies = given };
            }
        }

        gods[player] = gods[player] with { CreatureSpecies = species };
        return gods;
    }

    /// <summary>A world size in words.</summary>
    /// <param name="preset">The size.</param>
    /// <returns>For example "4 km across, 20 villages and 6 gods".</returns>
    public static string Describe(WorldSizePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:0} km across, {1} villages and {2} gods",
            preset.HeightmapResolution * preset.VertexSpacing / 1000f,
            preset.Settlements,
            preset.Gods);
    }

    /// <summary>The size preset of a name, if the content offers it.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="size">The name, ignoring case.</param>
    /// <returns>The preset, or <c>null</c>.</returns>
    public static WorldSizePreset? Find(GameContent content, string? size)
    {
        ArgumentNullException.ThrowIfNull(content);
        return size is null ? null : content.Sizes.Sizes.FirstOrDefault(s => string.Equals(s.Name, size, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The generation settings and gods of a world size: the content's own for the first size or
    /// none; for a larger one, the island, regions, settlements (with the extra names), rivers
    /// (with the extra names) and gods (with the extra gods) it names. Few or many villages change
    /// the settlements founded (ADR-082), with more names when they need them.
    /// </summary>
    /// <param name="content">Validated content.</param>
    /// <param name="size">The size's name.</param>
    /// <param name="villages">Few, normal or many villages.</param>
    /// <returns>The settings and gods.</returns>
    public static (WorldGenSettings WorldGen, PantheonSetup Pantheon) AtSize(GameContent content, string? size, VillageCount villages = VillageCount.Normal)
    {
        ArgumentNullException.ThrowIfNull(content);
        var (worldGen, pantheon) = Find(content, size) is { } preset && !ReferenceEquals(preset, content.Sizes.Sizes[0])
            ? AtSize(content.Sizes, preset, content.WorldGen, content.Pantheon)
            : (content.WorldGen, content.Pantheon);
        return villages == VillageCount.Normal ? (worldGen, pantheon) : (WithVillages(content.Sizes, worldGen, WorldChoices.VillagesAt(content, size, villages)), pantheon);
    }

    /// <summary>Generation settings that found a number of villages, with the more names when theirs are too few.</summary>
    private static WorldGenSettings WithVillages(WorldSizes sizes, WorldGenSettings worldGen, int count)
    {
        var settlement = worldGen.StartingSettlement;
        var names = settlement.Names.Count >= count ? settlement.Names : [.. settlement.Names, .. sizes.MoreSettlementNames];
        return worldGen with { StartingSettlement = settlement with { Count = count, Names = names } };
    }

    /// <summary>The generation settings and gods of a size preset, from the default ones.</summary>
    private static (WorldGenSettings WorldGen, PantheonSetup Pantheon) AtSize(WorldSizes sizes, WorldSizePreset preset, WorldGenSettings defaults, PantheonSetup gods)
    {
        var settlement = defaults.StartingSettlement;
        var worldGen = defaults with
        {
            HeightmapResolution = preset.HeightmapResolution,
            VertexSpacing = preset.VertexSpacing,
            RegionsPerSide = preset.RegionsPerSide,
            StartingSettlement = settlement with { Count = preset.Settlements, Names = [.. settlement.Names, .. sizes.ExtraSettlementNames] },
            Hydrology = defaults.Hydrology is { } hydrology
                ? hydrology with { MaxRivers = preset.Rivers, MaxMainRivers = preset.MainRivers, CellSize = preset.HydrologyCellSize, RiverNames = [.. hydrology.RiverNames, .. sizes.ExtraRiverNames] }
                : null,
        };
        var pantheon = gods with { Gods = [.. gods.Gods, .. gods.ExtraGods.Take(Math.Max(0, preset.Gods - gods.Gods.Count))] };
        return (worldGen, pantheon);
    }

    /// <summary>
    /// Checks that every size can be made: the first as the content describes it, valid generation
    /// settings, names enough (for many villages too), gods enough and room for them, distinct names;
    /// and that the New World page's other choices make sense (ADR-082).
    /// </summary>
    /// <param name="sizes">The sizes.</param>
    /// <param name="worldGen">The default generation settings.</param>
    /// <param name="pantheon">The gods.</param>
    /// <exception cref="ArgumentException">A size cannot be made.</exception>
    public static void Validate(WorldSizes sizes, WorldGenSettings worldGen, PantheonSetup pantheon)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        ArgumentNullException.ThrowIfNull(worldGen);
        ArgumentNullException.ThrowIfNull(pantheon);
        if (sizes.Sizes.Select(s => s.Name.ToUpperInvariant()).Distinct().Count() != sizes.Sizes.Count)
        {
            throw new ArgumentException("Every world size needs its own name.");
        }

        ValidateChoices(sizes);
        for (var i = 0; i < sizes.Sizes.Count; i++)
        {
            var preset = sizes.Sizes[i];
            if (preset.Gods < WorldChoices.MinGods || preset.Gods > pantheon.Gods.Count + pantheon.ExtraGods.Count)
            {
                throw new ArgumentException($"World size {preset.Name} needs between 2 and {pantheon.Gods.Count + pantheon.ExtraGods.Count} gods.");
            }

            if (preset.Gods > preset.Settlements / sizes.VillagesPerGod)
            {
                throw new ArgumentException($"World size {preset.Name} has room for {preset.Settlements / sizes.VillagesPerGod} gods in its {preset.Settlements} villages, not {preset.Gods}.");
            }

            if (i == 0 && (preset.HeightmapResolution != worldGen.HeightmapResolution || preset.VertexSpacing != worldGen.VertexSpacing
                || preset.RegionsPerSide != worldGen.RegionsPerSide || preset.Settlements != worldGen.StartingSettlement.Count || preset.Gods != pantheon.Gods.Count))
            {
                throw new ArgumentException($"The first world size, {preset.Name}, must describe the world worldgen.json and gods.json make.");
            }

            var (atSize, gods) = i == 0 ? (worldGen, pantheon) : AtSize(sizes, preset, worldGen, pantheon);
            WorldGenSettingsValidator.Validate(atSize);
            PantheonFounder.Validate(gods);
            foreach (var villages in new[] { VillageCount.Few, VillageCount.Many })
            {
                WorldGenSettingsValidator.Validate(WithVillages(sizes, atSize, WorldChoices.Scaled(sizes, preset.Settlements, villages)));
            }
        }
    }

    /// <summary>Checks the New World page's choices beside the sizes (ADR-082).</summary>
    private static void ValidateChoices(WorldSizes sizes)
    {
        if (sizes.VillagesPerGod < 1 || !(sizes.PantheonShare is > 0f and <= 1f) || !(sizes.FewVillages is > 0f and <= 1f) || !(sizes.ManyVillages >= 1f && float.IsFinite(sizes.ManyVillages)))
        {
            throw new ArgumentException("VillagesPerGod must be at least 1, PantheonShare and FewVillages above 0 and at most 1, and ManyVillages at least 1.");
        }

        if (sizes.HoldMinutes.Count == 0 || sizes.HoldMinutes.Any(m => m < 1) || sizes.HoldMinutes.Distinct().Count() != sizes.HoldMinutes.Count)
        {
            throw new ArgumentException("HoldMinutes needs one or more different times of at least a minute.");
        }

        foreach (var (name, list) in new[] { ("Peaceful", sizes.Tempers.Peaceful), ("Warlike", sizes.Tempers.Warlike) })
        {
            if (list.Count == 0 || list.Any(p => p == GodPersonality.None || !Enum.IsDefined(p)) || list.Distinct().Count() != list.Count)
            {
                throw new ArgumentException($"The {name} temper needs one or more different personalities, never None.");
            }
        }
    }
}
