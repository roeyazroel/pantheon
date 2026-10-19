using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Tests.Support;

/// <summary>Brings gods to know each other (ADR-036), for tests of what they do once met.</summary>
internal static class Meetings
{
    /// <summary>Every god meets every other, as at a Divine Council.</summary>
    /// <param name="world">World state.</param>
    /// <returns>The world.</returns>
    public static WorldState All(WorldState world)
    {
        foreach (var god in world.Gods)
        {
            foreach (var other in world.Gods)
            {
                Acquaintance.Introduce(world, god.Id, other.Id, MeetingWay.Council, null, []);
            }
        }

        return world;
    }
}
