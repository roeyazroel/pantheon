using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;

namespace Pantheon.Game;

/// <summary>
/// The limits of the New World page's choices (ADR-082): how many gods and villages a world may be
/// made with, the names a player may give, how long a victory may be held, and what each choice is
/// called. Pure, so the page, the launch options and the tests ask the same questions.
/// </summary>
public static class WorldChoices
{
    /// <summary>The longest name a player may give their god or Creature: as long as the gods' minds may give theirs (ADR-065).</summary>
    public const int NameLength = 18;

    /// <summary>The fewest gods a world is made with.</summary>
    public const int MinGods = 2;

    /// <summary>A name a player typed, trimmed and with no control characters; <c>null</c> when nothing is left or it is too long.</summary>
    /// <param name="text">The text typed.</param>
    /// <returns>The name, or <c>null</c> to keep the content's.</returns>
    public static string? CleanName(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var name = new string([.. text.Where(c => !char.IsControl(c))]).Trim();
        return name.Length is > 0 and <= NameLength ? name : null;
    }

    /// <summary>The size preset a name asks for: the named one, or the first.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="size">The size's name.</param>
    /// <returns>The preset, or <c>null</c> when the content offers no sizes.</returns>
    public static WorldSizePreset? SizeOf(GameContent content, string? size)
    {
        ArgumentNullException.ThrowIfNull(content);
        return WorldMaker.Find(content, size) ?? (content.Sizes.Sizes.Count > 0 ? content.Sizes.Sizes[0] : null);
    }

    /// <summary>How many villages an island of a size is founded with for a village count.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="size">The size's name.</param>
    /// <param name="villages">Few, normal or many.</param>
    /// <returns>The villages, at least 2.</returns>
    public static int VillagesAt(GameContent content, string? size, VillageCount villages)
    {
        ArgumentNullException.ThrowIfNull(content);
        var normal = SizeOf(content, size)?.Settlements ?? content.WorldGen.StartingSettlement.Count;
        return Scaled(content.Sizes, normal, villages);
    }

    /// <summary>The villages of a count from a size's own number.</summary>
    /// <param name="sizes">The sizes' content.</param>
    /// <param name="normal">The size's own number.</param>
    /// <param name="villages">Few, normal or many.</param>
    /// <returns>The villages, at least 2.</returns>
    public static int Scaled(WorldSizes sizes, int normal, VillageCount villages)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        var share = villages switch
        {
            VillageCount.Few => sizes.FewVillages,
            VillageCount.Normal => 1f,
            VillageCount.Many => sizes.ManyVillages,
        };
        return Math.Max(2, (int)MathF.Round(normal * share, MidpointRounding.AwayFromZero));
    }

    /// <summary>The most gods a world may hold: as many as the content names, and one for every <c>VillagesPerGod</c> villages.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="size">The size's name.</param>
    /// <param name="villages">Few, normal or many.</param>
    /// <returns>The most gods, at least <see cref="MinGods"/>.</returns>
    public static int MaxGods(GameContent content, string? size, VillageCount villages)
    {
        ArgumentNullException.ThrowIfNull(content);
        var named = content.Pantheon.Gods.Count + content.Pantheon.ExtraGods.Count;
        var room = VillagesAt(content, size, villages) / Math.Max(1, content.Sizes.VillagesPerGod);
        return Math.Max(MinGods, Math.Min(named, room));
    }

    /// <summary>How many gods a world is made with: the number asked, or the size's own, within the limits.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="options">How the world is made.</param>
    /// <returns>The gods.</returns>
    public static int GodsFor(GameContent content, NewWorldOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);
        var wanted = options.Gods ?? SizeOf(content, options.Size)?.Gods ?? content.Pantheon.Gods.Count;
        return Math.Clamp(wanted, MinGods, MaxGods(content, options.Size, options.Villages));
    }

    /// <summary>How many of a world's gods may win a pantheon victory together: <c>PantheonShare</c> of them, rounded down.</summary>
    /// <param name="sizes">The sizes' content.</param>
    /// <param name="gods">The world's gods.</param>
    /// <returns>The most gods in a winning pantheon; 1 means none wins together.</returns>
    public static int PantheonMaxGods(WorldSizes sizes, int gods)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        return Math.Max(1, (int)MathF.Floor(gods * sizes.PantheonShare));
    }

    /// <summary>The times a victory may be held for, in minutes, shortest first; the divine rules' own when the content lists none.</summary>
    /// <param name="content">Validated content.</param>
    /// <returns>The minutes.</returns>
    public static IReadOnlyList<int> HoldMinutes(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.Sizes.HoldMinutes.Count > 0 ? [.. content.Sizes.HoldMinutes.Order()] : [DefaultHoldMinutes(content)];
    }

    /// <summary>The divine rules' own hold, in whole minutes.</summary>
    /// <param name="content">Validated content.</param>
    /// <returns>The minutes.</returns>
    public static int DefaultHoldMinutes(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Math.Max(1, (int)MathF.Round(content.DivineRules.VictorySeconds / 60f));
    }

    /// <summary>A calamity level in words, for the page and the debug panel.</summary>
    /// <param name="level">The level.</param>
    /// <returns>For example "harsh".</returns>
    public static string Words(CalamityLevel level) => level switch
    {
        CalamityLevel.Gentle => "gentle",
        CalamityLevel.Normal => "normal",
        CalamityLevel.Harsh => "harsh",
    };

    /// <summary>A rival strength in words.</summary>
    /// <param name="strength">The strength.</param>
    /// <returns>For example "strong".</returns>
    public static string Words(RivalStrength strength) => strength switch
    {
        RivalStrength.Weak => "weak",
        RivalStrength.Even => "even",
        RivalStrength.Strong => "strong",
    };

    /// <summary>A rivals' temper in words.</summary>
    /// <param name="temper">The temper.</param>
    /// <returns>For example "warlike".</returns>
    public static string Words(RivalTemper temper) => temper switch
    {
        RivalTemper.Mixed => "mixed",
        RivalTemper.Peaceful => "peaceful",
        RivalTemper.Warlike => "warlike",
    };

    /// <summary>A village count in words.</summary>
    /// <param name="villages">The count.</param>
    /// <returns>For example "many".</returns>
    public static string Words(VillageCount villages) => villages switch
    {
        VillageCount.Few => "few",
        VillageCount.Normal => "normal",
        VillageCount.Many => "many",
    };

    /// <summary>
    /// The world's gods with the player's names: the god and Creature named as the player asked, and a
    /// rival that had either name taking the player's old one, so no two gods or Creatures share a name.
    /// </summary>
    /// <param name="gods">The slots.</param>
    /// <param name="godName">The god's name asked, or <c>null</c>.</param>
    /// <param name="creatureName">The Creature's name asked, or <c>null</c>.</param>
    /// <returns>The slots, renamed.</returns>
    public static List<GodSlot> Named(List<GodSlot> gods, string? godName, string? creatureName)
    {
        ArgumentNullException.ThrowIfNull(gods);
        var player = gods.FindIndex(g => g.Controller == GodController.Human);
        if (player < 0)
        {
            return gods;
        }

        if (CleanName(godName) is { } name)
        {
            var old = gods[player].Name;
            Swap(gods, player, name, g => g.Name, (g, n) => g with { Name = n }, old);
        }

        if (CleanName(creatureName) is { } creature)
        {
            var old = gods[player].CreatureName;
            Swap(gods, player, creature, g => g.CreatureName, (g, n) => g with { CreatureName = n }, old);
        }

        return gods;
    }

    /// <summary>The rivals' personalities within a temper: a rival whose personality the temper does not allow draws one it does, in turn.</summary>
    /// <param name="sizes">The sizes' content, for the tempers.</param>
    /// <param name="gods">The slots.</param>
    /// <param name="temper">The temper.</param>
    /// <returns>The slots.</returns>
    public static List<GodSlot> Tempered(WorldSizes sizes, List<GodSlot> gods, RivalTemper temper)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        ArgumentNullException.ThrowIfNull(gods);
        var allowed = sizes.PersonalitiesOf(temper);
        var rival = 0;
        for (var i = 0; i < gods.Count; i++)
        {
            if (gods[i].Controller == GodController.Human)
            {
                continue;
            }

            if (!allowed.Contains(gods[i].Personality))
            {
                gods[i] = gods[i] with { Personality = allowed[rival % allowed.Count] };
            }

            rival++;
        }

        return gods;
    }

    /// <summary>Gives the player's slot a name, and any other slot with that name the player's old one.</summary>
    private static void Swap(List<GodSlot> gods, int player, string name, Func<GodSlot, string> read, Func<GodSlot, string, GodSlot> write, string old)
    {
        for (var i = 0; i < gods.Count; i++)
        {
            if (i != player && string.Equals(read(gods[i]), name, StringComparison.OrdinalIgnoreCase))
            {
                gods[i] = write(gods[i], old);
            }
        }

        gods[player] = write(gods[player], name);
    }
}
