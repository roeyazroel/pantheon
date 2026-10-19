using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Tests.Support;

/// <summary>Rival worlds with pinned diplomacy rules, for diplomacy tests.</summary>
internal static class DiplomacyWorlds
{
    /// <summary>The politics shipped with Milestone 15, part 5 (ADR-025), pinned.</summary>
    public static Pantheon.Domain.Politics.PoliticsRules PoliticsRules { get; } = new()
    {
        MaxTreatySeconds = 1200f,
        MaxTribute = 60f,
        MaxPowerAsk = 500f,
        SecretLeakPerSecond = 0.002f,
        TreatyBetrayalShare = 0.6f,
        MaxFavours = 3f,
        AssistSeconds = 120f,
        BrokenPromiseTrustLoss = 0.2f,
        FirstCouncilSeconds = 300f,
        CouncilIntervalSeconds = 300f,
        CouncilCooldownSeconds = 180f,
        VoteSeconds = 60f,
        DecreeSeconds = 480f,
        CondemnTrustLoss = 0.15f,
        CondemnDoubt = 0.08f,
        DefianceTrustLoss = 0.15f,
        CoordinatedCostFactor = 0.5f,
        StruckSeconds = 180f,
        AbsorbShare = 0.4f,
    };

    /// <summary>Diplomacy rules with fixed values (the balance shipped with Milestone 6).</summary>
    public static DiplomacyRules Rules { get; } = new()
    {
        StepIntervalTicks = 10,
        StartingTrust = 0.5f,
        StartingRivalry = 0.2f,
        StartingSuspicion = 0.2f,
        DriftPerStep = 0.004f,
        MemoryFadePerStep = 0.004f,
        BetrayalFadePerStep = 0.0005f,
        MemoryCapacity = 16,
        ProposalSeconds = 60f,
        ProposalCooldownSeconds = 180f,
        StatusCooldownSeconds = 60f,
        HelpGratitude = 0.08f,
        CourtingRivalry = 0.03f,
        HarmTrustLoss = 0.08f,
        HarmRivalry = 0.1f,
        BetrayalTrustLoss = 0.5f,
        BetrayalReputationLoss = 0.2f,
        WarResistance = 0.6f,
        WarCreatureAggression = 0.6f,
        HostilityThreshold = 0.35f,
        Politics = PoliticsRules,
    };


    /// <summary>A player and an AI rival over settlements at the given x positions.</summary>
    /// <param name="positionsX">X positions of the settlements.</param>
    /// <returns>The world.</returns>
    public static WorldState Create(params float[] positionsX) => RivalWorlds.Create(GodPersonality.Opportunist, 500f, positionsX);

    /// <summary>A diplomacy authority over the pinned rules with its own ledger.</summary>
    /// <returns>The authority.</returns>
    public static DiplomacyAuthority Authority() => new(Rules, new DiplomacyLedger(Rules));

    /// <summary>Sets the status of a pair, as if it had begun long ago.</summary>
    /// <param name="world">World state.</param>
    /// <param name="status">Status.</param>
    public static void SetStatus(WorldState world, DiplomaticStatus status)
    {
        var stance = DiplomacyModel.EnsureStance(world, 1, RivalWorlds.RivalId);
        stance.Status = status;
        stance.SinceTick = 0;
        world.Tick = 10_000;
    }
}
