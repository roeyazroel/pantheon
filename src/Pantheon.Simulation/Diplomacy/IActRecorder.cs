using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Diplomacy;

/// <summary>
/// Receives the acts that gods notice of each other where they happen: a miracle on a rival's
/// followers, a Creature's attack, a settlement won from a rival (ADR-012).
/// </summary>
public interface IActRecorder
{
    /// <summary>Records that <paramref name="actorGodId"/> did something <paramref name="observerGodId"/> notices.</summary>
    /// <param name="world">World state.</param>
    /// <param name="observerGodId">The god who notices.</param>
    /// <param name="actorGodId">The god who acted.</param>
    /// <param name="act">What it did.</param>
    /// <param name="settlementId">The settlement it concerned, if any.</param>
    void Record(WorldState world, int observerGodId, int actorGodId, DiplomaticAct act, int? settlementId);
}
