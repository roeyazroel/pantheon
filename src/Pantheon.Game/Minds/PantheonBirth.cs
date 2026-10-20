using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;

namespace Pantheon.Game.Minds;

/// <summary>
/// How a new world's rival gods are born (ADR-065): written by the scribes from the seed, or asked of
/// a language model, with the scribes writing whatever the model does not (it fails, refuses, runs
/// out of time or writes a god badly). Either way the world is made from the gods born, so the
/// pantheon is part of the world, kept in its saves.
/// </summary>
public static class PantheonBirth
{
    /// <summary>The scribes' gods for a world.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="minds">The minds' content.</param>
    /// <param name="seed">The world's seed.</param>
    /// <param name="options">How the world is made.</param>
    /// <returns>The gods, one per slot.</returns>
    public static BornPantheon Write(GameContent content, MindContent minds, WorldSeed seed, NewWorldOptions options)
    {
        ArgumentNullException.ThrowIfNull(minds);
        return PersonaScribe.Write(minds.Lore, WorldMaker.PantheonFor(content, options), seed);
    }

    /// <summary>Asks a model for a world's gods, the scribes writing what it does not.</summary>
    /// <param name="model">The model.</param>
    /// <param name="content">Validated content.</param>
    /// <param name="minds">The minds' content.</param>
    /// <param name="seed">The world's seed.</param>
    /// <param name="options">How the world is made.</param>
    /// <param name="cancel">Cancels the question; the scribes then write the gods, with no problem told.</param>
    /// <returns>The gods, one per slot, and why the scribes wrote any.</returns>
    public static async Task<BornPantheon> Ask(ILanguageModel model, GameContent content, MindContent minds, WorldSeed seed, NewWorldOptions options, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(minds);
        ArgumentNullException.ThrowIfNull(options);
        var setup = WorldMaker.PantheonFor(content, options);
        var scribed = PersonaScribe.Write(minds.Lore, setup, seed);
        ModelAnswer answer;
        try
        {
            answer = await model.Ask(PantheonPrompt.Question(setup, Describe(content, setup, options), minds), cancel).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled, by the player or the clock: the caller knows which.
            return scribed;
        }

        return answer.Outcome == ModelOutcome.Answered
            ? PantheonPrompt.Read(answer.Json, scribed, model.Name)
            : scribed with { Problem = $"{model.Name} wrote no gods ({answer.Problem}), so the scribes wrote them." };
    }

    /// <summary>The world in a few words, for the model.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="setup">The world's gods.</param>
    /// <param name="options">How the world is made.</param>
    /// <returns>For example "an island 2 km across, of 12 villages and 4 gods, with four seasons".</returns>
    public static string Describe(GameContent content, PantheonSetup setup, NewWorldOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(options);
        var (worldGen, _) = WorldMaker.AtSize(content, options.Size);
        var across = worldGen.HeightmapResolution * worldGen.VertexSpacing / 1000f;
        return string.Format(
            CultureInfo.InvariantCulture,
            "an island {0:0.#} km across, of {1} villages and {2} gods, {3}",
            across,
            worldGen.StartingSettlement.Count,
            setup.Gods.Count,
            options.Seasons ? "with four seasons and hard winters" : "with an even climate the year round");
    }
}
