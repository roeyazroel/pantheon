using System;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Gods;

namespace Pantheon.Game.Angel;

/// <summary>Where the angel stands between kindness and harm (ADR-074), from the lightest to the darkest, with the torn angel between.</summary>
public enum AngelStage
{
    /// <summary>The god's kindness far outweighs its harm: white and gold, the halo whole.</summary>
    Radiant,

    /// <summary>More help than harm.</summary>
    Kind,

    /// <summary>The god has done little yet, or as little of either: a pale, silver angel, still forming.</summary>
    Unformed,

    /// <summary>Much help and much harm: one wing light, one dark.</summary>
    Torn,

    /// <summary>More harm than help.</summary>
    Stern,

    /// <summary>Harm far outweighs help: ash wings, a broken burning halo, embers for eyes.</summary>
    Fallen,
}

/// <summary>How the angel speaks: its voice follows its stage (ADR-074).</summary>
public enum AngelTone
{
    /// <summary>A radiant or kind angel: warm, urging mercy and care.</summary>
    Light,

    /// <summary>An unformed angel: curious, even, waiting to see what its god will be.</summary>
    Silver,

    /// <summary>A torn angel: two voices in one, offering both ways.</summary>
    Torn,

    /// <summary>A stern or fallen angel: cold, urging fear and domination.</summary>
    Dark,
}

/// <summary>Marks the god's traits leave on the angel (ADR-074).</summary>
[Flags]
public enum AngelMarks
{
    /// <summary>No marks.</summary>
    None = 0,

    /// <summary>Vengeful: a burning blade in its hand.</summary>
    Blade = 1,

    /// <summary>Deceptive: a veil over its face.</summary>
    Veil = 2,

    /// <summary>Cruel: chains about its wrists.</summary>
    Chains = 4,

    /// <summary>Ambitious: a crown of thorns.</summary>
    Crown = 8,

    /// <summary>Chaotic: cracks of fire in its skin.</summary>
    Cracks = 16,

    /// <summary>Generous: threads of gold in its robe.</summary>
    Gold = 32,

    /// <summary>Merciful: light in its open hands.</summary>
    OpenHands = 64,

    /// <summary>Honest: clear, shining eyes.</summary>
    ClearEyes = 128,
}

/// <summary>
/// How the player's angel looks (ADR-074), read from what the world remembers of its god (spec §3.2,
/// §11): never a score the rules keep, only a reading of the god's conduct and traits.
/// </summary>
/// <param name="Lean">From −1 (harm only) to 1 (kindness only), times how formed the angel is.</param>
/// <param name="Formed">How much the god has done, from 0 (nothing yet) towards 1.</param>
/// <param name="Conflict">How torn it is: high when the god has done much of both (0..1).</param>
/// <param name="Stage">Its stage.</param>
/// <param name="Marks">The marks of the god's traits.</param>
public readonly record struct AngelLook(float Lean, float Formed, float Conflict, AngelStage Stage, AngelMarks Marks)
{
    /// <summary>A newborn angel: nothing done yet.</summary>
    public static AngelLook Newborn => new(0f, 0f, 0f, AngelStage.Unformed, AngelMarks.None);

    /// <summary>Its voice.</summary>
    public AngelTone Tone => Stage switch
    {
        AngelStage.Radiant or AngelStage.Kind => AngelTone.Light,
        AngelStage.Unformed => AngelTone.Silver,
        AngelStage.Torn => AngelTone.Torn,
        AngelStage.Stern or AngelStage.Fallen => AngelTone.Dark,
    };

    /// <summary>Its stage's place from the darkest (0) to the lightest (4); the torn and the unformed are in the middle.</summary>
    public int Light => Stage switch
    {
        AngelStage.Fallen => 0,
        AngelStage.Stern => 1,
        AngelStage.Unformed or AngelStage.Torn => 2,
        AngelStage.Kind => 3,
        AngelStage.Radiant => 4,
    };
}

/// <summary>Reads the angel's look from its god's conduct and traits (ADR-074). Pure.</summary>
public static class AngelLooks
{
    // Where the stages divide on the lean.
    private const float RadiantLean = 0.6f;
    private const float KindLean = 0.2f;

    /// <summary>The look of a god's angel.</summary>
    /// <param name="god">The god.</param>
    /// <param name="content">The angel's content: how each deed weighs, how soon it forms, and when it is torn.</param>
    /// <returns>The look.</returns>
    public static AngelLook Of(GodState god, AngelContent content)
    {
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(content);
        var (kind, harm) = Weigh(god.Conduct, content.Weights);
        var total = kind + harm;
        var formed = total / (total + content.Softness);
        var lean = total > 0f ? (kind - harm) / total * formed : 0f;
        var conflict = Math.Clamp(2f * Math.Min(kind, harm) / (total + content.Softness), 0f, 1f);
        return new AngelLook(lean, formed, conflict, StageOf(lean, conflict, content.TornConflict), MarksOf(god));
    }

    /// <summary>A look forced to a stage, for captures (<c>--angel=&lt;stage&gt;</c>).</summary>
    /// <param name="stage">The stage.</param>
    /// <param name="marks">Its marks.</param>
    /// <returns>A look in the middle of the stage.</returns>
    public static AngelLook Forced(AngelStage stage, AngelMarks marks = AngelMarks.None) => stage switch
    {
        AngelStage.Radiant => new AngelLook(0.9f, 0.9f, 0f, stage, marks),
        AngelStage.Kind => new AngelLook(0.4f, 0.6f, 0.1f, stage, marks),
        AngelStage.Unformed => new AngelLook(0f, 0.1f, 0f, stage, marks),
        AngelStage.Torn => new AngelLook(0f, 0.8f, 0.8f, stage, marks),
        AngelStage.Stern => new AngelLook(-0.4f, 0.6f, 0.1f, stage, marks),
        AngelStage.Fallen => new AngelLook(-0.9f, 0.9f, 0f, stage, marks),
    };

    /// <summary>The kindness and the harm in a god's conduct, each deed by its weight.</summary>
    /// <param name="conduct">The conduct.</param>
    /// <param name="weights">How each kind of deed weighs.</param>
    /// <returns>The kindness and the harm.</returns>
    public static (float Kind, float Harm) Weigh(GodConduct conduct, AngelWeights weights)
    {
        ArgumentNullException.ThrowIfNull(conduct);
        ArgumentNullException.ThrowIfNull(weights);
        var kind = (conduct.Help * weights.Help) + (conduct.Gifts * weights.Gifts) + (conduct.Faithfulness * weights.Faithfulness);
        var harm = (conduct.Harm * weights.Harm) + (conduct.Cruelty * weights.Cruelty) + (conduct.Vengeance * weights.Vengeance)
            + (conduct.Upheaval * weights.Upheaval) + (conduct.Deceit * weights.Deceit) + (conduct.Aggression * weights.Aggression);
        return (Math.Max(0f, kind), Math.Max(0f, harm));
    }

    /// <summary>The stage of a lean and a conflict.</summary>
    private static AngelStage StageOf(float lean, float conflict, float torn) =>
        conflict >= torn ? AngelStage.Torn
        : lean >= RadiantLean ? AngelStage.Radiant
        : lean >= KindLean ? AngelStage.Kind
        : lean > -KindLean ? AngelStage.Unformed
        : lean > -RadiantLean ? AngelStage.Stern
        : AngelStage.Fallen;

    /// <summary>The marks of a god's traits.</summary>
    private static AngelMarks MarksOf(GodState god)
    {
        var marks = AngelMarks.None;
        foreach (var trait in god.Traits)
        {
            marks |= trait switch
            {
                GodTrait.Vengeful => AngelMarks.Blade,
                GodTrait.Deceptive => AngelMarks.Veil,
                GodTrait.Cruel => AngelMarks.Chains,
                GodTrait.Ambitious => AngelMarks.Crown,
                GodTrait.Chaotic => AngelMarks.Cracks,
                GodTrait.Generous => AngelMarks.Gold,
                GodTrait.Merciful => AngelMarks.OpenHands,
                GodTrait.Honest => AngelMarks.ClearEyes,
                GodTrait.Compassionate => AngelMarks.None,
            };
        }

        return marks;
    }
}
