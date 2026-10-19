using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Divine;
using Pantheon.Game.Weather;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Hud;

/// <summary>Builds the player HUD from the session (spec §57). Pure and culture-invariant.</summary>
public static class HudReadout
{
    /// <summary>Most miracles that get a number key.</summary>
    public const int MaxHotkeys = 9;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Builds the HUD.</summary>
    /// <param name="session">Game session.</param>
    /// <param name="selectedMiracleId">Selected miracle, if any.</param>
    /// <param name="target">Ground point under the cursor, if any.</param>
    /// <returns>The HUD model.</returns>
    public static HudModel Build(GameSession session, string? selectedMiracleId, GroundPosition? target)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.LocalGod is not { } god)
        {
            return new HudModel
            {
                HasGod = false,
                GodLine = "No god to play",
                PowerText = string.Empty,
                IncomeText = string.Empty,
                FollowersText = string.Empty,
                Miracles = [],
            };
        }

        var content = session.Content;
        var world = session.World;
        var stepsPerSecond = session.Scheduler.TicksPerSecond / content.DivineRules.StepIntervalTicks;
        var income = GodStanding.ComputePowerPerStep(content.DivineRules, content.SettlementRules, world, god.Id) * stepsPerSecond;
        var followers = GodStanding.CountFollowers(world, god.Id);
        var next = GodStanding.NextRank(content.DivineRules, god.Rank);
        var slots = new List<MiracleSlot>();
        MiracleDefinition? selected = null;
        foreach (var miracle in content.Miracles.Miracles)
        {
            if (string.Equals(miracle.Id, selectedMiracleId, StringComparison.Ordinal))
            {
                selected = miracle;
            }

            slots.Add(new MiracleSlot
            {
                Hotkey = slots.Count < MaxHotkeys ? slots.Count + 1 : 0,
                MiracleId = miracle.Id,
                Name = miracle.Name,
                Tooltip = DescribeMiracle(miracle, session.Scheduler.TicksPerSecond),
                BaseCost = miracle.PowerCost,
                Selected = selected == miracle,
                Locked = god.Rank < miracle.MinimumRank,
                CooldownSeconds = MiracleCaster.CooldownTicksLeft(world, god, miracle.Id) / session.Scheduler.TicksPerSecond,
                Affordable = god.DivinePower >= miracle.PowerCost,
            });
        }

        var (hint, valid) = DescribeTarget(session, selected, target, god.DivinePower);
        return new HudModel
        {
            HasGod = true,
            GodLine = Format("{0} - {1}", god.Name, DivineText.Rank(god.Rank)),
            PowerText = Format("{0:0}", Math.Floor(god.DivinePower)),
            IncomeText = Format("{0:+0.0;-0.0;0.0}/s", income),
            FollowersText = next is null
                ? Format("{0} followers", followers)
                : Format("{0} followers - {1} at {2}", followers, DivineText.Rank(next.Rank), next.Followers),
            Miracles = slots,
            TargetHint = hint,
            TargetValid = valid,
            TargetRadius = selected?.Radius ?? 0f,
            Creature = BuildCreatureCard(session),
            Rivals = BuildRivals(session.World, god.Id, session.Players),
            Contest = DescribeContest(session, god, out var favours),
            ContestFavoursPlayer = favours,
            Weather = DescribeWeather(session, god.Id, out var warning),
            WeatherWarning = warning,
        };
    }

    /// <summary>
    /// The weather line: shown during a dry or wet spell, or when settlements that follow you are in
    /// drought, under a storm, threatened by fire or shaken by an earthquake, which it names.
    /// </summary>
    private static string? DescribeWeather(GameSession session, int godId, out bool warning)
    {
        var world = session.World;
        var rules = session.Content.Weather;
        var dry = new List<string>();
        var stormy = new List<string>();
        var burning = new List<string>();
        var shaken = new List<string>();
        foreach (var settlement in world.Settlements)
        {
            if (BeliefModel.Find(settlement, godId) is not { Adherence: > 0f })
            {
                continue;
            }

            if (SettlementEconomy.DroughtOf(session.Content.SettlementRules, world.Regions, settlement) >= session.Content.SettlementRules.DroughtThreshold)
            {
                dry.Add(settlement.Name);
            }

            if (WeatherModel.At(rules, world.Weather, settlement.Position).Wind >= rules.StormWind)
            {
                stormy.Add(settlement.Name);
            }

            if (DisasterModel.FireThreat(session.Content.SettlementRules, world.Regions, settlement) > 0f)
            {
                burning.Add(settlement.Name);
            }

            if (DisasterModel.ShakingAt(world.Disasters, settlement.Position) > 0f)
            {
                shaken.Add(settlement.Name);
            }
        }

        warning = world.Weather.Spell == WeatherSpell.Dry || dry.Count > 0 || stormy.Count > 0 || burning.Count > 0 || shaken.Count > 0;
        if (world.Weather.Spell == WeatherSpell.Normal && !warning)
        {
            return null;
        }

        var line = WeatherText.Describe(world.Weather, world.Tick);
        if (dry.Count > 0)
        {
            line += " Drought in " + string.Join(", ", dry) + ".";
        }

        if (stormy.Count > 0)
        {
            line += " Storm over " + string.Join(", ", stormy) + ".";
        }

        if (burning.Count > 0)
        {
            line += " Fire near " + string.Join(", ", burning) + ".";
        }

        if (shaken.Count > 0)
        {
            line += " The earth shakes at " + string.Join(", ", shaken) + ".";
        }

        return line;
    }

    /// <summary>One line per other god: its player's name when a player plays it (ADR-018), else its personality.</summary>
    private static IReadOnlyList<string> BuildRivals(WorldState world, int localGodId, IReadOnlyDictionary<int, string> players)
    {
        var lines = new List<string>();
        foreach (var rival in world.Gods)
        {
            if (rival.Id == localGodId)
            {
                continue;
            }

            var personality = DivineText.Personality(rival.Personality);
            var who = players.TryGetValue(rival.Id, out var player) && rival.Controller != GodController.Ai ? " (" + player + ")"
                : personality.Length > 0 ? " the " + personality : string.Empty;
            lines.Add(Format(
                "{0}{1}: {2} followers ({3:0%}), {4}",
                rival.Name,
                who,
                GodStanding.CountFollowers(world, rival.Id),
                VictorySystem.ShareOf(world, rival.Id),
                Diplomacy.DiplomacyText.Status(Simulation.Diplomacy.DiplomacyModel.StatusOf(world, localGodId, rival.Id))));
        }

        return lines;
    }

    /// <summary>The race for victory, from the player's side.</summary>
    private static string? DescribeContest(GameSession session, GodState local, out bool favoursPlayer)
    {
        var world = session.World;
        favoursPlayer = false;
        if (world.Gods.Count < 2)
        {
            return null;
        }

        var rules = session.Content.DivineRules;
        if (world.WinnerGodIds.Count > 0)
        {
            favoursPlayer = world.WinnerGodIds.Contains(local.Id);
            var winners = DivineText.Names(world, world.WinnerGodIds, local.Id);
            return world.WinnerGodIds.Count > 1 ? Format("{0} have won together. The world goes on.", winners)
                : favoursPlayer ? "You have won. The world goes on."
                : Format("{0} has won. The world goes on.", winners);
        }

        foreach (var god in world.Gods)
        {
            if (god.DominantSinceTick is { } since)
            {
                var left = (VictorySystem.TicksToWin(rules) - (world.Tick - since)) / session.Scheduler.TicksPerSecond;
                var (share, allies) = VictorySystem.StandingOf(rules, world, god.Id);
                var who = new List<int>(allies) { god.Id };
                who.Sort();
                favoursPlayer = who.Contains(local.Id);
                return who.Count > 1
                    ? Format("{0} hold {1:0%} of all belief together: they win together in {2}", DivineText.Names(world, who, local.Id), share, DivineText.Clock(left))
                    : favoursPlayer
                        ? Format("You hold {0:0%} of all belief: you win in {1}", share, DivineText.Clock(left))
                        : Format("{0} holds {1:0%} of all belief: it wins in {2}", god.Name, share, DivineText.Clock(left));
            }
        }

        favoursPlayer = true;
        var (mine, mates) = VictorySystem.StandingOf(rules, world, local.Id);
        return mates.Count == 0
            ? Format("You hold {0:0%} of all belief; {1:0%} for {2} wins", mine, rules.VictoryShare, DivineText.Clock(rules.VictorySeconds))
            : Format("{0} hold {1:0%} of all belief together; {2:0%} for {3} wins together", DivineText.Names(world, [.. mates, local.Id], local.Id), mine, rules.VictoryShare, DivineText.Clock(rules.VictorySeconds));
    }

    /// <summary>The Creature's card: level, activity, needs, loyalty and what feedback would judge.</summary>
    /// <param name="session">Game session.</param>
    /// <returns>The card, or <c>null</c> without a Creature.</returns>
    public static CreatureCard? BuildCreatureCard(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.LocalCreature is not { } creature)
        {
            return null;
        }

        var world = session.World;
        var judged = session.FindJudgedAction();
        var needs = creature.Needs;
        var place = judged is { } subject ? CreatureText.Concerning(world, subject.SettlementId, subject.OtherCreatureId) : null;
        var morality = creature.Morality switch
        {
            > 0.3f => "kind",
            < -0.3f => "cruel",
            _ => "neither kind nor cruel",
        };
        return new CreatureCard
        {
            Title = Format("{0} the {1} - level {2}", creature.Name, creature.Species, creature.Level),
            Doing = CreatureText.DescribeActivity(creature, world),
            Condition = Format("health {0:0%}   hunger {1:0%}   tiredness {2:0%}   boredom {3:0%}", creature.Health, needs.Hunger, needs.Fatigue, needs.Boredom),
            Temperament = Format(
                "loyalty {0:0%}   {1} ({2:+0.00;-0.00;0.00})   combat skill {3:0%}{4}",
                creature.Loyalty,
                morality,
                creature.Morality,
                creature.CombatSkill,
                creature.Scars switch { 0 => string.Empty, 1 => "   1 scar", _ => Format("   {0} scars", creature.Scars) }),
            Bonds = CreatureBondText.Summary(session.Content.Creatures.Bonds, world, creature),
            JudgeHint = judged is { } j
                ? Format("Reward or punish: {0}", CreatureText.Doing(j.Action, place))
                : "Nothing to reward or punish right now",
            CanJudge = judged is not null,
            Banished = creature.Activity.Phase == CreaturePhase.Banished,
        };
    }

    /// <summary>The verdict on casting the selected miracle at the cursor.</summary>
    private static (string? Hint, bool Valid) DescribeTarget(GameSession session, MiracleDefinition? selected, GroundPosition? target, float power)
    {
        if (selected is null)
        {
            return (null, false);
        }

        if (target is not { } point)
        {
            return (Format("{0}: point at the ground to cast", selected.Name), false);
        }

        var check = session.CheckCast(selected.Id, point);
        var reached = string.Join(", ", session.FindReached(selected.Id, point).Select(s => s.Name));
        return (DivineText.DescribeCheck(check, selected.Id, power, session.Scheduler.TicksPerSecond, reached), check.IsAccepted);
    }

    /// <summary>Tooltip text of a miracle.</summary>
    private static string DescribeMiracle(MiracleDefinition miracle, double ticksPerSecond) =>
        Format(
            "{0}\n{1}\nCost {2:0} (more where your influence is weak), radius {3:0} m, cooldown {4:0} s\nNeeds {5:0%} influence and the rank of {6}",
            miracle.Name,
            miracle.Description,
            miracle.PowerCost,
            miracle.Radius,
            miracle.CooldownTicks / Math.Max(1.0, ticksPerSecond),
            miracle.InfluenceRequirement,
            DivineText.Rank(miracle.MinimumRank));

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(Invariant, format, args);
}
