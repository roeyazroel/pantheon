using System.Collections.Generic;
using Pantheon.Domain.Gods;

namespace Pantheon.Domain.Belief;

/// <summary>
/// Data-driven tuning of belief, worship, Divine Power and influence, loaded from
/// <c>game/Data/divine_rules.json</c>. The belief model steps once every
/// <see cref="StepIntervalTicks"/> ticks and every rate is per step (ADR-009).
/// </summary>
public sealed record DivineRules
{
    /// <summary>Ticks between belief steps; 10 ticks make one simulated second.</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Divine Power one worshipper of full intensity raises per step, at a shrine.</summary>
    public required float PowerPerWorshipperPerStep { get; init; }

    /// <summary>Worship intensity of believers who neither love nor fear their god.</summary>
    public required float BaseWorshipIntensity { get; init; }

    /// <summary>Extra worship intensity at full Love.</summary>
    public required float LoveWorshipIntensity { get; init; }

    /// <summary>Extra worship intensity at full Fear: frightened people worship hardest.</summary>
    public required float FearWorshipIntensity { get; init; }

    /// <summary>Religious infrastructure multiplier of a settlement's shrine.</summary>
    public required float ShrineInfrastructure { get; init; }

    /// <summary>Stored food per person at which a settlement feels secure.</summary>
    public required float ComfortFoodPerPerson { get; init; }

    /// <summary>How much drought counts as hardship (0..1): people fear for the harvest before the stores run out (ADR-013).</summary>
    public required float DroughtWorry { get; init; }

    /// <summary>How much danger (fire on the farmland, shaking ground) counts as hardship (0..1; ADR-014).</summary>
    public required float DangerWorry { get; init; }

    /// <summary>Fear of every god a struck settlement knows, per unit of the disaster's severity: the gods are angry.</summary>
    public required float DisasterFear { get; init; }

    /// <summary>Doubt about every god a struck settlement knows, per unit of the disaster's severity: why did they not protect us?</summary>
    public required float DisasterDoubt { get; init; }

    /// <summary>Faith gained per step by a settlement that wholly follows the god, closing that share of the gap to 1.</summary>
    public required float FaithFromWorship { get; init; }

    /// <summary>Share of Faith lost per step at full Doubt.</summary>
    public required float FaithErosionFromDoubt { get; init; }

    /// <summary>Share of Faith lost per step by people who do not follow the god: unbelievers forget what they saw.</summary>
    public required float FaithForgettingPerStep { get; init; }

    /// <summary>Love gained per step by followers in full comfort, closing that share of the gap to 1.</summary>
    public required float LoveFromComfort { get; init; }

    /// <summary>Share of Love that fades per step.</summary>
    public required float LoveDecayPerStep { get; init; }

    /// <summary>Share of Love lost per step by followers in full hardship: they expect protection.</summary>
    public required float LoveLossFromHardship { get; init; }

    /// <summary>Share of Fear that fades per step in full safety.</summary>
    public required float FearDecayPerStep { get; init; }

    /// <summary>Doubt that a god who shows no power settles towards.</summary>
    public required float SilenceDoubt { get; init; }

    /// <summary>Share of the gap to <see cref="SilenceDoubt"/> that Doubt closes per step.</summary>
    public required float DoubtDriftPerStep { get; init; }

    /// <summary>Doubt gained per step by followers in full hardship, closing that share of the gap to 1.</summary>
    public required float DoubtFromHardship { get; init; }

    /// <summary>Doubt of people who have just heard of a god.</summary>
    public required float NewBelieverDoubt { get; init; }

    /// <summary>Share of the people who follow no god that convert per step at full pull.</summary>
    public required float ConversionPerStep { get; init; }

    /// <summary>Pull of Faith alone, without Love or Fear.</summary>
    public required float BaseConversion { get; init; }

    /// <summary>Pull added by full Love.</summary>
    public required float LoveConversion { get; init; }

    /// <summary>Pull added by full Fear: fear converts fastest.</summary>
    public required float FearConversion { get; init; }

    /// <summary>Share of followers that leave per step at full push.</summary>
    public required float ApostasyPerStep { get; init; }

    /// <summary>Push of Doubt alone.</summary>
    public required float BaseApostasy { get; init; }

    /// <summary>Push added by full Fear: frightened followers abandon a god quickly.</summary>
    public required float FearFragility { get; init; }

    /// <summary>Share of the push that full Love holds back: loving followers stay.</summary>
    public required float LoveLoyalty { get; init; }

    /// <summary>
    /// Pull of following no god, against which the gods' pulls compete for the people who leave a
    /// god: they follow another god in proportion to its pull and stay without a god in proportion
    /// to this (spec §32: conversion between gods).
    /// </summary>
    public required float ApostateFreedom { get; init; }

    /// <summary>Faith below which a belief without followers is forgotten and removed.</summary>
    public required float ForgottenFaith { get; init; }

    /// <summary>Reach of a settlement's influence, in metres, before its followers are counted.</summary>
    public required float InfluenceBaseRadius { get; init; }

    /// <summary>Extra reach in metres per square root of followers.</summary>
    public required float InfluenceRadiusPerSqrtFollower { get; init; }

    /// <summary>Influence at the centre of a settlement that wholly follows the god with full Faith (capped at 1).</summary>
    public required float InfluenceStrength { get; init; }

    /// <summary>Influence of a god at its Creature (spec §10: Creature presence), fading to 0 at <see cref="CreatureInfluenceReach"/>.</summary>
    public required float CreatureInfluenceStrength { get; init; }

    /// <summary>Distance in metres over which the influence of a god's Creature fades.</summary>
    public required float CreatureInfluenceReach { get; init; }

    /// <summary>Distance in metres within which people see a miracle and change their belief.</summary>
    public required float WitnessRadius { get; init; }

    /// <summary>Radius in metres of a settlement's footprint: a miracle reaching it touches the settlement.</summary>
    public required float SettlementRadius { get; init; }

    /// <summary>Extra cost share of a miracle cast with no influence; costs scale by <c>1 + factor × (1 − influence)</c>.</summary>
    public required float WeakInfluenceCostFactor { get; init; }

    /// <summary>Doubt that a rival god's miracle adds to the followers of every other god who see it (scaled by closeness).</summary>
    public required float RivalMiracleDoubt { get; init; }

    /// <summary>Extra cost share per unit by which the strongest rival's influence at the target exceeds the caster's (spec §10).</summary>
    public required float ContestedCostFactor { get; init; }

    /// <summary>Share of the world's followers a god must hold to dominate (spec §115: 60%).</summary>
    public required float VictoryShare { get; init; }

    /// <summary>Simulated seconds of unbroken dominance that win the game (spec §115: ten minutes).</summary>
    public required float VictorySeconds { get; init; }

    /// <summary>Share of a miracle's Love that people get even when they did not need it.</summary>
    public required float GratitudeFloor { get; init; }

    /// <summary>Share of a region's blessing that fades per step.</summary>
    public required float BlessingDecayPerStep { get; init; }

    /// <summary>Followers needed for each rank above <see cref="DivineRank.MinorSpirit"/>, in rank order.</summary>
    public required IReadOnlyList<RankThreshold> Ranks { get; init; }
}

/// <summary>Followers a god needs to reach a rank.</summary>
public sealed record RankThreshold
{
    /// <summary>The rank.</summary>
    public required DivineRank Rank { get; init; }

    /// <summary>Followers needed.</summary>
    public required int Followers { get; init; }
}
