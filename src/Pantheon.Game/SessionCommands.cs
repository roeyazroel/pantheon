using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.Divine;
using Pantheon.Persistence;

namespace Pantheon.Game;

/// <summary>
/// Player and debug commands on a <see cref="GameSession"/>. Each returns a one-line status for
/// the debug overlay. Save and load problems are reported in the status rather than thrown, so a
/// bad file never ends the game.
/// </summary>
public static class SessionCommands
{
    /// <summary>Share of a settlement's population that arrives with <see cref="AddSettlers"/>.</summary>
    public const float SettlerShare = 0.5f;

    /// <summary>Fewest settlers that arrive with <see cref="AddSettlers"/>, so an empty settlement can be refounded.</summary>
    public const int MinimumSettlers = 10;

    /// <summary>Divine Power granted by the <see cref="GrantPower"/> debug command.</summary>
    public const float GrantedPower = 500f;

    /// <summary>Time scales selected by the speed hotkeys, in order.</summary>
    public static IReadOnlyList<double> SpeedPresets { get; } = [1.0, 4.0, 16.0];

    /// <summary>Saves the current world.</summary>
    /// <param name="session">Session to save.</param>
    /// <param name="path">Save file path.</param>
    /// <returns>Status line.</returns>
    public static string QuickSave(GameSession session, string path)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        try
        {
            session.SaveTo(path);
            return Format("Saved tick {0} to {1}", session.World.Tick, Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Format("Save failed: {0}", exception.Message);
        }
    }

    /// <summary>Replaces the current world with a saved one. On failure the current world is kept.</summary>
    /// <param name="session">Session to load into.</param>
    /// <param name="path">Save file path.</param>
    /// <returns>Status line.</returns>
    public static string QuickLoad(GameSession session, string path)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        if (!File.Exists(path))
        {
            return Format("No save found at {0}", Path.GetFileName(path));
        }

        try
        {
            session.LoadFrom(path);
            return Format("Loaded tick {0} of seed {1} from {2}", session.World.Tick, session.World.Seed, Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            return Format("Load failed, current world kept: {0}", exception.Message);
        }
    }

    /// <summary>Replaces the current world with a newly generated one.</summary>
    /// <param name="session">Session to replace the world of.</param>
    /// <param name="seed">Seed of the new world.</param>
    /// <returns>Status line.</returns>
    public static string NewWorld(GameSession session, WorldSeed seed)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        session.StartNewWorld(seed);
        return Format("New world from seed {0}", seed);
    }

    /// <summary>Pauses a running simulation or resumes a paused one.</summary>
    /// <param name="session">Session whose clock is toggled.</param>
    /// <returns>Status line.</returns>
    public static string TogglePause(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        session.Scheduler.IsPaused = !session.Scheduler.IsPaused;
        return session.Scheduler.IsPaused ? "Simulation paused" : "Simulation resumed";
    }

    /// <summary>Selects one of the <see cref="SpeedPresets"/> and resumes the simulation.</summary>
    /// <param name="session">Session whose clock is changed.</param>
    /// <param name="preset">Zero-based index into <see cref="SpeedPresets"/>.</param>
    /// <returns>Status line.</returns>
    public static string SetSpeedPreset(GameSession session, int preset)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(preset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(preset, SpeedPresets.Count);

        session.Scheduler.TimeScale = SpeedPresets[preset];
        session.Scheduler.IsPaused = false;
        return Format("Simulation speed x{0}", SpeedPresets[preset]);
    }

    /// <summary>Selects the next faster (<paramref name="step"/> 1) or slower (-1) speed preset and resumes the simulation.</summary>
    /// <param name="session">Session whose clock is changed.</param>
    /// <param name="step">+1 for faster, -1 for slower.</param>
    /// <returns>Status line.</returns>
    public static string ChangeSpeed(GameSession session, int step)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var current = 0;
        for (var i = 0; i < SpeedPresets.Count; i++)
        {
            if (Math.Abs(SpeedPresets[i] - session.Scheduler.TimeScale) < Math.Abs(SpeedPresets[current] - session.Scheduler.TimeScale))
            {
                current = i;
            }
        }

        return SetSpeedPreset(session, Math.Clamp(current + Math.Sign(step), 0, SpeedPresets.Count - 1));
    }

    /// <summary>The words for a command only the host may give, on a joined player's replica (ADR-018).</summary>
    public const string HostOnlyText = "Only the host can do that";

    /// <summary>The refusal of a command only the host may give: saving, loading, the clock and the debug tools.</summary>
    /// <param name="session">The session.</param>
    /// <returns><see cref="HostOnlyText"/> on a replica, <c>null</c> on the host or alone.</returns>
    public static string? HostOnly(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.IsReplica ? HostOnlyText : null;
    }

    /// <summary>Casts a miracle for the local player's god; the session checks it first (spec §73), or the host does for a joined player.</summary>
    /// <param name="session">Session to cast in.</param>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where to cast it.</param>
    /// <returns>Status line: the result, or why it was refused, or that the host was asked.</returns>
    public static string CastMiracle(GameSession session, string miracleId, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Client is { } client
            ? client.Request(id => new Networking.Protocol.CastIntent(id, miracleId, target))
            : CastMiracleAs(session, session.LocalGodId ?? 0, miracleId, target).Text;
    }

    /// <summary>Casts a miracle for a god, as its player's intent: the local player's, or a joined player's on the host.</summary>
    /// <param name="session">Session to cast in.</param>
    /// <param name="godId">The issuing god.</param>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where to cast it.</param>
    /// <returns>Whether it was cast, and the status line: the result, or why it was refused.</returns>
    public static CommandOutcome CastMiracleAs(GameSession session, int godId, string miracleId, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        var power = session.World.Gods.Find(g => g.Id == godId)?.DivinePower ?? 0f;
        var check = session.CastMiracleAs(godId, miracleId, target);
        if (!check.IsAccepted || check.Miracle is not { } miracle)
        {
            return new CommandOutcome(false, DivineText.DescribeCheck(check, miracleId, power, session.Scheduler.TicksPerSecond));
        }

        var cast = session.PeekLastCast();
        var names = cast is null
            ? string.Empty
            : string.Join(", ", session.World.Settlements.Where(s => cast.AffectedSettlementIds.Contains(s.Id)).Select(s => s.Name));
        return new CommandOutcome(true, DivineText.DescribeCast(miracle, check.Cost, names, cast?.Deaths ?? 0));
    }

    /// <summary>Sends a command to the local player's Creature; the session checks it first.</summary>
    /// <param name="session">Session to command in.</param>
    /// <param name="kind">Reward, punish or go to.</param>
    /// <param name="target">Where to go, for <see cref="CreatureCommandKind.GoTo"/>.</param>
    /// <returns>Status line.</returns>
    public static string CommandCreature(GameSession session, CreatureCommandKind kind, GroundPosition target = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Client is { } client
            ? client.Request(id => new Networking.Protocol.CreatureIntent(id, kind, target))
            : CommandCreatureAs(session, session.LocalGodId ?? 0, kind, target).Text;
    }

    /// <summary>Sends a command to a god's Creature, as its player's intent.</summary>
    /// <param name="session">Session to command in.</param>
    /// <param name="godId">The issuing god.</param>
    /// <param name="kind">Reward, punish or go to.</param>
    /// <param name="target">Where to go, for <see cref="CreatureCommandKind.GoTo"/>.</param>
    /// <returns>Whether it was carried out, and the status line.</returns>
    public static CommandOutcome CommandCreatureAs(GameSession session, int godId, CreatureCommandKind kind, GroundPosition target = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var creatureId = session.World.Gods.Find(g => g.Id == godId)?.CreatureId;
        var name = session.World.Creatures.Find(c => c.Id == creatureId)?.Name ?? "Your Creature";
        var result = session.CommandCreatureAs(godId, kind, target);
        return new CommandOutcome(result.IsAccepted, CreatureText.DescribeCommand(result, kind, name));
    }

    /// <summary>Sends a diplomatic command to another god; the session checks it first.</summary>
    /// <param name="session">Session to command in.</param>
    /// <param name="otherGodId">The other god.</param>
    /// <param name="kind">What to do.</param>
    /// <returns>Status line.</returns>
    public static string CommandDiplomacy(GameSession session, int otherGodId, Domain.Diplomacy.DiplomaticCommandKind kind)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Client is { } client
            ? client.Request(id => new Networking.Protocol.DiplomacyIntent(id, otherGodId, kind))
            : CommandDiplomacyAs(session, session.LocalGodId ?? 0, otherGodId, kind).Text;
    }

    /// <summary>Sends a diplomatic command from a god to another, as its player's intent.</summary>
    /// <param name="session">Session to command in.</param>
    /// <param name="godId">The issuing god.</param>
    /// <param name="otherGodId">The other god.</param>
    /// <param name="kind">What to do.</param>
    /// <returns>Whether it was carried out, and the status line.</returns>
    public static CommandOutcome CommandDiplomacyAs(GameSession session, int godId, int otherGodId, Domain.Diplomacy.DiplomaticCommandKind kind)
    {
        ArgumentNullException.ThrowIfNull(session);
        var other = session.World.Gods.Find(g => g.Id == otherGodId)?.Name ?? "that god";
        var rejection = session.CommandDiplomacyAs(godId, otherGodId, kind);
        return rejection == Simulation.Diplomacy.DiplomacyRejection.None
            ? new CommandOutcome(true, Format("{0}: {1}", DiplomacyText.Command(kind), other))
            : new CommandOutcome(false, DiplomacyText.DescribeRejection(rejection, kind, other));
    }

    /// <summary>Debug: grants the local player's god <see cref="GrantedPower"/> Divine Power.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <returns>Status line.</returns>
    public static string GrantPower(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        if (session.LocalGod is not { } god)
        {
            return "No god to grant power to";
        }

        god.DivinePower += GrantedPower;
        return Format("{0} gains {1:0} Divine Power (now {2:0})", god.Name, GrantedPower, god.DivinePower);
    }

    /// <summary>Pauses the simulation, then runs exactly one tick, so its effect can be inspected.</summary>
    /// <param name="session">Session to step.</param>
    /// <returns>Status line.</returns>
    public static string StepTick(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        session.Scheduler.IsPaused = true;
        session.StepOneTick();
        return Format("Stepped to tick {0} (paused)", session.World.Tick);
    }

    /// <summary>Pauses the simulation, then runs ticks up to and including the next settlement step.</summary>
    /// <param name="session">Session to step.</param>
    /// <returns>Status line.</returns>
    public static string StepSettlement(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        session.Scheduler.IsPaused = true;
        var ticks = session.StepToNextSettlementStep();
        return Format("Settlement step: ran {0} tick(s) to tick {1} (paused)", ticks, session.World.Tick);
    }

    /// <summary>Runs several ticks at once, for example before the first frame of a capture.</summary>
    /// <param name="session">Session to advance.</param>
    /// <param name="ticks">Number of ticks; must not be negative.</param>
    /// <returns>Status line.</returns>
    public static string AdvanceTicks(GameSession session, int ticks)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        session.RunTicks(ticks);
        return Format("Advanced {0} ticks to tick {1}", ticks, session.World.Tick);
    }

    /// <summary>Debug famine: every settlement loses its stored food.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <returns>Status line.</returns>
    public static string SpoilFood(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var changes = new List<string>();
        foreach (var settlement in session.World.Settlements)
        {
            changes.Add(Format("{0} lost {1:0} food", settlement.Name, settlement.Food));
            settlement.Food = 0f;
        }

        return changes.Count > 0 ? "Food stores spoiled: " + string.Join(", ", changes) : "No settlement to spoil";
    }

    /// <summary>
    /// Debug migration: settlers arrive in every settlement, adding <see cref="SettlerShare"/> of
    /// its population (at least <see cref="MinimumSettlers"/>), even beyond its housing.
    /// </summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <returns>Status line.</returns>
    public static string AddSettlers(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var changes = new List<string>();
        foreach (var settlement in session.World.Settlements)
        {
            var settlers = Math.Max(MinimumSettlers, (int)(settlement.Population * SettlerShare));
            settlement.Population += settlers;
            changes.Add(Format("{0} +{1} (now {2})", settlement.Name, settlers, settlement.Population));
        }

        return changes.Count > 0 ? "Settlers arrived: " + string.Join(", ", changes) : "No settlement for settlers";
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}

/// <summary>What a player's command came to: whether the world changed, and the status line that says so or why not.</summary>
/// <param name="Accepted">Whether it was carried out.</param>
/// <param name="Text">The status line.</param>
public readonly record struct CommandOutcome(bool Accepted, string Text);
