using Pantheon.Domain.World;

namespace Pantheon.Domain.Gods;

/// <summary>
/// A player's or AI's intent to cast a miracle (spec §73). The authority validates it against the
/// world before anything changes; the issuer never sets Divine Power, belief or population itself.
/// </summary>
/// <param name="GodId">The casting god.</param>
/// <param name="MiracleId">Id of the miracle (<c>miracles.json</c>).</param>
/// <param name="Target">Ground position the miracle is aimed at.</param>
public sealed record CastMiracleRequest(int GodId, string MiracleId, GroundPosition Target);
