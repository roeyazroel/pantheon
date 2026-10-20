using System;
using Pantheon.Domain.Gods;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// A god's own profile (ADR-065): its personality's, leaning as its temper leans. Two Tyrants are
/// alike in what they weigh, but one may be quicker to war and the other slower to forgive.
/// </summary>
public static class GodTempers
{
    /// <summary>The profile leaning as the temper leans, each weight kept from 0 to 1.</summary>
    /// <param name="profile">The personality's profile.</param>
    /// <param name="temper">The god's temper; none leans nowhere.</param>
    /// <param name="reach">How far a leaning of 1 moves a weight (<c>god_ai.json</c>'s <c>TemperReach</c>).</param>
    /// <returns>The profile itself when nothing leans, else a new one.</returns>
    public static GodProfile Apply(GodProfile profile, GodTemper? temper, float reach)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (temper is null || temper.IsEven || reach <= 0f)
        {
            return profile;
        }

        return profile with
        {
            Aggression = Lean(profile.Aggression, temper.Aggression, reach),
            Cooperation = Lean(profile.Cooperation, temper.Cooperation, reach),
            Treachery = Lean(profile.Treachery, temper.Treachery, reach),
            Grudge = Lean(profile.Grudge, temper.Grudge, reach),
            Protect = Lean(profile.Protect, temper.Protect, reach),
            Convert = Lean(profile.Convert, temper.Convert, reach),
            Intimidate = Lean(profile.Intimidate, temper.Intimidate, reach),
        };
    }

    /// <summary>A temper with every leaning from -1 to 1; a leaning that is not a number leans nowhere.</summary>
    /// <param name="temper">The temper.</param>
    /// <returns>The temper in range.</returns>
    public static GodTemper Clamped(GodTemper temper)
    {
        ArgumentNullException.ThrowIfNull(temper);
        return new GodTemper
        {
            Aggression = Unit(temper.Aggression),
            Cooperation = Unit(temper.Cooperation),
            Treachery = Unit(temper.Treachery),
            Grudge = Unit(temper.Grudge),
            Protect = Unit(temper.Protect),
            Convert = Unit(temper.Convert),
            Intimidate = Unit(temper.Intimidate),
        };
    }

    /// <summary>A weight moved by a leaning, kept from 0 to 1.</summary>
    private static float Lean(float weight, float leaning, float reach) => Math.Clamp(weight + (Unit(leaning) * reach), 0f, 1f);

    /// <summary>A leaning from -1 to 1.</summary>
    private static float Unit(float leaning) => float.IsFinite(leaning) ? Math.Clamp(leaning, -1f, 1f) : 0f;
}
