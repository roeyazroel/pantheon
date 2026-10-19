using System;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Diplomacy;

/// <summary>
/// One diplomacy step (spec §37–40, ADR-012):
/// <list type="bullet">
/// <item>relations drift back towards their resting values;</item>
/// <item>memories fade, betrayal slowest;</item>
/// <item>fear and respect follow the other god's following and rank;</item>
/// <item>proposals lapse;</item>
/// <item>pairs that are neither allied nor at war warm to friendly or sour to hostile as both
/// sides' feelings dictate;</item>
/// <item>gods that crossed paths meet (ADR-036).</item>
/// </list>
/// </summary>
public sealed class DiplomacySystem : ISimulationSystem
{
    private readonly DiplomacyRules _rules;

    /// <summary>Creates the system with validated rules.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    public DiplomacySystem(DiplomacyRules rules)
    {
        DiplomacyRulesValidator.Validate(rules);
        _rules = rules;
    }

    /// <summary>How warm a god feels towards another, from -1 (hostile) to 1 (friendly).</summary>
    /// <param name="relation">The relation.</param>
    /// <returns>Warmth.</returns>
    public static float Warmth(GodRelation relation)
    {
        ArgumentNullException.ThrowIfNull(relation);
        return Math.Clamp(((relation.Trust - 0.5f) * 2f) + relation.Gratitude - relation.Rivalry - relation.Suspicion - (0.2f * DiplomacyModel.Grudge(relation)), -1f, 1f);
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        foreach (var relation in world.Diplomacy.Relations)
        {
            Drift(relation);
            UpdateStanding(world, relation);
        }

        world.Diplomacy.Proposals.RemoveAll(p => p.ExpiresAtTick <= world.Tick);
        foreach (var a in world.Gods)
        {
            foreach (var b in world.Gods)
            {
                if (a.Id < b.Id)
                {
                    Settle(world, a.Id, b.Id);
                }
            }
        }

        Acquaintance.Step(_rules.Acquaintance, world, context.Events);
    }

    /// <summary>Feelings drift back to rest and memories fade.</summary>
    private void Drift(GodRelation relation)
    {
        var d = _rules.DriftPerStep;
        relation.Trust += d * (_rules.StartingTrust - relation.Trust);
        relation.Rivalry += d * (_rules.StartingRivalry - relation.Rivalry);
        relation.Suspicion += d * (_rules.StartingSuspicion - relation.Suspicion);
        relation.Gratitude -= d * relation.Gratitude;
        foreach (var memory in relation.Memories)
        {
            memory.Weight *= 1f - (memory.Act == DiplomaticAct.Betrayed ? _rules.BetrayalFadePerStep : _rules.MemoryFadePerStep);
        }

        relation.Memories.RemoveAll(m => m.Weight < 0.01f);
    }

    /// <summary>Fear follows the other god's share of the two gods' followers; respect follows its rank.</summary>
    private static void UpdateStanding(WorldState world, GodRelation relation)
    {
        var mine = GodStanding.CountFollowers(world, relation.ObserverGodId);
        var theirs = GodStanding.CountFollowers(world, relation.SubjectGodId);
        var total = mine + theirs;
        relation.Fear = total > 0 ? SimMath.Clamp01(((float)theirs / total * 2f) - 1f) : 0f;
        relation.Respect = GodStanding.Find(world, relation.SubjectGodId) is { } subject ? SimMath.Clamp01((int)subject.Rank / 5f) : 0f;
    }

    /// <summary>Warms a pair to friendly or sours it to hostile, unless they are allied or at war.</summary>
    private void Settle(WorldState world, int a, int b)
    {
        var stance = DiplomacyModel.FindStance(world, a, b);
        if (stance?.Status is DiplomaticStatus.Alliance or DiplomaticStatus.War)
        {
            return;
        }

        var ab = DiplomacyModel.FindRelation(world, a, b);
        var ba = DiplomacyModel.FindRelation(world, b, a);
        if (ab is null && ba is null)
        {
            return;
        }

        // Friendship needs both sides warm; hostility needs only one side cold.
        var coolest = Math.Min(ab is null ? 0f : Warmth(ab), ba is null ? 0f : Warmth(ba));
        var status = coolest <= -_rules.HostilityThreshold ? DiplomaticStatus.Hostile
            : coolest >= _rules.HostilityThreshold ? DiplomaticStatus.Friendly
            : DiplomaticStatus.Neutral;
        stance ??= DiplomacyModel.EnsureStance(world, a, b);
        if (stance.Status != status)
        {
            stance.Status = status;
            stance.SinceTick = world.Tick;
        }
    }
}
