using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Conflict;

/// <summary>Why the conflict authority refuses a command, or <see cref="None"/> when it is carried out.</summary>
public enum ConflictRejection
{
    /// <summary>The command was carried out.</summary>
    None,

    /// <summary>A god does not exist, or a god acted against itself.</summary>
    UnknownGod,

    /// <summary>The issuer does not control the god.</summary>
    NotAuthorized,

    /// <summary>There is no such settlement, or no one lives there.</summary>
    UnknownSettlement,

    /// <summary>The god worked against has no following there.</summary>
    NoFollowers,

    /// <summary>The acting god's influence there is too weak.</summary>
    OutsideInfluence,

    /// <summary>The god lacks the Divine Power.</summary>
    NotEnoughPower,

    /// <summary>The god acted through mortals too recently.</summary>
    TooSoon,

    /// <summary>The settlement wages no holy war for the god.</summary>
    NotItsWar,
}

/// <summary>A god's word to its people's holy war (spec §46).</summary>
/// <param name="GodId">The god.</param>
/// <param name="SettlementId">The settlement waging the holy war.</param>
/// <param name="Stance">Encourage, discourage or ignore.</param>
public sealed record HolyWarCommand(int GodId, int SettlementId, HolyWarStance Stance);

/// <summary>
/// The authority for proxy acts and holy wars (spec §45–46, §73, ADR-026). Inciting rebels and
/// spreading propaganda are covert: the god whose people they work on may trace them, by what it
/// knows of the author. A god may encourage or discourage its people's holy war; discouraging costs
/// their love, for mortals are not puppets.
/// </summary>
public sealed class ConflictAuthority
{
    /// <summary>The cooldown entry proxy acts keep in a god's cooldowns.</summary>
    public const string ProxyCooldown = "proxy";

    private readonly DivineRules _divine;
    private readonly ConflictRules _rules;
    private readonly IActRecorder? _ledger;

    /// <summary>Creates the authority.</summary>
    /// <param name="divine">Divine rules, for influence.</param>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="ledger">Where exposed acts are recorded; <c>null</c> for none.</param>
    public ConflictAuthority(DivineRules divine, ConflictRules rules, IActRecorder? ledger)
    {
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(rules);
        _divine = divine;
        _rules = rules;
        _ledger = ledger;
    }

    /// <summary>Whether a proxy act would be carried out, without changing anything.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The act.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host.</param>
    /// <returns>Why not, or <see cref="ConflictRejection.None"/>.</returns>
    public ConflictRejection Check(WorldState world, ProxyCommand command, int? issuerGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        if (GodStanding.Find(world, command.GodId) is not { } god || GodStanding.Find(world, command.TargetGodId) is null || command.GodId == command.TargetGodId)
        {
            return ConflictRejection.UnknownGod;
        }

        if (issuerGodId is { } issuer && issuer != command.GodId)
        {
            return ConflictRejection.NotAuthorized;
        }

        if (world.Settlements.Find(s => s.Id == command.SettlementId) is not { Population: > 0 } settlement)
        {
            return ConflictRejection.UnknownSettlement;
        }

        return BeliefModel.Find(settlement, command.TargetGodId) is not { Adherence: >= 0.05f } ? ConflictRejection.NoFollowers
            : InfluenceField.Build(_divine, world, god.Id).At(settlement.Position) < _rules.ProxyInfluence ? ConflictRejection.OutsideInfluence
            : god.Cooldowns.Exists(c => c.MiracleId == ProxyCooldown && c.ReadyAtTick > world.Tick) ? ConflictRejection.TooSoon
            : god.DivinePower < Cost(command.Kind) ? ConflictRejection.NotEnoughPower
            : ConflictRejection.None;
    }

    /// <summary>Checks a proxy act and carries it out.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The act.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host.</param>
    /// <param name="events">Receives <see cref="ProxyActed"/>, and <see cref="CovertActExposed"/> when traced.</param>
    /// <returns>Why it was refused, or <see cref="ConflictRejection.None"/>.</returns>
    public ConflictRejection Execute(WorldState world, ProxyCommand command, int? issuerGodId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var rejection = Check(world, command, issuerGodId);
        if (rejection != ConflictRejection.None)
        {
            return rejection;
        }

        var god = GodStanding.Find(world, command.GodId)!;
        var settlement = world.Settlements.Find(s => s.Id == command.SettlementId)!;
        var belief = BeliefModel.Find(settlement, command.TargetGodId)!;
        god.DivinePower -= Cost(command.Kind);
        StartCooldown(world, god);
        Work(belief, command.Kind);
        if (command.Kind == ProxyKind.Incite)
        {
            // Rebels need an unsettled village (spec §45: support rebels).
            settlement.Society.Stability = SimMath.Clamp01(settlement.Society.Stability - (_rules.InciteResentment / 2f));
        }
        events.Add(new ProxyActed { Tick = world.Tick, GodId = god.Id, SettlementId = settlement.Id, TargetGodId = command.TargetGodId, Kind = command.Kind });
        if (Covert.IsExposed(_rules, world, god.Id, command.TargetGodId, command.Kind.ToString()))
        {
            Covert.Expose(_ledger, world, god.Id, command.TargetGodId, command.Kind.ToString(), settlement.Id, events);
        }

        return ConflictRejection.None;
    }

    /// <summary>Whether a god may speak to a holy war, without changing anything.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The command.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host.</param>
    /// <returns>Why not, or <see cref="ConflictRejection.None"/>.</returns>
    public ConflictRejection Check(WorldState world, HolyWarCommand command, int? issuerGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        return GodStanding.Find(world, command.GodId) is null ? ConflictRejection.UnknownGod
            : issuerGodId is { } issuer && issuer != command.GodId ? ConflictRejection.NotAuthorized
            : world.Settlements.Find(s => s.Id == command.SettlementId) is not { } settlement ? ConflictRejection.UnknownSettlement
            : settlement.Society.HolyWar?.GodId != command.GodId || !Enum.IsDefined(command.Stance) ? ConflictRejection.NotItsWar
            : ConflictRejection.None;
    }

    /// <summary>Sets the god's word to the holy war; discouraging it costs the attackers' love.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The command.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host.</param>
    /// <returns>Why it was refused, or <see cref="ConflictRejection.None"/>.</returns>
    public ConflictRejection Execute(WorldState world, HolyWarCommand command, int? issuerGodId)
    {
        var rejection = Check(world, command, issuerGodId);
        if (rejection != ConflictRejection.None)
        {
            return rejection;
        }

        var settlement = world.Settlements.Find(s => s.Id == command.SettlementId)!;
        var war = settlement.Society.HolyWar!;
        if (command.Stance == HolyWarStance.Discourage && war.Stance != HolyWarStance.Discourage && BeliefModel.Find(settlement, command.GodId) is { } belief)
        {
            belief.Love = SimMath.Clamp01(belief.Love - _rules.DiscourageLoveLoss);
        }

        war.Stance = command.Stance;
        return ConflictRejection.None;
    }

    /// <summary>The Divine Power a proxy act costs.</summary>
    /// <param name="kind">The act.</param>
    /// <returns>The cost.</returns>
    public float Cost(ProxyKind kind) => kind switch
    {
        ProxyKind.Incite => _rules.IncitePower,
        ProxyKind.Propaganda => _rules.PropagandaPower,
    };

    /// <summary>What the act does to the followers of the god worked against.</summary>
    private void Work(GodBelief belief, ProxyKind kind)
    {
        switch (kind)
        {
            case ProxyKind.Incite:
                belief.Resentment = BeliefModel.ApplyChange(belief.Resentment, _rules.InciteResentment);
                belief.Fear = BeliefModel.ApplyChange(belief.Fear, _rules.InciteResentment / 2f);
                break;
            case ProxyKind.Propaganda:
                belief.Doubt = BeliefModel.ApplyChange(belief.Doubt, _rules.PropagandaDoubt);
                break;
        }
    }

    /// <summary>Starts the cooldown between proxy acts.</summary>
    private void StartCooldown(WorldState world, GodState god)
    {
        var ready = world.Tick + (long)MathF.Round(_rules.ProxyCooldownSeconds * SimulationTime.TicksPerSecond);
        if (god.Cooldowns.Find(c => c.MiracleId == ProxyCooldown) is { } cooldown)
        {
            cooldown.ReadyAtTick = ready;
        }
        else
        {
            god.Cooldowns.Add(new MiracleCooldown { MiracleId = ProxyCooldown, ReadyAtTick = ready });
        }
    }
}
