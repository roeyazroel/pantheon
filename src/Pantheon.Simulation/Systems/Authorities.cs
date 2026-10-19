using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Miracles;

namespace Pantheon.Simulation.Systems;

/// <summary>
/// The authorities that check and carry out every god's commands (spec §73): the same objects
/// serve the local player, the AI gods and, later, a multiplayer host.
/// </summary>
/// <param name="Miracles">Miracle casts.</param>
/// <param name="Creatures">Rewards, punishments and orders to Creatures.</param>
/// <param name="Diplomacy">Diplomatic commands.</param>
/// <param name="Migration">Refugee policies.</param>
public sealed record Authorities(MiracleCaster Miracles, CreatureAuthority Creatures, DiplomacyAuthority Diplomacy, MigrationAuthority Migration);
