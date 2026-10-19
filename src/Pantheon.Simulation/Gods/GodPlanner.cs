using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.GreatEvents;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Gods;

/// <summary>A goal an AI god could set itself, with its weight and why (ADR-036).</summary>
/// <param name="Kind">What it is.</param>
/// <param name="SettlementId">The village it concerns, if any.</param>
/// <param name="GodId">The god it concerns, if any.</param>
/// <param name="Weight">How much it matters (0..1).</param>
/// <param name="Reason">Why, for the debug view.</param>
public sealed record GoalOption(GodGoalKind Kind, int? SettlementId, int? GodId, float Weight, string Reason);

/// <summary>
/// The major planning of an AI god (spec §47's long-term goals, §87, ADR-036). Every
/// <c>PlanSeconds</c> it weighs what it could set out to do: protect its home from what threatens
/// it, convert a village within reach, befriend or weaken a god it knows, prepare for a drought or
/// the plague, bring down the ancient beast and win back its doubters. It holds the weightiest few,
/// one of each kind, and keeps a goal for at least <c>MinGoalSeconds</c> unless it is moot. The
/// strategic evaluation and the diplomat then favour what serves its goals (<see cref="GoalFocus"/>).
/// </summary>
public sealed class GodPlanner
{
    // An enemy Creature this near a god's home threatens it.
    private const float ThreatMetres = 300f;

    private readonly PlanningRules _rules;
    private readonly GodAiRules _ai;
    private readonly DiplomacyRules? _diplomacy;

    /// <summary>Creates the planner.</summary>
    /// <param name="ai">AI rules, with the planning rules.</param>
    /// <param name="diplomacy">Diplomacy rules: how gods come to know each other (a god plans only about gods it knows) and how they feel at rest; <c>null</c> where there is no diplomacy.</param>
    public GodPlanner(GodAiRules ai, DiplomacyRules? diplomacy)
    {
        ArgumentNullException.ThrowIfNull(ai);
        _ai = ai;
        _rules = ai.Planning;
        _diplomacy = diplomacy;
    }

    /// <summary>The planning rules.</summary>
    public PlanningRules Rules => _rules;

    /// <summary>Plans when the god's plan is due: keeps its young goals, then takes up the weightiest of the rest.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <param name="events">Receives <see cref="GodGoalTaken"/> for every new goal.</param>
    public void PlanIfDue(WorldState world, GodState god, GodProfile profile, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(events);
        if (!_rules.Enabled || world.Tick < god.NextPlanTick)
        {
            return;
        }

        var chosen = new List<GodGoal>();
        foreach (var goal in god.Goals)
        {
            var weight = Score(world, god, profile, goal.Kind, goal.SettlementId, goal.GodId)?.Weight ?? 0f;
            if (weight > 0f && world.Tick - goal.SinceTick < Ticks(_rules.MinGoalSeconds))
            {
                goal.Weight = weight;
                chosen.Add(goal);
            }
        }

        foreach (var option in Options(world, god, profile))
        {
            if (chosen.Count >= _rules.MaxGoals)
            {
                break;
            }

            if (option.Weight < _rules.Threshold || chosen.Exists(g => g.Kind == option.Kind))
            {
                continue;
            }

            if (god.Goals.Find(g => g.Kind == option.Kind && g.SettlementId == option.SettlementId && g.GodId == option.GodId) is { } held)
            {
                held.Weight = option.Weight;
                chosen.Add(held);
                continue;
            }

            chosen.Add(new GodGoal { Kind = option.Kind, SettlementId = option.SettlementId, GodId = option.GodId, SinceTick = world.Tick, Weight = option.Weight });
            events.Add(new GodGoalTaken { Tick = world.Tick, GodId = god.Id, Kind = option.Kind, SettlementId = option.SettlementId, OtherGodId = option.GodId });
        }

        god.Goals.Clear();
        god.Goals.AddRange(chosen);
        god.NextPlanTick = world.Tick + Math.Max(1L, Ticks(_rules.PlanSeconds));
    }

    /// <summary>The best goal of each kind the god could set itself now, weightiest first.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <returns>At most one option of each kind; none without planning.</returns>
    public List<GoalOption> Options(WorldState world, GodState god, GodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        var options = new List<GoalOption>();
        if (!_rules.Enabled)
        {
            return options;
        }

        Best(options, Score(world, god, profile, GodGoalKind.ProtectHome, Home(world, god.Id)?.Id, null));
        GoalOption? convert = null;
        foreach (var settlement in world.Settlements)
        {
            convert = Better(convert, Score(world, god, profile, GodGoalKind.Convert, settlement.Id, null));
        }

        Best(options, convert);
        GoalOption? befriend = null;
        GoalOption? weaken = null;
        foreach (var other in world.Gods)
        {
            befriend = Better(befriend, Score(world, god, profile, GodGoalKind.Befriend, null, other.Id));
            weaken = Better(weaken, Score(world, god, profile, GodGoalKind.Weaken, null, other.Id));
        }

        Best(options, befriend);
        Best(options, weaken);
        Best(options, Score(world, god, profile, GodGoalKind.Prepare, null, null));
        Best(options, Score(world, god, profile, GodGoalKind.SlayBeast, null, null));
        Best(options, Score(world, god, profile, GodGoalKind.Recover, null, null));
        options.Sort(static (a, b) => b.Weight.CompareTo(a.Weight));
        return options;
    }

    /// <summary>What the god prepares its people against: the plague's and the drought's severity (0..1).</summary>
    /// <param name="world">World state.</param>
    /// <param name="rules">Planning rules.</param>
    /// <returns>The plague's and the drought's severity.</returns>
    public static (float Plague, float Drought) Hardships(WorldState world, PlanningRules rules)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        var plague = 0f;
        foreach (var settlement in world.Settlements)
        {
            plague = Math.Max(plague, settlement.Population > 0 ? settlement.Plague : 0f);
        }

        plague = GreatEventModel.Active(world, GreatEventKind.Plague) is not null ? 1f : SimMath.Clamp01(plague * 4f);
        var dryness = 1f - GreatEventWatch.LandSoil(world);
        var drought = rules.Dryness >= 1f ? 0f : SimMath.Clamp01((dryness - rules.Dryness) / (1f - rules.Dryness));
        return (plague, drought);
    }

    /// <summary>The village a god counts as its home: where most of its followers live, among those it leads.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The village, or <c>null</c> when it leads none.</returns>
    public static SettlementState? Home(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        SettlementState? home = null;
        var most = 0f;
        foreach (var settlement in world.Settlements)
        {
            var followers = settlement.Population * (BeliefModel.Find(settlement, godId)?.Adherence ?? 0f);
            if (followers > most && TreatyModel.Leader(settlement) == godId)
            {
                most = followers;
                home = settlement;
            }
        }

        return home;
    }

    /// <summary>The weight of one goal now, or <c>null</c> when it is moot.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <param name="kind">The goal.</param>
    /// <param name="settlementId">The village it concerns, if any.</param>
    /// <param name="otherGodId">The god it concerns, if any.</param>
    /// <returns>The option, or <c>null</c>.</returns>
    public GoalOption? Score(WorldState world, GodState god, GodProfile profile, GodGoalKind kind, int? settlementId, int? otherGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        var settlement = settlementId is { } id ? world.Settlements.Find(s => s.Id == id && s.Population > 0) : null;
        var other = otherGodId is { } godId && godId != god.Id && Acquaintance.Knows(_diplomacy?.Acquaintance ?? AcquaintanceRules.None, world, god.Id, godId) ? GodStanding.Find(world, godId) : null;
        return kind switch
        {
            GodGoalKind.ProtectHome => settlement is not null ? Protect(world, god, profile, settlement) : null,
            GodGoalKind.Convert => settlement is not null ? Convert(world, god, profile, settlement) : null,
            GodGoalKind.Befriend => other is not null ? Befriend(world, god, profile, other) : null,
            GodGoalKind.Weaken => other is not null ? Weaken(world, god, profile, other) : null,
            GodGoalKind.Prepare => Prepare(world, profile),
            GodGoalKind.SlayBeast => SlayBeast(world, god, profile),
            GodGoalKind.Recover => Recover(world, god, profile),
        };
    }

    /// <summary>Protecting its home from the beast, an enemy's Creature or the plague.</summary>
    private GoalOption? Protect(WorldState world, GodState god, GodProfile profile, SettlementState home)
    {
        if (TreatyModel.Leader(home) != god.Id)
        {
            return null;
        }

        var (danger, from) = (0f, string.Empty);
        if (world.GreatEvents.Beast is { Phase: BeastPhase.Awake } beast && beast.TargetSettlementId == home.Id)
        {
            (danger, from) = (1f, beast.Name);
        }

        foreach (var creature in world.Creatures)
        {
            if (danger < 0.8f && creature.GodId != god.Id && DiplomacyModel.AreAtWar(world, god.Id, creature.GodId) && Near(creature.Position, home.Position, ThreatMetres))
            {
                (danger, from) = (0.8f, creature.Name);
            }
        }

        if (home.Plague * 2f > danger)
        {
            (danger, from) = (SimMath.Clamp01(home.Plague * 2f), "the plague");
        }

        return danger >= _rules.HomeDanger
            ? new GoalOption(GodGoalKind.ProtectHome, home.Id, null, SimMath.Clamp01(profile.Protect * danger), Format("protect {0} from {1}", home.Name, from))
            : null;
    }

    /// <summary>Converting a village within reach of its own that neither it nor an ally leads, the bigger and less its own the better.</summary>
    private GoalOption? Convert(WorldState world, GodState god, GodProfile profile, SettlementState settlement)
    {
        var leader = TreatyModel.Leader(settlement);
        if (leader == god.Id || (leader is { } ally && DiplomacyModel.AreAllied(world, god.Id, ally)) || !WithinReach(world, god.Id, settlement))
        {
            return null;
        }

        var mine = BeliefModel.Find(settlement, god.Id)?.Adherence ?? 0f;
        var size = Math.Min(1f, MathF.Sqrt(settlement.Population / _ai.ReferencePopulation));
        var weight = profile.Convert * (1f - mine) * size;
        return weight > 0f ? new GoalOption(GodGoalKind.Convert, settlement.Id, null, weight, Format("convert {0} ({1:0%} ours)", settlement.Name, mine)) : null;
    }

    /// <summary>Befriending a god it knows and is neither allied with nor at war with: the warmer it feels towards it and the more a shared enemy, the more.</summary>
    private GoalOption? Befriend(WorldState world, GodState god, GodProfile profile, GodState other)
    {
        var status = DiplomacyModel.StatusOf(world, god.Id, other.Id);
        if (status is DiplomaticStatus.Alliance or DiplomaticStatus.War)
        {
            return null;
        }

        var warmth = Feeling(world, god.Id, other.Id);
        var shared = SharesEnemy(world, god.Id, other.Id) ? 0.3f : 0f;
        var weight = profile.Cooperation * SimMath.Clamp01(0.5f + (0.5f * warmth) + shared - (status == DiplomaticStatus.Hostile ? 0.3f : 0f));
        return weight > 0f ? new GoalOption(GodGoalKind.Befriend, null, other.Id, weight, Format("befriend {0} (warmth {1:+0.00;-0.00;0.00}{2})", other.Name, warmth, shared > 0f ? ", a shared enemy" : string.Empty)) : null;
    }

    /// <summary>Weakening a god it knows and is not allied with: the more it leads, the colder this god feels towards it, and at war most of all.</summary>
    private GoalOption? Weaken(WorldState world, GodState god, GodProfile profile, GodState other)
    {
        if (DiplomacyModel.AreAllied(world, god.Id, other.Id))
        {
            return null;
        }

        var mine = GodStanding.CountFollowers(world, god.Id);
        var theirs = GodStanding.CountFollowers(world, other.Id);
        var lead = Math.Max(0f, (theirs - mine) / (float)Math.Max(1, theirs + mine));
        var hate = Math.Max(0f, -Feeling(world, god.Id, other.Id));
        var war = DiplomacyModel.AreAtWar(world, god.Id, other.Id) ? 0.4f : 0f;
        var weight = profile.Aggression * SimMath.Clamp01(lead + hate + war);
        return weight > 0f ? new GoalOption(GodGoalKind.Weaken, null, other.Id, weight, Format("weaken {0} (leads {1:0.00}, hate {2:0.00}{3})", other.Name, lead, hate, war > 0f ? ", at war" : string.Empty)) : null;
    }

    /// <summary>Preparing its people for the plague or a drought.</summary>
    private GoalOption? Prepare(WorldState world, GodProfile profile)
    {
        var (plague, drought) = Hardships(world, _rules);
        var weight = profile.Protect * Math.Max(plague, drought);
        return weight > 0f ? new GoalOption(GodGoalKind.Prepare, null, null, weight, plague >= drought ? Format("prepare for the plague ({0:0.00})", plague) : Format("prepare for the drought ({0:0.00})", drought)) : null;
    }

    /// <summary>Bringing down the ancient beast while it is awake: the protective for their people, the bold and the glory-seeking for the glory.</summary>
    private static GoalOption? SlayBeast(WorldState world, GodState god, GodProfile profile)
    {
        if (world.GreatEvents.Beast is not { Phase: BeastPhase.Awake or BeastPhase.Returning } beast)
        {
            return null;
        }

        var threat = beast.TargetSettlementId is { } target && world.Settlements.Find(s => s.Id == target) is { } prey ? BeliefModel.Find(prey, god.Id)?.Adherence ?? 0f : 0f;
        var weight = SimMath.Clamp01((profile.Protect * threat) + (0.4f * profile.Aggression) + (0.3f * profile.Opportunism));
        return new GoalOption(GodGoalKind.SlayBeast, null, null, weight, Format("slay {0} (threat {1:0.00})", beast.Name, threat));
    }

    /// <summary>Winning back its doubters: worth more to the protective and the blessing the more they doubt.</summary>
    private GoalOption? Recover(WorldState world, GodState god, GodProfile profile)
    {
        var (doubt, followers) = (0f, 0f);
        foreach (var settlement in world.Settlements)
        {
            if (BeliefModel.Find(settlement, god.Id) is { } belief && settlement.Population > 0)
            {
                var people = settlement.Population * belief.Adherence;
                doubt += belief.Doubt * people;
                followers += people;
            }
        }

        doubt = followers > 0f ? doubt / followers : 0f;
        return doubt >= _rules.Doubt
            ? new GoalOption(GodGoalKind.Recover, null, null, Math.Max(profile.Protect, profile.Bless) * SimMath.Clamp01(doubt * 2f), Format("win back the doubters ({0:0.00})", doubt))
            : null;
    }

    /// <summary>
    /// How warm a god feels towards another beyond how gods feel at rest (-1..1): its warmth less a
    /// fresh relation's, which starting rivalry and suspicion leave cool. 0 for gods with no dealings.
    /// </summary>
    private float Feeling(WorldState world, int godId, int otherId)
    {
        if (_diplomacy is null || DiplomacyModel.FindRelation(world, godId, otherId) is not { } view)
        {
            return 0f;
        }

        return Math.Clamp(DiplomacySystem.Warmth(view) - DiplomacySystem.Warmth(DiplomacyModel.NewRelation(_diplomacy, godId, otherId)), -1f, 1f);
    }

    /// <summary>Whether a village lies within reach of one the god leads.</summary>
    private bool WithinReach(WorldState world, int godId, SettlementState settlement)
    {
        foreach (var own in world.Settlements)
        {
            if (own.Population > 0 && Near(own.Position, settlement.Position, _rules.ConvertReach) && TreatyModel.Leader(own) == godId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether two gods are both at war with a third.</summary>
    private static bool SharesEnemy(WorldState world, int godId, int otherId)
    {
        foreach (var third in world.Gods)
        {
            if (third.Id != godId && third.Id != otherId && DiplomacyModel.AreAtWar(world, godId, third.Id) && DiplomacyModel.AreAtWar(world, otherId, third.Id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adds an option to the list when there is one.</summary>
    private static void Best(List<GoalOption> options, GoalOption? option)
    {
        if (option is not null)
        {
            options.Add(option);
        }
    }

    /// <summary>The weightier of two options.</summary>
    private static GoalOption? Better(GoalOption? best, GoalOption? option) => option is not null && (best is null || option.Weight > best.Weight) ? option : best;

    /// <summary>Whether two points lie within a distance.</summary>
    private static bool Near(GroundPosition a, GroundPosition b, float metres)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return (dx * dx) + (dz * dz) <= metres * metres;
    }

    /// <summary>Seconds in ticks.</summary>
    private static long Ticks(float seconds) => (long)MathF.Round(seconds * SimulationTime.TicksPerSecond);

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
}
