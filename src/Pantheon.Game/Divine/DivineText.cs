using System;
using System.Globalization;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
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
            CastRejection.RankTooLow => Format("{0} needs the rank of {1}", name, Rank(check.Miracle?.MinimumRank ?? DivineRank.MinorSpirit)),
            CastRejection.OnCooldown => Format("{0} is ready in {1:0.0} s", name, check.CooldownTicksLeft / Math.Max(1.0, ticksPerSecond)),
            CastRejection.OutsideWorld => "There is nothing to cast on there",
            CastRejection.OutsideInfluence => Format(
                "Outside your influence: {0:0%} here, {1} needs {2:0%}",
                check.Influence,
                name,
                check.Miracle?.InfluenceRequirement ?? 0f),
            CastRejection.NotEnoughPower => Format("Not enough Divine Power: {0} costs {1:0} here, you have {2:0}", name, check.Cost, power),
        };
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
