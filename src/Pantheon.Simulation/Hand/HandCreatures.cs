using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Hand;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Hand;

/// <summary>A god lifting its own Creature, and setting it down or throwing it (spec §55, ADR-030).</summary>
internal sealed class HandCreatures(HandRules rules)
{
    // Thrown, a Creature keeps at least this much health and resents it a little.
    private const float LeastHealth = 0.05f;
    private const float ThrownLoyalty = 0.05f;

    /// <summary>What lifting the god's Creature at a point came to.</summary>
    public enum Lifting
    {
        /// <summary>The Creature is not within reach, or is banished.</summary>
        NotNear,

        /// <summary>The Creature is fighting and cannot be lifted.</summary>
        Fighting,

        /// <summary>The hand holds it now.</summary>
        Lifted,
    }

    /// <summary>Lifts the god's Creature when it stands within reach of the point, and nearer than anything else the hand could lift.</summary>
    public Lifting Lift(WorldState world, GodState god, GroundPosition target, float reach)
    {
        if (world.Creatures.Find(c => c.GodId == god.Id) is not { } creature || creature.Activity.Phase == CreaturePhase.Banished)
        {
            return Lifting.NotNear;
        }

        var (dx, dz) = (creature.Position.X - target.X, creature.Position.Z - target.Z);
        var within = System.Math.Min(reach, rules.Reach);
        if ((dx * dx) + (dz * dz) > within * within)
        {
            return Lifting.NotNear;
        }

        return creature.Activity.Action == CreatureAction.Fight && creature.Activity.Phase == CreaturePhase.Performing ? Lifting.Fighting : Lifting.Lifted;
    }

    /// <summary>Sets the god's Creature down at a point, where it decides afresh; thrown, it is hurt and resents it.</summary>
    public void Place(WorldState world, GodState god, GroundPosition target, bool thrown)
    {
        if (world.Creatures.Find(c => c.GodId == god.Id) is not { } creature)
        {
            return;
        }

        creature.Position = target;
        var activity = creature.Activity;
        (activity.Phase, activity.TicksLeft, activity.Target, activity.TargetSettlementId, activity.TargetCreatureId) = (CreaturePhase.Deciding, 0, target, null, null);
        activity.Path.Clear();
        if (thrown)
        {
            creature.Health = System.Math.Max(LeastHealth, creature.Health - rules.ThrownCreatureHealth);
            creature.Loyalty = SimMath.Clamp01(creature.Loyalty - ThrownLoyalty);
        }
    }
}
