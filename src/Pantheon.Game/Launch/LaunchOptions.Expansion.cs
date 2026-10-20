using System;
using System.Collections.Generic;
using Pantheon.Domain.Politics;

namespace Pantheon.Game.Launch;

/// <summary>The launch options of Milestone 15's later parts: society, culture, politics, conflict and world sizes (ADR-023 to ADR-027).</summary>
public sealed partial record LaunchOptions
{
    /// <summary>A priority the player's god asks of the focused settlement at the start (ADR-023).</summary>
    public Domain.Society.SettlementPriority? Priority { get; init; }

    /// <summary>Whether the player's Creature is set on the rival nearest it at the start (ADR-062), for captures; with <c>--meet</c> the rival is brought beside it.</summary>
    public bool Fight { get; init; }

    /// <summary>A page of the title screen open from the start (ADR-060): new, worlds, together or settings; for captures.</summary>
    public string? TitlePage { get; init; }

    /// <summary>Whether the Settlements panel is open at the start, on the focused settlement.</summary>
    public bool SettlementPanel { get; init; }

    /// <summary>A doctrine, or secularism, the focused settlement holds at the start (ADR-024).</summary>
    public CultureAtStart? Doctrine { get; init; }

    /// <summary>Whether the council panel (U) is open at the start (ADR-025).</summary>
    public bool CouncilPanel { get; init; }

    /// <summary>Whether the Divine Council meets at the start, for captures.</summary>
    public bool SummonCouncil { get; init; }

    /// <summary>Traits the player's Creature has strongly at the start, by §26's words (ADR-037); none when <c>null</c>.</summary>
    public IReadOnlyList<string>? Temperament { get; init; }

    /// <summary>Whether every god has met every other at the start (ADR-036), for captures.</summary>
    public bool GodsMet { get; init; }

    /// <summary>A treaty the player's god signs with its strongest rival at the start (ADR-025).</summary>
    public TreatyKind? Treaty { get; init; }

    /// <summary>A war the player's god declares on its strongest rival at the start, for this goal (ADR-026).</summary>
    public Domain.Conflict.WarGoal? WarGoal { get; init; }

    /// <summary>Whether the focused settlement goes to holy war on a neighbour at the start (ADR-026).</summary>
    public bool HolyWar { get; init; }

    /// <summary>Whether the player casts veiled miracles from the start (ADR-026).</summary>
    public bool Veil { get; init; }

    /// <summary>The size of a new world, by its name in <c>world_sizes.json</c> (ADR-027); the first when <c>null</c>.</summary>
    public string? WorldSize { get; init; }

    /// <summary>How a new world may be won (ADR-027); ascendancy only when <c>null</c>.</summary>
    public Domain.World.VictorySettings? Victory { get; init; }

    /// <summary>Whether a new world turns through four seasons (ADR-028); it does when <c>null</c>.</summary>
    public bool? Seasons { get; init; }

    /// <summary>The season that begins at the start (ADR-028), for captures.</summary>
    public Domain.Weather.Season? Season { get; init; }

    /// <summary>What the player's hand lifts at the start (ADR-030), for captures.</summary>
    public HandAtStart? Hand { get; init; }

    /// <summary>The page of the Book of the World open at the start (ADR-031), for captures.</summary>
    public Book.BookTab? Book { get; init; }

    /// <summary>Whether the camera starts on the strategic overview (ADR-031), for captures.</summary>
    public bool Overview { get; init; }

    /// <summary>Whether a prophet of the player's god rises at the focused village at the start (ADR-032), for captures.</summary>
    public bool Prophet { get; init; }

    /// <summary>Whether the player follows one of the focused village's people at the start, the camera on them (ADR-043).</summary>
    public bool Villager { get; init; }

    /// <summary>Whether a new world is the stress scenario of spec §104 (ADR-044): 50 villages of 100,000 people, weather and disasters.</summary>
    public bool Stress { get; init; }

    /// <summary>Whether the camera goes to the herd nearest the focused settlement at the start (ADR-054).</summary>
    public bool FocusWildlife { get; init; }

    /// <summary>A reshaping of the land made near the focused settlement at the start (ADR-045), for captures.</summary>
    public Domain.Land.ReshapeKind? Reshape { get; init; }

    /// <summary>The great event that begins at the start (ADR-034), for captures.</summary>
    public GreatEvents.GreatEventAtStart? GreatEvent { get; init; }

    /// <summary>Whether every road the villages would plan is built at the start (ADR-033), for captures.</summary>
    public bool Roads { get; init; }

    /// <summary>Whether every village's wood and stone start full (<c>true</c>) or empty (<c>false</c>) (ADR-033).</summary>
    public bool? Stores { get; init; }

    /// <summary>How to make the world these options ask for, with names the content knows.</summary>
    /// <param name="content">Validated content.</param>
    /// <returns>The species, size and victories; an unknown species or size is left to the content's own.</returns>
    public NewWorldOptions NewWorld(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new NewWorldOptions(
            Simulation.Creatures.CreatureModel.FindSpecies(content.Creatures, Species)?.Name,
            Stress ? Performance.StressScenario.SizeName : WorldMaker.Find(content, WorldSize)?.Name,
            Victory,
            Seasons ?? true);
    }

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] ExpansionOptions() => ["priority", "settlement-panel", "doctrine", "council-panel", "summon-council", "treaty", "war-goal", "holy-war", "veil", "world-size", "victory", "seasons", "season", "hand", "book", "overview", "prophet", "roads", "stores", "great-event", "gods-met", "temperament", "villager", "stress", "reshape", "wildlife", "title-page", "fight"];

    /// <summary>
    /// Victories from <c>all</c>, <c>sandbox</c> or a list of <c>ascendancy</c>, <c>conquest</c> and
    /// <c>cultural</c> separated by commas; <c>null</c> when the text is none of these.
    /// </summary>
    private static Domain.World.VictorySettings? ParseVictory(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && parts[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.World.VictorySettings.All;
        }

        if (parts.Length == 1 && parts[0].Equals("sandbox", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.World.VictorySettings.Sandbox;
        }

        var victory = Domain.World.VictorySettings.Sandbox;
        foreach (var part in parts)
        {
            if (part.Equals("ascendancy", StringComparison.OrdinalIgnoreCase))
            {
                victory = victory with { Ascendancy = true };
            }
            else if (part.Equals("conquest", StringComparison.OrdinalIgnoreCase))
            {
                victory = victory with { Conquest = true };
            }
            else if (part.Equals("cultural", StringComparison.OrdinalIgnoreCase))
            {
                victory = victory with { Cultural = true };
            }
            else
            {
                return null;
            }
        }

        return parts.Length > 0 ? victory : null;
    }

    /// <summary>Applies one of this part's options; <c>null</c> when the option is not one of them.</summary>
    private static LaunchOptions? ApplyExpansion(LaunchOptions options, string name, string value, List<string> warnings)
    {
        var trimmed = value.Trim();
        switch (name)
        {
            case "fight":
                return OnOff(value) is { } fight ? options with { Fight = fight } : Warn(options, warnings, $"'--fight' needs on or off, got '{value}'.");
            case "title-page":
                return value is "new" or "worlds" or "together" or "settings"
                    ? options with { TitlePage = value, Menu = true }
                    : Warn(options, warnings, $"Unknown title page '{value}'; expected new, worlds, together or settings.");
            case "priority":
                return Enum.TryParse<Domain.Society.SettlementPriority>(trimmed, ignoreCase: true, out var priority) && Enum.IsDefined(priority) && !int.TryParse(trimmed, out _)
                    ? options with { Priority = priority }
                    : Warn(options, warnings, $"'--priority' needs a priority such as Temple or Preparation, got '{value}'.");
            case "doctrine":
                return Enum.TryParse<CultureAtStart>(trimmed, ignoreCase: true, out var doctrine) && Enum.IsDefined(doctrine) && !int.TryParse(trimmed, out _)
                    ? options with { Doctrine = doctrine }
                    : Warn(options, warnings, $"'--doctrine' needs a doctrine such as Charity or Militarism, or Secular, got '{value}'.");
            case "settlement-panel":
                return OnOff(value) is { } open ? options with { SettlementPanel = open } : Warn(options, warnings, $"'--settlement-panel' needs on or off, got '{value}'.");
            case "council-panel":
                return OnOff(value) is { } council ? options with { CouncilPanel = council } : Warn(options, warnings, $"'--council-panel' needs on or off, got '{value}'.");
            case "temperament":
                var words = trimmed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                return words.Length > 0 && Array.TrueForAll(words, w => System.Linq.Enumerable.Contains(Creatures.TemperamentText.Words, w.ToLowerInvariant()))
                    ? options with { Temperament = words }
                    : Warn(options, warnings, $"'--temperament' needs words such as brave, protective or jealous, separated by commas, got '{value}'.");
            case "gods-met":
                return OnOff(value) is { } met ? options with { GodsMet = met } : Warn(options, warnings, $"'--gods-met' needs on or off, got '{value}'.");
            case "summon-council":
                return OnOff(value) is { } summon ? options with { SummonCouncil = summon } : Warn(options, warnings, $"'--summon-council' needs on or off, got '{value}'.");
            case "treaty":
                return Enum.TryParse<TreatyKind>(trimmed, ignoreCase: true, out var treaty) && Enum.IsDefined(treaty) && !int.TryParse(trimmed, out _)
                    ? options with { Treaty = treaty }
                    : Warn(options, warnings, $"'--treaty' needs a treaty such as NonAggression or MutualDefense, got '{value}'.");
            case "war-goal":
                return Enum.TryParse<Domain.Conflict.WarGoal>(trimmed, ignoreCase: true, out var goal) && Enum.IsDefined(goal) && !int.TryParse(trimmed, out _)
                    ? options with { WarGoal = goal }
                    : Warn(options, warnings, $"'--war-goal' needs Conquest, HolySite, Tribute or Humiliation, got '{value}'.");
            case "holy-war":
                return OnOff(value) is { } holy ? options with { HolyWar = holy } : Warn(options, warnings, $"'--holy-war' needs on or off, got '{value}'.");
            case "veil":
                return OnOff(value) is { } veil ? options with { Veil = veil } : Warn(options, warnings, $"'--veil' needs on or off, got '{value}'.");
            case "world-size":
                return trimmed.Length > 0 ? options with { WorldSize = trimmed } : Warn(options, warnings, "'--world-size' needs a size such as Small, Large or Huge.");
            case "seasons":
                return OnOff(value) is { } seasons ? options with { Seasons = seasons } : Warn(options, warnings, $"'--seasons' needs on or off, got '{value}'.");
            case "season":
                return Enum.TryParse<Domain.Weather.Season>(trimmed, ignoreCase: true, out var season) && Enum.IsDefined(season) && !int.TryParse(trimmed, out _)
                    ? options with { Season = season }
                    : Warn(options, warnings, $"'--season' needs spring, summer, autumn or winter, got '{value}'.");
            case "book":
                return Enum.TryParse<Book.BookTab>(trimmed, ignoreCase: true, out var page) && Enum.IsDefined(page) && !int.TryParse(trimmed, out _)
                    ? options with { Book = page }
                    : Warn(options, warnings, $"'--book' needs world, religion, miracles, creature or villager, got '{value}'.");
            case "overview":
                return OnOff(value) is { } overview ? options with { Overview = overview } : Warn(options, warnings, $"'--overview' needs on or off, got '{value}'.");
            case "great-event":
                return Enum.TryParse<GreatEvents.GreatEventAtStart>(trimmed, ignoreCase: true, out var great) && Enum.IsDefined(great) && !int.TryParse(trimmed, out _)
                    ? options with { GreatEvent = great }
                    : Warn(options, warnings, $"'--great-event' needs plague, eclipse, reformation, beast or anomaly, got '{value}'.");
            case "roads":
                return OnOff(value) is { } roads ? options with { Roads = roads } : Warn(options, warnings, $"'--roads' needs on or off, got '{value}'.");
            case "stores":
                return trimmed.Equals("full", StringComparison.OrdinalIgnoreCase) ? options with { Stores = true }
                    : trimmed.Equals("empty", StringComparison.OrdinalIgnoreCase) ? options with { Stores = false }
                    : Warn(options, warnings, $"'--stores' needs full or empty, got '{value}'.");
            case "prophet":
                return OnOff(value) is { } prophet ? options with { Prophet = prophet } : Warn(options, warnings, $"'--prophet' needs on or off, got '{value}'.");
            case "villager":
                return OnOff(value) is { } villager ? options with { Villager = villager } : Warn(options, warnings, $"'--villager' needs on or off, got '{value}'.");
            case "stress":
                return OnOff(value) is { } stress ? options with { Stress = stress } : Warn(options, warnings, $"'--stress' needs on or off, got '{value}'.");
            case "reshape":
                return trimmed.ToLowerInvariant() switch
                {
                    "island" => options with { Reshape = Domain.Land.ReshapeKind.Island },
                    "gorge" => options with { Reshape = Domain.Land.ReshapeKind.Gorge },
                    "river" => options with { Reshape = Domain.Land.ReshapeKind.Channel },
                    _ => Warn(options, warnings, $"'--reshape' needs island, gorge or river, got '{value}'."),
                };
            case "hand":
                return Enum.TryParse<HandAtStart>(trimmed, ignoreCase: true, out var hand) && Enum.IsDefined(hand) && !int.TryParse(trimmed, out _)
                    ? options with { Hand = hand }
                    : Warn(options, warnings, $"'--hand' needs villagers, creature, offering or throw, got '{value}'.");
            case "wildlife":
                return OnOff(value) is { } wild ? options with { FocusWildlife = wild } : Warn(options, warnings, $"'--wildlife' needs on or off, got '{value}'.");
            case "victory":
                return ParseVictory(trimmed) is { } victory
                    ? options with { Victory = victory }
                    : Warn(options, warnings, $"'--victory' needs all, sandbox, or ascendancy, conquest and cultural separated by commas, got '{value}'.");
            default:
                return null;
        }
    }
}
