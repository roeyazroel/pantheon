using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Miracles;

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
        };
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
        var place = judged?.SettlementId is { } id ? world.Settlements.Find(s => s.Id == id)?.Name : null;
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
            Temperament = Format("loyalty {0:0%}   {1} ({2:+0.00;-0.00;0.00})", creature.Loyalty, morality, creature.Morality),
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
