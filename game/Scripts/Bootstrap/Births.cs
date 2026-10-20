using System;
using System.Threading;
using Godot;
using Pantheon.Client.UI.Flow;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Minds;
using Pantheon.Game.Settings;
using Pantheon.Oracle;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// How a new world's rival gods are born in the client (ADR-065): at once by the scribes, or by the
/// language model the settings choose, asked off the main thread while the title screen shows the
/// gods waking (the player may let the scribes write them instead). The world is made from the gods
/// born, on the main thread, when they are ready.
/// </summary>
internal sealed class Births
{
    private readonly GameContent _content;
    private readonly MindContent? _minds;
    private readonly MenuScreen _menu;
    private readonly Func<GameSettings> _settings;
    private readonly MindProvider? _override;
    private CancellationTokenSource? _asking;
    private bool _skipped;

    /// <summary>Creates the births.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="minds">The minds' content; without it worlds are made with the content's gods.</param>
    /// <param name="menu">The menus, for the waiting page.</param>
    /// <param name="settings">The player's settings now.</param>
    /// <param name="overrideProvider">Who gives the gods minds in this run over the settings (<c>--minds</c>).</param>
    public Births(GameContent content, MindContent? minds, MenuScreen menu, Func<GameSettings> settings, MindProvider? overrideProvider)
    {
        (_content, _minds, _menu, _settings, _override) = (content, minds, menu, settings, overrideProvider);
    }

    /// <summary>Whether a model is being asked now.</summary>
    public bool Asking => _asking is not null;

    /// <summary>The minds' content, if it loaded.</summary>
    public MindContent? Minds => _minds;

    /// <summary>Who gives the gods minds now: the run's choice, else the settings'.</summary>
    public MindProvider Provider => _override ?? _settings().Minds;

    /// <summary>The model the gods' minds come from now, for talking to them (ADR-066); none for the scribes.</summary>
    /// <param name="problem">Why there is none when one was chosen.</param>
    /// <returns>The model, which the caller disposes, or <c>null</c>.</returns>
    public ILanguageModel? OpenModel(out string? problem)
    {
        if (_minds is null)
        {
            problem = null;
            return null;
        }

        return MindFactory.Open(_settings() with { Minds = Provider }, Startup.Keys, _minds, out problem);
    }

    /// <summary>The model the gods' and Creatures' agents ask (ADR-072), or <c>null</c> for the scribes.</summary>
    /// <param name="settings">The player's settings.</param>
    /// <param name="forced">Whether <c>--agents</c> turns the agents on or off for the run; the settings decide when <c>null</c>.</param>
    /// <param name="problem">Why there is no model when one was chosen.</param>
    /// <returns>The model.</returns>
    public ILanguageModel? OpenAgentModel(GameSettings settings, bool? forced, out string? problem)
    {
        if (_minds is null)
        {
            problem = null;
            return null;
        }

        return MindFactory.OpenAgents(settings with { Minds = Provider, Agents = forced ?? settings.Agents }, Startup.Keys, _minds, out problem);
    }

    /// <summary>The scribes' gods for a world, at once: for worlds made without asking (F2, the tutorial).</summary>
    /// <param name="seed">The world's seed.</param>
    /// <param name="options">How it is made.</param>
    /// <returns>The options with the gods born.</returns>
    public NewWorldOptions Scribed(WorldSeed seed, NewWorldOptions options) =>
        _minds is null ? options : options with { Born = PantheonBirth.Write(_content, _minds, seed, options) };

    /// <summary>Asks the chosen model for a greeting (<c>--mind-check</c>), and reports its answer or why there is none.</summary>
    /// <param name="report">Receives the answer in words.</param>
    public async void Check(Action<string> report)
    {
        if (_minds is null)
        {
            report("Minds: the minds' content did not load");
            return;
        }

        var model = MindFactory.Open(_settings() with { Minds = Provider }, Startup.Keys, _minds, out var problem);
        if (model is null)
        {
            report("Minds: " + (problem ?? "the scribes write the gods; there is no model to ask"));
            return;
        }

        try
        {
            var (_, text) = await MindCheck.Run(model, CancellationToken.None);
            report("Minds: " + text);
        }
        finally
        {
            (model as IDisposable)?.Dispose();
        }
    }

    /// <summary>Bears a world's gods, then makes the world: at once by the scribes, or once the model answers.</summary>
    /// <param name="seed">The world's seed.</param>
    /// <param name="options">How it is made.</param>
    /// <param name="make">Makes the world from the options with its gods, and says why the scribes wrote them when a model was asked (or <c>null</c>).</param>
    public async void Bear(WorldSeed seed, NewWorldOptions options, Action<NewWorldOptions, string?> make)
    {
        if (_minds is null || Asking)
        {
            if (!Asking)
            {
                make(options, null);
            }

            return;
        }

        var model = MindFactory.Open(_settings() with { Minds = Provider }, Startup.Keys, _minds, out var problem);
        if (model is null)
        {
            make(Scribed(seed, options), problem);
            return;
        }

        _asking = new CancellationTokenSource(TimeSpan.FromSeconds(_minds.TimeoutSeconds));
        _skipped = false;
        try
        {
            _menu.ShowBirth(model.Name, () =>
            {
                _skipped = true;
                _asking?.Cancel();
            });
            var born = await PantheonBirth.Ask(model, _content, _minds, seed, options, _asking.Token);
            var why = born.Author != BornPantheon.Scribes ? born.Problem
                : _skipped ? "The scribes wrote this world's gods, as you asked."
                : born.Problem ?? $"{model.Name} took too long, so the scribes wrote this world's gods.";
            make(options with { Born = born }, why);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        {
            GD.PushWarning($"The gods' birth failed: {exception.Message}");
            make(Scribed(seed, options), $"{model.Name} could not be asked, so the scribes wrote this world's gods.");
        }
        finally
        {
            _asking?.Dispose();
            _asking = null;
            (model as IDisposable)?.Dispose();
        }
    }
}
