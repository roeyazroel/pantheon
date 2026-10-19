using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Conflict;

/// <summary>
/// Mortal holy wars (spec §46, ADR-026): a militarist settlement may decide, of its own will, that
/// a neighbour of another faith is its enemy, and raid it: people die, followers of the other god
/// are converted by force, and the attacked feel unsafe. Its god may encourage (fiercer raids) or
/// discourage (gentler raids and an earlier end, at the cost of the people's love). It ends when
/// its time runs out, the zeal fades, or the attacked village turns to the attackers' god.
/// </summary>
public sealed class HolyWarSystem : ISimulationSystem
{
    /// <summary>The stream holy wars are declared from, derived from the seed, the settlement and the tick.</summary>
    public const string Stream = "conflict.holywar";

    // The attackers' god must hold this share of them.
    private const float Zeal = 0.4f;

    private readonly ConflictRules _rules;
    private readonly int _range;
    private readonly IActRecorder? _ledger;
    private readonly int _intervalTicks;

    /// <summary>Creates the system.</summary>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="range">How many regions away a neighbour may be attacked.</param>
    /// <param name="ledger">Where the attacked god notes the war; <c>null</c> for none.</param>
    /// <param name="intervalTicks">Ticks between steps.</param>
    public HolyWarSystem(ConflictRules rules, int range, IActRecorder? ledger, int intervalTicks)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _rules = rules;
        _range = Math.Max(1, range);
        _ledger = ledger;
        _intervalTicks = intervalTicks;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population <= 0)
            {
                settlement.Society.HolyWar = null;
                continue;
            }

            if (settlement.Society.HolyWar is { } war)
            {
                Wage(world, settlement, war, seconds, context.Events);
            }
            else
            {
                Consider(world, settlement, seconds, context.Events);
            }
        }
    }

    /// <summary>A settlement ready for holy war may declare one on its most hated neighbour of another faith.</summary>
    private void Consider(WorldState world, SettlementState settlement, float seconds, List<WorldEvent> events)
    {
        var militarism = settlement.Culture.Militarism;
        var restUntil = settlement.Society.HolyWarEndedTick + (long)(_rules.HolyWarCooldownSeconds * SimulationTime.TicksPerSecond);
        if (militarism < _rules.HolyWarMilitarism || world.Tick < restUntil || Culture.CultureModel.Main(settlement) is not { Adherence: >= Zeal } main
            || CouncilModel.IsNeutral(world, settlement.Id))
        {
            return;
        }

        if (Target(world, settlement, main.GodId) is not { } target)
        {
            return;
        }

        var enemy = TreatyModel.Leader(target)!.Value;
        var war = DiplomacyModel.AreAtWar(world, main.GodId, enemy) ? 2f : 1f;
        var draw = new SimulationRandom(SeedMixer.Derive(world.Seed, Stream + "." + settlement.Id, world.Tick)).NextFloat();
        if (draw >= _rules.HolyWarPerSecond * seconds * war * militarism)
        {
            return;
        }

        settlement.Society.HolyWar = new HolyWarState
        {
            DefenderSettlementId = target.Id, GodId = main.GodId, EnemyGodId = enemy, SinceTick = world.Tick, Stance = HolyWarStance.Ignore, Killed = 0, Converted = 0,
        };
        _ledger?.Record(world, enemy, main.GodId, DiplomaticAct.HolyWar, target.Id);
        events.Add(new HolyWarDeclared { Tick = world.Tick, SettlementId = settlement.Id, DefenderSettlementId = target.Id, GodId = main.GodId, EnemyGodId = enemy });
    }

    /// <summary>The neighbour of another faith a settlement would attack: in range, led by a god that is not its god's ally or treaty partner against war, the most populous.</summary>
    private SettlementState? Target(WorldState world, SettlementState settlement, int godId)
    {
        var (column, row) = world.Regions.GetCoordinates(settlement.RegionId);
        SettlementState? best = null;
        foreach (var other in world.Settlements)
        {
            if (other.Id == settlement.Id || other.Population <= 0 || TreatyModel.Leader(other) is not { } leader || leader == godId
                || DiplomacyModel.AreAllied(world, godId, leader) || TreatyModel.ForbidsWar(world, godId, leader) || CouncilModel.IsNeutral(world, other.Id))
            {
                continue;
            }

            var (c, r) = world.Regions.GetCoordinates(other.RegionId);
            if (Math.Max(Math.Abs(c - column), Math.Abs(r - row)) <= _range && (best is null || other.Population > best.Population))
            {
                best = other;
            }
        }

        return best;
    }

    /// <summary>One raid, then the end if it is due.</summary>
    private void Wage(WorldState world, SettlementState attackers, HolyWarState war, float seconds, List<WorldEvent> events)
    {
        var defenders = world.Settlements.Find(s => s.Id == war.DefenderSettlementId);
        var discouraged = war.Stance == HolyWarStance.Discourage;
        var length = _rules.HolyWarSeconds * (discouraged ? 0.5f : 1f);
        var over = defenders is null || defenders.Population <= 0 || world.Tick - war.SinceTick >= (long)(length * SimulationTime.TicksPerSecond)
            || attackers.Culture.Militarism < _rules.HolyWarMilitarism - 0.15f || DiplomacyModel.AreAllied(world, war.GodId, war.EnemyGodId)
            || TreatyModel.ForbidsWar(world, war.GodId, war.EnemyGodId);
        var won = defenders is not null && TreatyModel.Leader(defenders) == war.GodId;
        if (over || won)
        {
            End(world, attackers, war, won, events);
            return;
        }

        Raid(world, attackers, defenders!, war, seconds);
    }

    /// <summary>The attackers strike: deaths and forced conversions by their strength against the defenders'.</summary>
    private void Raid(WorldState world, SettlementState attackers, SettlementState defenders, HolyWarState war, float seconds)
    {
        var stance = war.Stance switch
        {
            HolyWarStance.Encourage => 1.5f,
            HolyWarStance.Discourage => 0.5f,
            HolyWarStance.Ignore => 1f,
        };
        var attack = attackers.Population * (0.5f + attackers.Culture.Militarism) * (1f + (0.2f * attackers.Society.Technology.Warfare)) * stance;
        var walls = defenders.Society.Buildings.Exists(b => b.Kind == BuildingKind.Walls) ? 2f : 1f;
        var defence = Math.Max(1f, defenders.Population * (0.5f + defenders.Society.Safety) * walls);
        var strength = Math.Clamp(attack / defence, 0.25f, 4f);
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, Stream + ".raid." + attackers.Id, world.Tick));
        var deaths = Whole(defenders.Population * _rules.RaidDeathsPerSecond * seconds * strength, random);
        defenders.Population -= Math.Min(deaths, defenders.Population);
        war.Killed += deaths;
        if (BeliefModel.Find(defenders, war.EnemyGodId) is { } theirs)
        {
            var moved = theirs.Adherence * _rules.RaidConversionPerSecond * seconds * strength;
            theirs.Adherence -= moved;
            Convert(defenders, war.GodId, moved, theirs);
            war.Converted += Whole(moved * defenders.Population, random);
        }

        defenders.Society.Safety = SimMath.Clamp01(defenders.Society.Safety - (0.05f * seconds));
        if (WarBook.Find(world, war.GodId, war.EnemyGodId) is { } godsWar)
        {
            godsWar.Add(war.GodId, _rules.RaidScore * seconds / 10f);
        }
    }

    /// <summary>A number of people: the whole part, and one more with the fraction's chance, so small raids still count.</summary>
    private static int Whole(float people, SimulationRandom random)
    {
        var whole = (int)MathF.Floor(people);
        return whole + (random.NextFloat() < people - whole ? 1 : 0);
    }

    /// <summary>Converted people join the attackers' god's following in the village.</summary>
    private static void Convert(SettlementState defenders, int godId, float share, GodBelief from)
    {
        if (BeliefModel.Find(defenders, godId) is { } belief)
        {
            belief.Adherence = SimMath.Clamp01(belief.Adherence + share);
        }
        else
        {
            defenders.Beliefs.Add(new GodBelief { GodId = godId, Adherence = share, Faith = from.Faith * 0.5f, Love = 0f, Fear = 0.3f, Doubt = 0f });
        }
    }

    /// <summary>The holy war ends; the settlement rests before another.</summary>
    private static void End(WorldState world, SettlementState attackers, HolyWarState war, bool won, List<WorldEvent> events)
    {
        attackers.Society.HolyWar = null;
        attackers.Society.HolyWarEndedTick = world.Tick;
        events.Add(new HolyWarEnded
        {
            Tick = world.Tick, SettlementId = attackers.Id, DefenderSettlementId = war.DefenderSettlementId, GodId = war.GodId,
            Killed = war.Killed, Converted = war.Converted, Won = won,
        });
    }
}
