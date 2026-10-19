using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Miracles;

namespace Pantheon.Game.Divine;

/// <summary>Player-facing wording for ranks and cast verdicts. Pure and culture-invariant.</summary>
public static class DivineText
{
    /// <summary>A rank as the player reads it.</summary>
    /// <param name="rank">Rank.</param>
    /// <returns>For example "Lesser God".</returns>
    public static string Rank(DivineRank rank) => rank switch
    {
        DivineRank.MinorSpirit => "Minor Spirit",
        DivineRank.LesserGod => "Lesser God",
        DivineRank.God => "God",
        DivineRank.GreaterGod => "Greater God",
        DivineRank.HighGod => "High God",
        DivineRank.Ascendant => "Ascendant",
    };

    /// <summary>An AI personality as the player reads it.</summary>
    /// <param name="personality">Personality.</param>
    /// <returns>For example "Nature Keeper"; empty for a player's god.</returns>
    public static string Personality(GodPersonality personality) => personality switch
    {
        GodPersonality.None => string.Empty,
        GodPersonality.Guardian => "Guardian",
        GodPersonality.Tyrant => "Tyrant",
        GodPersonality.Opportunist => "Opportunist",
        GodPersonality.Zealot => "Zealot",
        GodPersonality.NatureKeeper => "Nature Keeper",
        GodPersonality.Trickster => "Trickster",
        GodPersonality.Isolationist => "Isolationist",
        GodPersonality.Warlord => "Warlord",
    };

    /// <summary>Gods named in a list, the local player's first as "You" (ADR-015).</summary>
    /// <param name="world">World state, for the names.</param>
    /// <param name="godIds">The gods.</param>
    /// <param name="localGodId">The local player's god, if any.</param>
    /// <returns>For example "You and Seren" or "Vael, Kaio and Seren".</returns>
    public static string Names(WorldState world, IReadOnlyList<int> godIds, int? localGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(godIds);
        var names = new List<string>();
        if (localGodId is { } local && godIds.Contains(local))
        {
            names.Add("You");
        }

        foreach (var id in godIds)
        {
            if (id != localGodId)
            {
                names.Add(GodStanding.Find(world, id)?.Name ?? "?");
            }
        }

        return names.Count <= 1 ? string.Join(string.Empty, names) : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[^1];
    }

    /// <summary>A duration in minutes and seconds.</summary>
    /// <param name="seconds">Seconds.</param>
    /// <returns>For example "6:05".</returns>
    public static string Clock(double seconds)
    {
        var whole = (int)Math.Ceiling(Math.Max(0.0, seconds));
        return Format("{0}:{1:00}", whole / 60, whole % 60);
    }

    /// <summary>Why a miracle cannot be cast, or what casting it costs.</summary>
    /// <param name="check">The authority's verdict.</param>
    /// <param name="miracleId">The requested miracle id, for unknown miracles.</param>
    /// <param name="power">The god's Divine Power.</param>
    /// <param name="ticksPerSecond">Simulation ticks per second at ×1, for cooldowns.</param>
    /// <param name="reached">Names of the settlements the miracle would reach, if known.</param>
    /// <returns>One line.</returns>
    public static string DescribeCheck(CastCheck check, string miracleId, float power, double ticksPerSecond, string? reached = null)
    {
        var name = check.Miracle?.Name ?? miracleId;
        return check.Rejection switch
        {
            CastRejection.None => string.IsNullOrEmpty(reached)
                ? Format("{0}: costs {1:0} here, influence {2:0%}", name, check.Cost, check.Influence)
                : Format("{0} on {1}: costs {2:0}, influence {3:0%}", name, reached, check.Cost, check.Influence),
            CastRejection.UnknownGod => "You have no god to cast with",
            CastRejection.UnknownMiracle => Format("Unknown miracle '{0}'", miracleId),
            CastRejection.NotAuthorized => Format("You may not cast {0} for another god", name),
            CastRejection.DomainRequired => Format("{0} belongs to {1}: take up {1} first (G)", name, Domains.DomainText.InSentence(check.Miracle?.Domain ?? GodDomain.Nature)),
            CastRejection.NothingThere => Format("{0}: {1}", name, NothingThere(check.Miracle)),
            CastRejection.RankTooLow => Format("{0} needs the rank of {1}", name, Rank(check.Miracle?.MinimumRank ?? DivineRank.MinorSpirit)),
            CastRejection.OnCooldown => Format("{0} is ready in {1:0.0} s", name, check.CooldownTicksLeft / Math.Max(1.0, ticksPerSecond)),
            CastRejection.OutsideWorld => "There is nothing to cast on there",
            CastRejection.OutsideInfluence => Format(
                "Outside your influence: {0:0%} here, {1} needs {2:0%}",
                check.Influence,
                name,
                check.Miracle?.InfluenceRequirement ?? 0f),
            CastRejection.NotEnoughPower => Format("Not enough Divine Power: {0} costs {1:0} here, you have {2:0}", name, check.Cost, power),
            CastRejection.TreatyForbids => Format("{0}: a territorial agreement keeps you out of this land (break it in P)", name),
        };
    }

    /// <summary>What a targeted miracle did not find (ADR-021).</summary>
    private static string NothingThere(Pantheon.Domain.Miracles.MiracleDefinition? miracle)
    {
        foreach (var effect in miracle?.Effects ?? [])
        {
            switch (effect.Kind)
            {
                case Pantheon.Domain.Miracles.MiracleEffectKind.Redirect:
                    return Format("no rain or storm cloud within {0:0} m", effect.Amount);
                case Pantheon.Domain.Miracles.MiracleEffectKind.Wound:
                    return "no rival Creature there";
                case Pantheon.Domain.Miracles.MiracleEffectKind.Teach:
                    return "your Creature is not there";
                case Pantheon.Domain.Miracles.MiracleEffectKind.Awaken:
                    return "no sleeping volcano on this island";
                case Pantheon.Domain.Miracles.MiracleEffectKind.Still:
                    return "no disaster to still";
                case Pantheon.Domain.Miracles.MiracleEffectKind.Anoint:
                    return "no village devout enough, or you have all the prophets you may";
                case Pantheon.Domain.Miracles.MiracleEffectKind.Raise:
                    return "an island rises only from the open sea";
                case Pantheon.Domain.Miracles.MiracleEffectKind.Split:
                    return "no mountain there with low ground on both sides";
                case Pantheon.Domain.Miracles.MiracleEffectKind.Divert:
                    return "no river near enough that could run down to here";
            }
        }

        return "nothing to act on there";
    }

    /// <summary>The result of an accepted cast.</summary>
    /// <param name="miracle">The miracle.</param>
    /// <param name="cost">What it cost.</param>
    /// <param name="settlementNames">Names of the settlements it reached.</param>
    /// <param name="deaths">People it killed.</param>
    /// <returns>One line.</returns>
    public static string DescribeCast(MiracleDefinition miracle, float cost, string settlementNames, int deaths)
    {
        ArgumentNullException.ThrowIfNull(miracle);
        var where = string.IsNullOrEmpty(settlementNames) ? "the land" : settlementNames;
        var toll = deaths > 0 ? Format(", {0} died", deaths) : string.Empty;
        return Format("{0} on {1} for {2:0} Divine Power{3}", miracle.Name, where, cost, toll);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
