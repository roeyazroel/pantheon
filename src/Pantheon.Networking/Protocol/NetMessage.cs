using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;

namespace Pantheon.Networking.Protocol;

/// <summary>A message between a host and a player (spec §73, ADR-018). Each kind travels as JSON with a <c>Type</c> tag.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(Hello), "Hello")]
[JsonDerivedType(typeof(Welcome), "Welcome")]
[JsonDerivedType(typeof(Refused), "Refused")]
[JsonDerivedType(typeof(Snapshot), "Snapshot")]
[JsonDerivedType(typeof(EventBatch), "Events")]
[JsonDerivedType(typeof(PlayerList), "Players")]
[JsonDerivedType(typeof(CastIntent), "Cast")]
[JsonDerivedType(typeof(CreatureIntent), "Creature")]
[JsonDerivedType(typeof(DiplomacyIntent), "Diplomacy")]
[JsonDerivedType(typeof(RefugeeIntent), "Refugees")]
[JsonDerivedType(typeof(Verdict), "Verdict")]
[JsonDerivedType(typeof(Goodbye), "Goodbye")]
public abstract record NetMessage;

/// <summary>A player asks to join: the protocol version, a fingerprint of its game data, its name and, if it has one, the god it wants.</summary>
/// <param name="ProtocolVersion">The player's <see cref="NetworkProtocol.Version"/>.</param>
/// <param name="ContentFingerprint">A fingerprint of the player's game data; it must match the host's.</param>
/// <param name="PlayerName">The name others see.</param>
/// <param name="WantedGod">The name of the god it wants to play, or <c>null</c> for any.</param>
public sealed record Hello(int ProtocolVersion, string ContentFingerprint, string PlayerName, string? WantedGod) : NetMessage;

/// <summary>The host accepts a player and gives it a god; a <see cref="Snapshot"/> follows.</summary>
/// <param name="PlayerId">The player's peer id.</param>
/// <param name="GodId">The god it plays.</param>
public sealed record Welcome(int PlayerId, int GodId) : NetMessage;

/// <summary>The host turns a player away.</summary>
/// <param name="Reason">Why, in words.</param>
public sealed record Refused(string Reason) : NetMessage;

/// <summary>The host's whole world at a tick, with its clock (spec §95: on joining and then at intervals).</summary>
/// <param name="Tick">The world's tick.</param>
/// <param name="TimeScale">The host's simulation speed.</param>
/// <param name="Paused">Whether the host's clock is paused.</param>
/// <param name="World">The world in the save format.</param>
public sealed record Snapshot(long Tick, double TimeScale, bool Paused, string World) : NetMessage;

/// <summary>Events the host's world raised, in order, so players see alerts and effects (spec §91, §95).</summary>
/// <param name="Tick">The host's tick when they were sent.</param>
/// <param name="Events">The events.</param>
public sealed record EventBatch(long Tick, IReadOnlyList<EventEnvelope> Events) : NetMessage;

/// <summary>One world event: its type's name and its fields.</summary>
/// <param name="Kind">The event type's name, for example <c>MiracleCast</c>.</param>
/// <param name="Data">The event's fields.</param>
public sealed record EventEnvelope(string Kind, JsonElement Data);

/// <summary>Who plays which god now.</summary>
/// <param name="Seats">Every player, the host first.</param>
public sealed record PlayerList(IReadOnlyList<PlayerSeat> Seats) : NetMessage;

/// <summary>A player and its god.</summary>
/// <param name="PlayerId">The player's peer id (<see cref="NetworkProtocol.HostPeerId"/> for the host).</param>
/// <param name="Name">Its name.</param>
/// <param name="GodId">Its god.</param>
public sealed record PlayerSeat(int PlayerId, string Name, int GodId);

/// <summary>A player's intent to cast a miracle (spec §73's CastMiracleRequest); the host checks it.</summary>
/// <param name="RequestId">The player's number for the request, echoed in the <see cref="Verdict"/>.</param>
/// <param name="MiracleId">The miracle.</param>
/// <param name="Target">Where to cast it.</param>
public sealed record CastIntent(int RequestId, string MiracleId, GroundPosition Target) : NetMessage;

/// <summary>A player's intent towards its Creature: reward, punish or send it.</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="Kind">The command.</param>
/// <param name="Target">Where to go, for a go-to.</param>
public sealed record CreatureIntent(int RequestId, CreatureCommandKind Kind, GroundPosition Target) : NetMessage;

/// <summary>A player's diplomatic move towards another god.</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="OtherGodId">The other god.</param>
/// <param name="Kind">The move.</param>
public sealed record DiplomacyIntent(int RequestId, int OtherGodId, DiplomaticCommandKind Kind) : NetMessage;

/// <summary>A player's refugee policy.</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="Policy">The policy.</param>
public sealed record RefugeeIntent(int RequestId, RefugeePolicy Policy) : NetMessage;

/// <summary>The host's answer to an intent: whether it was carried out, and what happened or why not.</summary>
/// <param name="RequestId">The request it answers.</param>
/// <param name="Accepted">Whether the world changed.</param>
/// <param name="Text">What happened, or why not, as the player's status line.</param>
public sealed record Verdict(int RequestId, bool Accepted, string Text) : NetMessage;

/// <summary>A peer leaves the session: a player quitting, or the host ending it (spec §71).</summary>
/// <param name="Reason">Why, in words.</param>
public sealed record Goodbye(string Reason) : NetMessage;
