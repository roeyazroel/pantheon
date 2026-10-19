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
[JsonDerivedType(typeof(CouncilIntent), "Council")]
[JsonDerivedType(typeof(ProxyIntent), "Proxy")]
[JsonDerivedType(typeof(HolyWarIntent), "HolyWar")]
[JsonDerivedType(typeof(RefugeeIntent), "Refugees")]
[JsonDerivedType(typeof(DomainIntent), "Domain")]
[JsonDerivedType(typeof(PriorityIntent), "Priority")]
[JsonDerivedType(typeof(Verdict), "Verdict")]
[JsonDerivedType(typeof(Goodbye), "Goodbye")]
[JsonDerivedType(typeof(SectionUpdate), "Section")]
[JsonDerivedType(typeof(ViewFocus), "Focus")]
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
/// <param name="Veiled">Whether it is cast in secret (ADR-026).</param>
public sealed record CastIntent(int RequestId, string MiracleId, GroundPosition Target, bool Veiled = false) : NetMessage;

/// <summary>A player acts in secret through mortals against another god (ADR-026).</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="SettlementId">The settlement.</param>
/// <param name="TargetGodId">The god worked against.</param>
/// <param name="Kind">What it does.</param>
public sealed record ProxyIntent(int RequestId, int SettlementId, int TargetGodId, Pantheon.Domain.Conflict.ProxyKind Kind) : NetMessage;

/// <summary>A player speaks to its people's holy war (ADR-026).</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="SettlementId">The settlement waging it.</param>
/// <param name="Stance">Encourage, discourage or ignore.</param>
public sealed record HolyWarIntent(int RequestId, int SettlementId, Pantheon.Domain.Conflict.HolyWarStance Stance) : NetMessage;

/// <summary>A player's intent towards its Creature: reward, punish or send it.</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="Kind">The command.</param>
/// <param name="Target">Where to go, for a go-to.</param>
public sealed record CreatureIntent(int RequestId, CreatureCommandKind Kind, GroundPosition Target) : NetMessage;

/// <summary>A player's diplomatic move towards another god.</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="OtherGodId">The other god.</param>
/// <param name="Kind">The move.</param>
/// <param name="Terms">A treaty's terms, for a proposal (ADR-025).</param>
/// <param name="TreatyId">The treaty, to break it.</param>
/// <param name="Ask">What is asked, for a request.</param>
public sealed record DiplomacyIntent(int RequestId, int OtherGodId, DiplomaticCommandKind Kind, Pantheon.Domain.Politics.TreatyTerms? Terms = null, int? TreatyId = null, Pantheon.Domain.Politics.RequestAsk? Ask = null) : NetMessage;

/// <summary>A player tables a motion before the Divine Council, or votes on the one in session (ADR-025).</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="Motion">The motion to table; <c>null</c> to vote.</param>
/// <param name="Choice">The vote; <c>null</c> to table.</param>
public sealed record CouncilIntent(int RequestId, Pantheon.Domain.Politics.CouncilMotion? Motion, Pantheon.Domain.Politics.VoteChoice? Choice) : NetMessage;

/// <summary>A player's refugee policy.</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="Policy">The policy.</param>
public sealed record RefugeeIntent(int RequestId, RefugeePolicy Policy) : NetMessage;

/// <summary>A player takes up a domain for its god (ADR-021).</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="Domain">The domain.</param>
public sealed record DomainIntent(int RequestId, Pantheon.Domain.Gods.GodDomain Domain) : NetMessage;

/// <summary>A player asks one of its god's settlements to put something first (ADR-023).</summary>
/// <param name="RequestId">The player's number for the request.</param>
/// <param name="SettlementId">The settlement.</param>
/// <param name="Priority">The priority.</param>
public sealed record PriorityIntent(int RequestId, int SettlementId, Pantheon.Domain.Society.SettlementPriority Priority) : NetMessage;

/// <summary>The host's answer to an intent: whether it was carried out, and what happened or why not.</summary>
/// <param name="RequestId">The request it answers.</param>
/// <param name="Accepted">Whether the world changed.</param>
/// <param name="Text">What happened, or why not, as the player's status line.</param>
public sealed record Verdict(int RequestId, bool Accepted, string Text) : NetMessage;

/// <summary>A part of the world the host sends on its own schedule (spec §95–96, ADR-019).</summary>
public enum WorldSection
{
    /// <summary>The gods (Divine Power, rank, cooldowns, policies), with the world's tick, random state and winners: a medium rate.</summary>
    Gods,

    /// <summary>Where Creatures are and what they do now: the highest rate near the player, a lower one elsewhere.</summary>
    CreatureMotion,

    /// <summary>Every Creature whole (needs, personality, learning, memories, relations): a low rate.</summary>
    Creatures,

    /// <summary>The settlements (people, food, houses, beliefs): a low rate.</summary>
    Settlements,

    /// <summary>The regions' changing values (soil water, fire, scorch, flood, blessing, vegetation, fertility) that changed: a low rate.</summary>
    Regions,

    /// <summary>The weather cells and spell: a low rate.</summary>
    Weather,

    /// <summary>Earthquakes under way: a low rate.</summary>
    Disasters,

    /// <summary>Migration groups on the road: a medium rate.</summary>
    Migration,

    /// <summary>Relations, stances and proposals between gods: sent when they change.</summary>
    Diplomacy,

    /// <summary>The world's history (ADR-020): sent when it grows.</summary>
    History,

    /// <summary>The holy sites, their keepers and claims (ADR-021): the lowest rate.</summary>
    HolySites,

    /// <summary>The rivers' levels and the lakes (ADR-022): a low rate.</summary>
    Water,
}

/// <summary>A part of the host's world as it was at a tick (ADR-019), in the save format.</summary>
/// <param name="Tick">The host's tick.</param>
/// <param name="Section">Which part.</param>
/// <param name="Data">The part, as JSON in the save format.</param>
public sealed record SectionUpdate(long Tick, WorldSection Section, string Data) : NetMessage;

/// <summary>Where a player's camera looks, so the host sends what is near it more often (spec §95's interest management).</summary>
/// <param name="X">World X in metres.</param>
/// <param name="Z">World Z in metres.</param>
public sealed record ViewFocus(float X, float Z) : NetMessage;

/// <summary>A peer leaves the session: a player quitting, or the host ending it (spec §71).</summary>
/// <param name="Reason">Why, in words.</param>
public sealed record Goodbye(string Reason) : NetMessage;
