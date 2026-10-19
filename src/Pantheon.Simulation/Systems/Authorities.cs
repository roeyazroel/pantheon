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
/// <param name="Domains">Domains taken up (ADR-021), or <c>null</c> where gods have none.</param>
/// <param name="Society">Settlements' priorities (ADR-023), or <c>null</c> where settlements have no society rules.</param>
/// <param name="Council">The Divine Council's motions and votes (ADR-025), or <c>null</c> without one.</param>
/// <param name="Conflict">Proxy acts and holy war stances (ADR-026), or <c>null</c> without them.</param>
public sealed record Authorities(MiracleCaster Miracles, CreatureAuthority Creatures, DiplomacyAuthority Diplomacy, MigrationAuthority Migration, Domains.DomainAuthority? Domains = null, Society.SocietyAuthority? Society = null, Politics.CouncilAuthority? Council = null, Conflict.ConflictAuthority? Conflict = null);
