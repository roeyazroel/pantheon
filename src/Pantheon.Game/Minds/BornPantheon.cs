using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;

namespace Pantheon.Game.Minds;

/// <summary>One god of a pantheon born for a new world (ADR-065).</summary>
/// <param name="Name">Its name.</param>
/// <param name="CreatureName">Its Creature's name.</param>
/// <param name="Personality">Its personality; <see cref="GodPersonality.None"/> for a player's god.</param>
/// <param name="Persona">Who it is; none for a player's god.</param>
public sealed record BornGod(string Name, string CreatureName, GodPersonality Personality, GodPersona? Persona);

/// <summary>
/// The gods of a new world as they were written (ADR-065), one for each of the world's slots in
/// order: the player's as the content names it, each rival with its name, Creature, personality and
/// persona.
/// </summary>
/// <param name="Gods">The gods, one per slot.</param>
/// <param name="Author">Who wrote them: the scribes, or the model's name.</param>
/// <param name="Problem">Why the scribes wrote some or all of them when a model was asked; <c>null</c> otherwise.</param>
public sealed record BornPantheon(IReadOnlyList<BornGod> Gods, string Author, string? Problem = null)
{
    /// <summary>Who writes gods when no model does.</summary>
    public const string Scribes = "the scribes";

    /// <summary>The slots with the born gods' names, Creatures and personalities; the setup itself when the counts differ.</summary>
    /// <param name="setup">The world's gods as the content gives them.</param>
    /// <returns>The setup the world is made from.</returns>
    public PantheonSetup Apply(PantheonSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (Gods.Count != setup.Gods.Count)
        {
            return setup;
        }

        var slots = setup.Gods.Select((slot, i) => slot.Controller == GodController.Human
            ? slot
            : slot with { Name = Gods[i].Name, CreatureName = Gods[i].CreatureName, Personality = Gods[i].Personality }).ToList();
        return setup with { Gods = slots };
    }
}
