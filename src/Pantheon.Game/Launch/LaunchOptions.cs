using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;

namespace Pantheon.Game.Launch;

/// <summary>
/// Command-line options, passed to Godot after <c>--</c>, for example
/// <c>godot --path game -- --seed=1234 --terrain=fallback</c>. Both <c>--name=value</c> and
/// <c>--name value</c> are accepted. Invalid input produces a warning rather than an error.
/// </summary>
public sealed partial record LaunchOptions
{
    /// <summary>Option names (without the leading <c>--</c>) that <see cref="Parse"/> understands.</summary>
    public static IReadOnlySet<string> KnownOptions { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "seed",
        "load",
        "terrain",
        "advance-ticks",
        "camera-distance",
        "quit-after-frames",
        "screenshot",
        "divine-power",
        "cast",
        "overlay",
        "focus",
        "debug-panel",
        "select",
        "judge",
        "diplomacy-panel",
        "weather",
        "disaster",
        "refugees",
        "migrate",
        "meet",
        "host",
        "join",
        "name",
        "god",
        "upnp",
        "menu",
        "guide",
        "history-panel",
        "speed",
        "pause-menu",
        "species",
        "rank",
        "domains",
        "domains-panel",
        "settings-page",
    }.Union(ExpansionOptions()).Union(MindOptions()).Union(ControlOptions()).Union(MusicOptions()).Union(AgentOptions()).ToHashSet(StringComparer.Ordinal);

    /// <summary>Seed used when no <c>--seed</c> or <c>--load</c> option is given.</summary>
    public static WorldSeed DefaultSeed { get; } = new(1234);

    /// <summary>Seed of the world to generate, or <c>null</c> to use <see cref="DefaultSeed"/>.</summary>
    public WorldSeed? Seed { get; init; }

    /// <summary>Save file to load at startup instead of generating a world.</summary>
    public string? LoadPath { get; init; }

    /// <summary>Terrain renderer preference.</summary>
    public TerrainBackendPreference Terrain { get; init; } = TerrainBackendPreference.Auto;

    /// <summary>Simulation ticks to run before the first frame, for example to capture a grown settlement.</summary>
    public int? AdvanceTicks { get; init; }

    /// <summary>Starting camera distance in metres; the camera clamps it to its zoom range.</summary>
    public float? CameraDistance { get; init; }

    /// <summary>Quit automatically after this many frames (smoke tests and captures).</summary>
    public int? QuitAfterFrames { get; init; }

    /// <summary>Save a screenshot to this path just before the automatic quit.</summary>
    public string? ScreenshotPath { get; init; }

    /// <summary>Divine Power the player's god starts with, replacing the generated value (debug).</summary>
    public float? DivinePower { get; init; }

    /// <summary>Miracles the player's god casts, in order, on the focused settlement once the world is ready (debug).</summary>
    public IReadOnlyList<string> CastAtStart { get; init; } = [];

    /// <summary>Map overlay shown at the start.</summary>
    public OverlayKind? Overlay { get; init; }

    /// <summary>Settlement (1 for the first) the camera starts on and <see cref="CastAtStart"/> targets.</summary>
    public int? FocusSettlement { get; init; }

    /// <summary>Whether the camera starts on the player's Creature (<c>--focus=creature</c>).</summary>
    public bool FocusCreature { get; init; }

    /// <summary>Miracle selected at the start, so a capture shows its targeting ring at the screen centre.</summary>
    public string? SelectMiracle { get; init; }

    /// <summary>Reward or punishment given to the player's Creature on the first frame, so a capture shows the hand's gesture.</summary>
    public CreatureCommandKind? Judge { get; init; }

    /// <summary>Whether the diplomacy panel starts visible.</summary>
    public bool DiplomacyPanel { get; init; }

    /// <summary>Whether the F1 debug panel starts visible.</summary>
    public bool? DebugPanel { get; init; }

    /// <summary>Weather set up at the start: a spell, or a storm over the focused settlement.</summary>
    public WeatherAtStart? Weather { get; init; }

    /// <summary>A disaster started at the focused settlement at the start.</summary>
    public DisasterAtStart? Disaster { get; init; }

    /// <summary>The player's refugee policy from the start (ADR-016).</summary>
    public RefugeePolicy? Refugees { get; init; }

    /// <summary>People sent out of the focused settlement at the start (ADR-016).</summary>
    public int? Migrate { get; init; }

    /// <summary>
    /// The Creature brought beside the player's at the start, to meet it (ADR-017): an empty name for
    /// the nearest, <c>null</c> for none.
    /// </summary>
    public string? Meet { get; init; }

    /// <summary>The UDP port to host the world on (ADR-018), or <c>null</c> to play alone.</summary>
    public int? HostPort { get; init; }

    /// <summary>The host to join: its address and port (ADR-018), or <c>null</c> to play on this computer.</summary>
    public (string Address, int Port)? Join { get; init; }

    /// <summary>The player's name in a shared session.</summary>
    public string? PlayerName { get; init; }

    /// <summary>The god a joining player asks for, by name.</summary>
    public string? WantedGod { get; init; }

    /// <summary>Whether a host asks the router for its port with UPnP (spec §68).</summary>
    public bool Upnp { get; init; }

    /// <summary>Whether the start screen is shown (ADR-020); by default it is when the game starts without options.</summary>
    public bool? Menu { get; init; }

    /// <summary>Whether the first-minutes guide is shown from the start (ADR-020).</summary>
    public bool? Guide { get; init; }

    /// <summary>The simulation speed from the start: 1, 4 or 16 (the speed keys' presets), for measuring and captures.</summary>
    public double? Speed { get; init; }

    /// <summary>Whether the history panel (H) is open from the start, for captures.</summary>
    public bool HistoryPanel { get; init; }

    /// <summary>Whether the pause menu (Esc) is open from the start, for captures.</summary>
    public bool PauseMenu { get; init; }

    /// <summary>The species of the player's Creature in a new world (ADR-021), or <c>null</c> for <c>gods.json</c>'s.</summary>
    public string? Species { get; init; }

    /// <summary>The rank the player's god starts at, a debug tool to try domains and great miracles (ADR-021).</summary>
    public Domain.Gods.DivineRank? Rank { get; init; }

    /// <summary>The domains the player's god takes up at the start, in order, as its rank allows (ADR-021).</summary>
    public IReadOnlyList<Domain.Gods.GodDomain> Domains { get; init; } = [];

    /// <summary>The holy site the camera looks at from the start (1 is the first), for captures (ADR-021).</summary>
    public int? FocusSite { get; init; }

    /// <summary>Whether the Domains panel (G) is open from the start, for captures.</summary>
    public bool DomainsPanel { get; init; }

    /// <summary>Whether the settings page is open from the start, for captures.</summary>
    public bool SettingsPage { get; init; }


    /// <summary>Problems found while parsing.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Parses user command-line arguments.</summary>
    /// <param name="args">Arguments after Godot's <c>--</c> separator.</param>
    /// <returns>The parsed options.</returns>
    public static LaunchOptions Parse(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var items = new List<string>(args);
        var warnings = new List<string>();
        var options = new LaunchOptions();

        for (var i = 0; i < items.Count; i++)
        {
            var argument = items[i];
            if (!argument.StartsWith("--", StringComparison.Ordinal) || argument.Length == 2)
            {
                warnings.Add($"Ignoring unexpected argument '{argument}'.");
                continue;
            }

            var (name, value) = SplitOption(argument[2..]);
            if (value is null && i + 1 < items.Count && !items[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = items[++i];
            }

            options = Apply(options, name.ToLowerInvariant(), value, warnings);
        }

        return options with { Warnings = warnings };
    }

    /// <summary>Splits <c>name=value</c>; the value is <c>null</c> when there is no <c>=</c>.</summary>
    private static (string Name, string? Value) SplitOption(string body)
    {
        var separator = body.IndexOf('=', StringComparison.Ordinal);
        return separator < 0 ? (body, null) : (body[..separator], body[(separator + 1)..]);
    }

    /// <summary>Applies one option, recording a warning when it is unknown or its value is missing or invalid.</summary>
    private static LaunchOptions Apply(LaunchOptions options, string name, string? value, List<string> warnings)
    {
        if (!KnownOptions.Contains(name))
        {
            return Warn(options, warnings, $"Ignoring unknown option '--{name}'.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return Warn(options, warnings, $"Option '--{name}' needs a value.");
        }

        if ((ApplyExpansion(options, name, value, warnings) ?? ApplyMinds(options, name, value, warnings) ?? ApplyControls(options, name, value, warnings) ?? ApplyMusic(options, name, value, warnings) ?? ApplyAgents(options, name, value, warnings)) is { } expanded)
        {
            return expanded;
        }

        switch (name)
        {
            case "seed":
                return options with { Seed = WorldSeed.Parse(value) };
            case "load":
                return options with { LoadPath = value };
            case "terrain":
                return TryParseTerrain(value, out var terrain)
                    ? options with { Terrain = terrain }
                    : Warn(options, warnings, $"Unknown terrain backend '{value}'; expected auto, terrain3d or fallback.");
            case "advance-ticks":
                return TryParsePositiveInt(value, out var ticks)
                    ? options with { AdvanceTicks = ticks }
                    : Warn(options, warnings, $"'--advance-ticks' needs a positive integer, got '{value}'.");
            case "camera-distance":
                return TryParsePositiveFloat(value, out var distance)
                    ? options with { CameraDistance = distance }
                    : Warn(options, warnings, $"'--camera-distance' needs a positive number of metres, got '{value}'.");
            case "quit-after-frames":
                return TryParsePositiveInt(value, out var frames)
                    ? options with { QuitAfterFrames = frames }
                    : Warn(options, warnings, $"'--quit-after-frames' needs a positive integer, got '{value}'.");
            case "screenshot":
                return options with { ScreenshotPath = value };
            case "divine-power":
                return TryParsePositiveFloat(value, out var power)
                    ? options with { DivinePower = power }
                    : Warn(options, warnings, $"'--divine-power' needs a positive number, got '{value}'.");
            case "cast":
                return options with { CastAtStart = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) };
            case "overlay":
                return Enum.TryParse<OverlayKind>(value, ignoreCase: true, out var overlay) && Enum.IsDefined(overlay)
                    ? options with { Overlay = overlay }
                    : Warn(options, warnings, $"Unknown overlay '{value}'; expected influence, religion, faith, food, weather, danger, migration, culture, nature, population, risk, politics, navigation or authority.");
            case "focus":
                if (string.Equals(value.Trim(), "creature", StringComparison.OrdinalIgnoreCase))
                {
                    return options with { FocusCreature = true };
                }

                if (value.Trim().StartsWith("site", StringComparison.OrdinalIgnoreCase) && TryParsePositiveInt(value.Trim()[4..], out var site))
                {
                    return options with { FocusSite = site };
                }

                return TryParsePositiveInt(value, out var focus)
                    ? options with { FocusSettlement = focus }
                    : Warn(options, warnings, $"'--focus' needs a settlement number from 1, 'creature' or a holy site such as site2, got '{value}'.");
            case "judge":
                return value.Trim().ToLowerInvariant() switch
                {
                    "reward" => options with { Judge = CreatureCommandKind.Reward },
                    "punish" => options with { Judge = CreatureCommandKind.Punish },
                    _ => Warn(options, warnings, $"'--judge' needs reward or punish, got '{value}'."),
                };
            case "select":
                return options with { SelectMiracle = value.Trim() };
            case "diplomacy-panel":
                return value.Trim().ToLowerInvariant() switch
                {
                    "on" => options with { DiplomacyPanel = true },
                    "off" => options with { DiplomacyPanel = false },
                    _ => Warn(options, warnings, $"'--diplomacy-panel' needs on or off, got '{value}'."),
                };
            case "weather":
                return Enum.TryParse<WeatherAtStart>(value.Trim(), ignoreCase: true, out var weather) && Enum.IsDefined(weather)
                    ? options with { Weather = weather }
                    : Warn(options, warnings, $"'--weather' needs normal, dry, wet, storm, heatwave, coldwave or hurricane, got '{value}'.");
            case "disaster":
                return Enum.TryParse<DisasterAtStart>(value.Trim(), ignoreCase: true, out var disaster) && Enum.IsDefined(disaster)
                    ? options with { Disaster = disaster }
                    : Warn(options, warnings, $"'--disaster' needs quake, fire, flood, tornado, landslide, meteor, blight, locusts, tsunami or eruption, got '{value}'.");
            case "refugees":
                return value.Trim().ToLowerInvariant() switch
                {
                    "welcome" => options with { Refugees = RefugeePolicy.Welcome },
                    "convert" => options with { Refugees = RefugeePolicy.DemandConversion },
                    "turnaway" => options with { Refugees = RefugeePolicy.TurnAway },
                    _ => Warn(options, warnings, $"'--refugees' needs welcome, convert or turnaway, got '{value}'."),
                };
            case "migrate":
                return TryParsePositiveInt(value.Trim(), out var migrants)
                    ? options with { Migrate = migrants }
                    : Warn(options, warnings, $"'--migrate' needs a positive whole number of people, got '{value}'.");
            case "host":
                return value.Trim().ToLowerInvariant() switch
                {
                    "on" => options with { HostPort = Networking.NetworkProtocol.DefaultPort },
                    "off" => options with { HostPort = null },
                    var port when TryParsePort(port, out var number) => options with { HostPort = number },
                    _ => Warn(options, warnings, $"'--host' needs on, off or a port from 1 to 65535, got '{value}'."),
                };
            case "join":
                return TryParseAddress(value.Trim(), out var address)
                    ? options with { Join = address }
                    : Warn(options, warnings, $"'--join' needs an address, optionally with :port, got '{value}'.");
            case "name":
                return value.Trim().Length is > 0 and <= 24
                    ? options with { PlayerName = value.Trim() }
                    : Warn(options, warnings, $"'--name' needs 1 to 24 characters, got '{value}'.");
            case "god":
                return value.Trim().Length > 0 ? options with { WantedGod = value.Trim() } : Warn(options, warnings, "'--god' needs a god's name.");
            case "speed":
                return value.Trim() switch
                {
                    "1" => options with { Speed = 1.0 },
                    "4" => options with { Speed = 4.0 },
                    "16" => options with { Speed = 16.0 },
                    _ => Warn(options, warnings, $"'--speed' needs 1, 4 or 16, got '{value}'."),
                };
            case "history-panel":
                return OnOff(value) is { } history ? options with { HistoryPanel = history } : Warn(options, warnings, $"'--history-panel' needs on or off, got '{value}'.");
            case "pause-menu":
                return OnOff(value) is { } pause ? options with { PauseMenu = pause } : Warn(options, warnings, $"'--pause-menu' needs on or off, got '{value}'.");
            case "menu":
                return OnOff(value) is { } menu ? options with { Menu = menu } : Warn(options, warnings, $"'--menu' needs on or off, got '{value}'.");
            case "species":
                return value.Trim().Length > 0 ? options with { Species = value.Trim() } : Warn(options, warnings, "'--species' needs a species' name.");
            case "rank":
                return Enum.TryParse<Domain.Gods.DivineRank>(value.Trim(), ignoreCase: true, out var rank) && Enum.IsDefined(rank) && !int.TryParse(value.Trim(), out _)
                    ? options with { Rank = rank }
                    : Warn(options, warnings, $"'--rank' needs a rank such as LesserGod or God, got '{value}'.");
            case "domains":
                return ParseDomains(value) is { } domains ? options with { Domains = domains } : Warn(options, warnings, $"'--domains' needs domains separated by commas, such as Storm,War, got '{value}'.");
            case "domains-panel":
                return OnOff(value) is { } panel ? options with { DomainsPanel = panel } : Warn(options, warnings, $"'--domains-panel' needs on or off, got '{value}'.");
            case "settings-page":
                return OnOff(value) is { } settings ? options with { SettingsPage = settings } : Warn(options, warnings, $"'--settings-page' needs on or off, got '{value}'.");
            case "guide":
                return OnOff(value) is { } guide ? options with { Guide = guide } : Warn(options, warnings, $"'--guide' needs on or off, got '{value}'.");
            case "upnp":
                return value.Trim().ToLowerInvariant() switch
                {
                    "on" => options with { Upnp = true },
                    "off" => options with { Upnp = false },
                    _ => Warn(options, warnings, $"'--upnp' needs on or off, got '{value}'."),
                };
            case "meet":
                return value.Trim() switch
                {
                    "" => Warn(options, warnings, "'--meet' needs on, off or a Creature's name."),
                    var on when on.Equals("on", StringComparison.OrdinalIgnoreCase) => options with { Meet = string.Empty },
                    var off when off.Equals("off", StringComparison.OrdinalIgnoreCase) => options with { Meet = null },
                    var creature => options with { Meet = creature },
                };
            case "debug-panel":
                return value.Trim().ToLowerInvariant() switch
                {
                    "on" => options with { DebugPanel = true },
                    "off" => options with { DebugPanel = false },
                    _ => Warn(options, warnings, $"'--debug-panel' needs on or off, got '{value}'."),
                };
            default:
                throw new UnreachableException($"Option '--{name}' is listed in {nameof(KnownOptions)} but not handled.");
        }
    }

    /// <summary>Records a warning and returns the options unchanged.</summary>
    private static LaunchOptions Warn(LaunchOptions options, List<string> warnings, string warning)
    {
        warnings.Add(warning);
        return options;
    }

    /// <summary>Domains separated by commas, each known; <c>null</c> when one is not.</summary>
    private static List<Domain.Gods.GodDomain>? ParseDomains(string value)
    {
        var domains = new List<Domain.Gods.GodDomain>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<Domain.Gods.GodDomain>(part, ignoreCase: true, out var domain) || !Enum.IsDefined(domain) || int.TryParse(part, out _))
            {
                return null;
            }

            domains.Add(domain);
        }

        return domains.Count > 0 ? domains : null;
    }

    /// <summary>"on" or "off", in any case; <c>null</c> otherwise.</summary>
    private static bool? OnOff(string value) => value.Trim().ToLowerInvariant() switch
    {
        "on" => true,
        "off" => false,
        _ => null,
    };

    /// <summary>Parses a UDP port from 1 to 65535.</summary>
    private static bool TryParsePort(string value, out int port) => TryParsePositiveInt(value, out port) && port <= 65535;

    /// <summary>Parses "address" or "address:port"; the port defaults to <see cref="Networking.NetworkProtocol.DefaultPort"/>.</summary>
    private static bool TryParseAddress(string value, out (string Address, int Port) address)
    {
        address = default;
        var colon = value.LastIndexOf(':');
        var (host, port) = colon > 0 && value.IndexOf(':') == colon ? (value[..colon], value[(colon + 1)..]) : (value, null);
        if (host.Length == 0 || host.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        var number = Networking.NetworkProtocol.DefaultPort;
        if (port is not null && !TryParsePort(port, out number))
        {
            return false;
        }

        address = (host, number);
        return true;
    }

    /// <summary>Parses a positive whole number written with digits only.</summary>
    private static bool TryParsePositiveInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result > 0;

    /// <summary>Parses a positive, finite number in the invariant culture (a dot as the decimal separator).</summary>
    private static bool TryParsePositiveFloat(string value, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result > 0f && float.IsFinite(result);

    /// <summary>Parses a terrain backend name, case-insensitively.</summary>
    private static bool TryParseTerrain(string value, out TerrainBackendPreference terrain)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "auto":
                terrain = TerrainBackendPreference.Auto;
                return true;
            case "terrain3d":
                terrain = TerrainBackendPreference.Terrain3D;
                return true;
            case "fallback":
                terrain = TerrainBackendPreference.Fallback;
                return true;
            default:
                terrain = TerrainBackendPreference.Auto;
                return false;
        }
    }
}
