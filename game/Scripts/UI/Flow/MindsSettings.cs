using System;
using System.Linq;
using System.Threading;
using Godot;
using Pantheon.Client.Bootstrap;
using Pantheon.Game.Minds;
using Pantheon.Game.Settings;
using Pantheon.Oracle;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The settings' Minds tab (ADR-065, ADR-067): who gives the rival gods their minds (the scribes,
/// Claude, GPT, Gemini or a model on this computer), the player's own key and model for each online
/// provider, the local model's address and name, and a test that asks the chosen model for a greeting.
/// </summary>
internal static class MindsSettings
{
    private static readonly (MindProvider Provider, string Name, string Description)[] Providers =
    [
        (MindProvider.Scribes, "The scribes (offline)", "The scribes write each world's gods from its seed: no network, and the same seed always gives the same gods."),
        (MindProvider.Claude, "Claude, by Anthropic", "Claude writes each world's gods: their names, histories and tempers. It needs your own Anthropic key, and its questions are billed to your Anthropic account."),
        (MindProvider.OpenAI, "GPT, by OpenAI", "GPT writes each world's gods. It needs your own OpenAI key, and its questions are billed to your OpenAI account."),
        (MindProvider.Gemini, "Gemini, by Google", "Gemini writes each world's gods. It needs your own Gemini API key, and its questions are billed to your Google account (or count against its free quota)."),
        (MindProvider.Local, "A model on this computer", "A model served by Ollama on this computer writes each world's gods. Install Ollama (ollama.com) and pull a model, for example: ollama pull llama3.2. Nothing leaves this computer."),
    ];

    /// <summary>Fills the tab.</summary>
    /// <param name="menu">The menu, holding the settings and reporting changes.</param>
    /// <param name="tab">The tab's column.</param>
    public static void Fill(MenuScreen menu, VBoxContainer tab)
    {
        var choice = UiKit.Choice(Providers.Select(p => (p.Name, p.Description)).ToList(), out var about);
        choice.Selected = Array.FindIndex(Providers, p => p.Provider == menu.Settings.Minds);
        about.Text = Providers[Math.Max(0, choice.Selected)].Description;
        tab.AddChild(UiKit.Row("The gods' minds", choice));
        tab.AddChild(about);

        var online = new (MindProvider Provider, VBoxContainer Rows)[]
        {
            (MindProvider.Claude, Online(menu, MindProvider.Claude, "sk-ant-…", "console.anthropic.com", m => m.ClaudeModels, s => s.ClaudeModel, (s, id) => s with { ClaudeModel = id })),
            (MindProvider.OpenAI, Online(menu, MindProvider.OpenAI, "sk-…", "platform.openai.com", m => m.OpenAIModels, s => s.OpenAIModel, (s, id) => s with { OpenAIModel = id })),
            (MindProvider.Gemini, Online(menu, MindProvider.Gemini, "AIza…", "aistudio.google.com", m => m.GeminiModels, s => s.GeminiModel, (s, id) => s with { GeminiModel = id })),
        };
        var local = Local(menu);
        foreach (var (_, rows) in online)
        {
            tab.AddChild(rows);
        }

        tab.AddChild(local);
        var status = UiKit.Text(string.Empty, UiTheme.CaptionSize, UiTheme.Muted);
        var test = UiKit.Button("Test", () => Test(menu, status));
        tab.AddChild(test);
        tab.AddChild(status);
        tab.AddChild(UiKit.Text("Your choice gives minds to the gods of new worlds; a world already made keeps the gods it was born with.", UiTheme.CaptionSize, UiTheme.Muted));

        void Show(MindProvider provider)
        {
            foreach (var (shown, rows) in online)
            {
                rows.Visible = provider == shown;
            }

            local.Visible = provider == MindProvider.Local;
            test.Visible = provider != MindProvider.Scribes;
            status.Text = string.Empty;
        }

        choice.ItemSelected += index =>
        {
            menu.ChangeSettings(menu.Settings with { Minds = Providers[(int)index].Provider });
            Show(menu.Settings.Minds);
        };
        Show(menu.Settings.Minds);
    }

    /// <summary>An online provider's rows: the player's key and the model.</summary>
    private static VBoxContainer Online(
        MenuScreen menu,
        MindProvider provider,
        string hint,
        string site,
        Func<MindContent, System.Collections.Generic.IReadOnlyList<ModelChoice>> models,
        Func<GameSettings, string> chosen,
        Func<GameSettings, string, GameSettings> choose)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        var variable = MindFactory.KeyVariables(provider).FirstOrDefault(v => System.Environment.GetEnvironmentVariable(v) is { Length: > 0 });
        var key = UiKit.Field(Startup.Keys.For(provider) ?? string.Empty, variable is not null ? $"using {variable} from your environment" : hint);
        key.Secret = true;
        void Keep() => Startup.KeepKey(provider, key.Text);
        key.TextSubmitted += _ => Keep();
        key.FocusExited += Keep;
        column.AddChild(UiKit.Row(Capital(MindFactory.KeyName(provider)), key));
        if (menu.Minds is { } minds)
        {
            var offered = models(minds);
            var list = UiKit.Choice(offered.Select(m => (m.Name, m.About)).ToList(), out var about);
            list.Selected = Math.Max(0, offered.ToList().FindIndex(m => m.Id == chosen(menu.Settings)));
            about.Text = offered[list.Selected].About;
            list.ItemSelected += index => menu.ChangeSettings(choose(menu.Settings, offered[(int)index].Id));
            column.AddChild(UiKit.Row("Model", list));
            column.AddChild(about);
        }

        column.AddChild(UiKit.Text($"Your key is kept on this computer only, in your profile, apart from your other settings. It goes only to its provider, with the gods' questions. Create one at {site}.", UiTheme.CaptionSize, UiTheme.Muted));
        return column;
    }

    /// <summary>A label with its first letter in capitals.</summary>
    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>The local model's rows: its address and its name, with the models the server has.</summary>
    private static VBoxContainer Local(MenuScreen menu)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        var address = UiKit.Field(menu.Settings.LocalUrl, GameSettings.DefaultLocalUrl);
        void KeepAddress() => menu.ChangeSettings(menu.Settings with { LocalUrl = address.Text });
        address.TextSubmitted += _ => KeepAddress();
        address.FocusExited += KeepAddress;
        column.AddChild(UiKit.Row("Address", address));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var name = UiKit.Field(menu.Settings.LocalModel, GameSettings.DefaultLocalModel);
        name.CustomMinimumSize = new Vector2(220, 38);
        void KeepName() => menu.ChangeSettings(menu.Settings with { LocalModel = name.Text });
        name.TextSubmitted += _ => KeepName();
        name.FocusExited += KeepName;
        var installed = new OptionButton { CustomMinimumSize = new Vector2(0, 38), FocusMode = Control.FocusModeEnum.None, Visible = false };
        installed.ItemSelected += index =>
        {
            name.Text = installed.GetItemText((int)index);
            KeepName();
        };
        var find = UiKit.Button("Find", () => Find(menu, installed));
        find.TooltipText = "The models this computer's server has";
        row.AddChild(name);
        row.AddChild(find);
        column.AddChild(UiKit.Row("Model", row));
        column.AddChild(installed);
        return column;
    }

    /// <summary>Lists the models the local server has.</summary>
    private static async void Find(MenuScreen menu, OptionButton installed)
    {
        if (!Uri.TryCreate(menu.Settings.LocalUrl, UriKind.Absolute, out var address))
        {
            return;
        }

        using var model = new LocalLanguageModel(address, menu.Settings.LocalModel, TimeSpan.FromSeconds(10));
        var (models, problem) = await model.Installed(CancellationToken.None);
        if (!GodotObject.IsInstanceValid(installed))
        {
            return;
        }

        installed.Clear();
        foreach (var name in models)
        {
            installed.AddItem(name);
        }

        installed.Visible = models.Count > 0;
        installed.TooltipText = problem ?? string.Empty;
        if (problem is not null)
        {
            installed.AddItem(problem);
            installed.Visible = true;
            installed.Disabled = true;
        }
    }

    /// <summary>Asks the chosen model for a greeting and shows its answer.</summary>
    private static async void Test(MenuScreen menu, Label status)
    {
        if (menu.Minds is not { } minds)
        {
            return;
        }

        var model = MindFactory.Open(menu.Settings, Startup.Keys, minds, out var problem);
        if (model is null)
        {
            status.Text = problem ?? "The scribes need no test.";
            return;
        }

        status.Text = $"Asking {model.Name}…";
        status.AddThemeColorOverride("font_color", UiTheme.Muted);
        try
        {
            var (answered, text) = await MindCheck.Run(model, CancellationToken.None);
            if (GodotObject.IsInstanceValid(status))
            {
                status.Text = text;
                status.AddThemeColorOverride("font_color", answered ? UiTheme.Good : UiTheme.Bad);
            }
        }
        finally
        {
            (model as IDisposable)?.Dispose();
        }
    }
}
