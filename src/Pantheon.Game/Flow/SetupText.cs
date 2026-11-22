using System;
using System.Globalization;
using Pantheon.Domain.World;

namespace Pantheon.Game.Flow;

/// <summary>How a world was made, in words (ADR-082): its gods, villages, rivals, calamities and victory's hold. Pure.</summary>
public static class SetupText
{
    /// <summary>The world's choices in a sentence, for the Book of the World.</summary>
    /// <param name="world">World state.</param>
    /// <param name="holdSeconds">How long its victory must be held.</param>
    /// <returns>For example "Made with 6 gods and many villages; harsh calamities; strong, warlike rivals; a victory is held for twenty minutes."</returns>
    public static string Sentence(WorldState world, float holdSeconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        var setup = world.Setup;
        var villages = setup.Villages == VillageCount.Normal ? "its size's villages" : WorldChoices.Words(setup.Villages) + " villages";
        var rivals = setup.Temper == RivalTemper.Mixed ? WorldChoices.Words(setup.Rivals) + " rivals" : $"{WorldChoices.Words(setup.Rivals)}, {WorldChoices.Words(setup.Temper)} rivals";
        var hold = world.Victory.IsSandbox() ? "no victory" : "a victory is held for " + VictoryText.Span(holdSeconds);
        return string.Format(CultureInfo.InvariantCulture, "Made with {0} gods and {1}; {2} calamities; {3}; {4}.", world.Gods.Count, villages, WorldChoices.Words(setup.Calamities), rivals, hold);
    }

    /// <summary>The world's choices on one line, for the debug panel (F1).</summary>
    /// <param name="world">World state.</param>
    /// <param name="holdSeconds">How long its victory must be held.</param>
    /// <returns>For example "Setup: 6 gods, many villages, calamities harsh, rivals strong and warlike, victory held 1200 s, colours 1:2 2:1".</returns>
    public static string DebugLine(WorldState world, float holdSeconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        var setup = world.Setup;
        var colors = string.Join(' ', world.Gods.FindAll(g => g.Color is not null).ConvertAll(g => string.Format(CultureInfo.InvariantCulture, "{0}:{1}", g.Id, g.Color + 1)));
        return string.Format(
            CultureInfo.InvariantCulture,
            "Setup: {0} gods, {1} villages, calamities {2}, rivals {3} and {4}, victory held {5:0} s{6}",
            world.Gods.Count,
            WorldChoices.Words(setup.Villages),
            WorldChoices.Words(setup.Calamities),
            WorldChoices.Words(setup.Rivals),
            WorldChoices.Words(setup.Temper),
            holdSeconds,
            colors.Length > 0 ? ", colours " + colors : string.Empty);
    }
}
