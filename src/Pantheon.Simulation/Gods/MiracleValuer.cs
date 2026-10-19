using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Miracles;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// What a miracle is worth to an AI god in one settlement it reaches, by the goals its personality
/// weighs (spec §48, ADR-011, ADR-021). Every factor that counts is noted as a reason.
/// </summary>
internal sealed class MiracleValuer
{
    // A partner's settlement counts as struck by disaster for this many ticks (three minutes).
    private const long StruckTicks = 1800;

    private const float MaxPopulationFactor = 2f;

    // A village whose own region lies this low is within a wave's reach.
    private const float FloodHeightValued = 10f;

    // Danger to its own people from which the god will still the whole world (ADR-030).
    private const float StillDanger = 0.5f;

    private readonly GodAiRules _rules;
    private readonly MiracleCaster _caster;

    /// <summary>Creates the valuer.</summary>
    public MiracleValuer(GodAiRules rules, MiracleCaster caster)
    {
        _rules = rules;
        _caster = caster;
    }

    /// <summary>What a miracle cast at a target is worth to the god in one settlement it reaches.</summary>
    public float Value(WorldState world, GodState god, GodProfile profile, MiracleDefinition miracle, SettlementState settlement, GroundPosition target, List<string> reasons)
    {
        var own = BeliefModel.Find(settlement, god.Id);
        var mine = own?.Adherence ?? 0f;
        var rivals = 0f;
        var allies = 0f;
        var rivalDoubt = 0f;
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.GodId == god.Id)
            {
                continue;
            }

            if (Politics.TreatyModel.Assists(world, god.Id, belief.GodId) && Politics.CouncilModel.Struck(world, settlement, world.Tick - StruckTicks))
            {
                // A pact to answer disasters among the partner's people (ADR-025): their need counts half as its own.
                mine += 0.5f * belief.Adherence;
                allies += 0.5f * belief.Adherence;
                continue;
            }

            if (DiplomacyModel.AreAllied(world, god.Id, belief.GodId))
            {
                // An ally's followers are not to be courted or frightened.
                allies += belief.Adherence;
                continue;
            }

            var enmity = DiplomacyModel.AreAtWar(world, god.Id, belief.GodId) ? 1.5f : 1f;
            rivals += belief.Adherence * enmity;
            rivalDoubt = Math.Max(rivalDoubt, belief.Adherence > 0f ? belief.Doubt : 0f);
        }

        var free = Math.Max(0f, 1f - mine - rivals - allies);
        var conditions = _caster.AssessConditions(world, settlement);
        var size = Math.Min(MaxPopulationFactor, MathF.Sqrt(settlement.Population / _rules.ReferencePopulation));
        var opportunity = 1f + (profile.Opportunism * Math.Max(conditions.Hardship, rivalDoubt));
        var value = 0f;
        foreach (var effect in miracle.Effects)
        {
            switch (effect.Kind)
            {
                case MiracleEffectKind.Rain:
                case MiracleEffectKind.Redirect:
                    // Rain is worth calling only on parched land: every goal scales with how far the drought is past RainDrought.
                    var drought = Math.Max(0f, conditions.WaterNeed - _rules.RainDrought) / (1f - _rules.RainDrought);
                    value += Note(reasons, settlement, "protect", profile.Protect * mine * drought * size);
                    value += Note(reasons, settlement, "defend", profile.Protect * mine * Math.Min(1f, 2f * rivals) * drought * size);
                    value += Note(reasons, settlement, "convert", profile.Convert * (free + rivals) * drought * opportunity * size);
                    break;
                case MiracleEffectKind.Water:
                    // Water is worth giving only to the thirsty or the parched: every goal scales with the need (ADR-022).
                    var thirst = BeliefModel.NeedFor(conditions, miracle.Gratitude);
                    value += Note(reasons, settlement, "protect", profile.Protect * mine * thirst * size);
                    value += Note(reasons, settlement, "convert", profile.Convert * (free + rivals) * thirst * opportunity * size);
                    break;
                case MiracleEffectKind.AddFood:
                case MiracleEffectKind.Heal:
                    var need = BeliefModel.NeedFor(conditions, miracle.Gratitude);
                    value += Note(reasons, settlement, "protect", profile.Protect * mine * need * size);
                    value += Note(reasons, settlement, "defend", profile.Protect * mine * Math.Min(1f, 2f * rivals) * size);
                    value += Note(reasons, settlement, "convert", profile.Convert * (free + rivals) * (0.3f + (0.7f * need)) * opportunity * size);
                    break;
                case MiracleEffectKind.Bless:
                    value += Note(reasons, settlement, "bless", profile.Bless * (mine + (0.5f * (1f - mine))) * (0.4f + (0.6f * conditions.FoodNeed)) * size);
                    break;
                case MiracleEffectKind.Regrow:
                    var harm = Math.Max(Math.Max(conditions.DangerNeed, conditions.WaterNeed), 0.5f * conditions.FoodNeed);
                    value += Note(reasons, settlement, "regrow", profile.Bless * (mine + (0.5f * (1f - mine))) * harm * size);
                    break;
                case MiracleEffectKind.Populate:
                    var room = SimMath.Clamp01(4f * (settlement.Housing - settlement.Population) / Math.Max(1f, settlement.Housing));
                    // Newcomers share the village's beliefs: a god grows its own people, and courts the others a little by the gift.
                    value += Note(reasons, settlement, "grow", ((profile.Protect * mine) + (0.2f * profile.Convert * (free + rivals))) * room * size);
                    break;
                case MiracleEffectKind.Smite:
                    // Striking a village also strikes its own followers there; it never strikes an ally's.
                    if (allies <= 0.05f)
                    {
                        value += Note(reasons, settlement, "intimidate", profile.Intimidate * (rivals + (0.3f * free)) * opportunity * (1f - mine) * size);
                    }

                    break;
                case MiracleEffectKind.Terrify:
                    value += Note(reasons, settlement, "dread", profile.Intimidate * rivals * opportunity * (1f - (0.5f * mine)) * size);
                    break;
                case MiracleEffectKind.Ignite:
                case MiracleEffectKind.Quake:
                case MiracleEffectKind.Flood:
                    value += ValueDisaster(effect.Kind, reasons, world, settlement, profile, mine, rivals, free, allies, opportunity, size);
                    break;
                case MiracleEffectKind.Judge:
                    value += ValueJudgement(world, god, profile, settlement, opportunity, size, reasons);
                    break;
                case MiracleEffectKind.Shield:
                    value += Note(reasons, settlement, "shield", profile.Protect * mine * conditions.DangerNeed * size);
                    break;
                case MiracleEffectKind.Sanctuary:
                    // Worth it only where its people are being drawn away: rivals present and its own people doubting.
                    var held = mine >= BeliefModel.MajorityShare ? 1f : 0.3f;
                    value += Note(reasons, settlement, "sanctuary", 0.5f * profile.Protect * mine * Math.Min(1f, 2f * rivals) * (own?.Doubt ?? 0f) * held * size);
                    break;
                case MiracleEffectKind.Wound:
                    value += ValueWound(world, god, profile, miracle, target, reasons);
                    break;
                case MiracleEffectKind.Teach:
                    value += MiracleEffects.FindOwnCreature(world, god.Id, target, miracle.Radius) is not null ? Note(reasons, settlement, "teach", 0.6f * profile.SendCreature) : 0f;
                    break;
                case MiracleEffectKind.Discord:
                    value += ValueDiscord(world, god, profile, settlement, reasons);
                    break;
                case MiracleEffectKind.Hallow:
                    // A lasting blessing on its own lands not yet hallowed, worth more the hungrier they are (ADR-030).
                    var unhallowed = 1f - SimMath.Clamp01(world.Regions.Cells[settlement.RegionId].Hallowed / Math.Max(0.01f, effect.Amount));
                    value += Note(reasons, settlement, "hallow", profile.Bless * mine * unhallowed * (0.5f + (0.5f * conditions.FoodNeed)) * size);
                    break;
                case MiracleEffectKind.Still:
                    // Only a real disaster among its own people is worth stilling the whole world for.
                    value += conditions.DangerNeed >= StillDanger ? Note(reasons, settlement, "still", profile.Protect * mine * conditions.DangerNeed * size) : 0f;
                    break;
                case MiracleEffectKind.Awaken:
                case MiracleEffectKind.Raise:
                case MiracleEffectKind.Split:
                case MiracleEffectKind.Divert:
                    // The AI never wakes the volcano or reshapes the land: it aims at villages, not at the sea, mountains or rivers (ADR-045).
                    break;
                case MiracleEffectKind.Anoint:
                    // A prophet from its own people goes out to win the others (ADR-032).
                    value += Note(reasons, settlement, "anoint", 0.5f * profile.Convert * mine * size);
                    break;
            }
        }

        return value;
    }

    /// <summary>
    /// What setting a village alight or shaking it is worth (ADR-014): fear among rivals' followers
    /// (a fire only where the land will burn), less the harm to its own followers, which counts
    /// against it. It never strikes an ally's village.
    /// </summary>
    private float ValueDisaster(MiracleEffectKind kind, List<string> reasons, WorldState world, SettlementState settlement, GodProfile profile, float mine, float rivals, float free, float allies, float opportunity, float size)
    {
        if (allies > 0.05f)
        {
            return 0f;
        }

        var reach = kind switch
        {
            MiracleEffectKind.Ignite => _caster.FlammabilityAt(world, settlement),
            MiracleEffectKind.Flood => world.Regions.Cells[settlement.RegionId].Elevation < FloodHeightValued ? 1f : 0f,
            _ => 1f,
        };
        var fear = Note(reasons, settlement, "intimidate", profile.Intimidate * (rivals + (0.3f * free)) * opportunity * (1f - mine) * reach * size);
        var harm = profile.Protect * mine * size;
        if (harm > 0.01f)
        {
            reasons.Add(Format("harms own {0} -{1:0.00}", settlement.Name, harm));
        }

        return fear - harm;
    }

    /// <summary>
    /// Judging a settlement: worth the share of its people who follow gods that wronged this one, by
    /// its appetite for intimidation and its grudges (ADR-022). Only the guilty suffer, so its own
    /// followers there do not count against it.
    /// </summary>
    private float ValueJudgement(WorldState world, GodState god, GodProfile profile, SettlementState settlement, float opportunity, float size, List<string> reasons)
    {
        var guilty = 0f;
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence > 0.05f && _caster.IsGuilty(world, god.Id, belief.GodId))
            {
                guilty += belief.Adherence;
            }
        }

        return Note(reasons, settlement, "judge", Math.Max(profile.Intimidate, profile.Grudge) * guilty * opportunity * size);
    }

    /// <summary>Wounding rival Creatures: worth more against an enemy's, by the god's appetite for war (ADR-021).</summary>
    private static float ValueWound(WorldState world, GodState god, GodProfile profile, MiracleDefinition miracle, GroundPosition target, List<string> reasons)
    {
        var value = 0f;
        foreach (var creature in MiracleEffects.FindRivalCreatures(world, god.Id, target, miracle.Radius))
        {
            var worth = 0.8f * profile.Aggression * (DiplomacyModel.AreAtWar(world, god.Id, creature.GodId) ? 1f : 0.25f);
            if (worth > 0.005f)
            {
                reasons.Add(Format("wound {0} {1:0.00}", creature.Name, worth));
            }

            value += worth;
        }

        return value;
    }

    /// <summary>Turning the gods followed in a settlement against each other: worth most where two of them are allied (ADR-021).</summary>
    private static float ValueDiscord(WorldState world, GodState god, GodProfile profile, SettlementState settlement, List<string> reasons)
    {
        var gods = new List<int>();
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.GodId != god.Id && belief.Adherence > 0.05f && !DiplomacyModel.AreAllied(world, god.Id, belief.GodId))
            {
                gods.Add(belief.GodId);
            }
        }

        var allied = false;
        for (var i = 0; i < gods.Count; i++)
        {
            for (var j = i + 1; j < gods.Count; j++)
            {
                allied |= DiplomacyModel.AreAllied(world, gods[i], gods[j]);
            }
        }

        return gods.Count < 2 ? 0f : Note(reasons, settlement, "discord", profile.Opportunism * (allied ? 1.2f : 0.25f));
    }

    /// <summary>Records a factor when it counts.</summary>
    internal static float Note(List<string> reasons, SettlementState settlement, string goal, float value)
    {
        if (value > 0.005f)
        {
            reasons.Add(Format("{0} {1} {2:0.00}", goal, settlement.Name, value));
        }

        return value;
    }

    /// <summary>Invariant-culture string formatting.</summary>
    internal static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
