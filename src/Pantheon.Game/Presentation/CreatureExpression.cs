using System;
using System.Linq;
using Pantheon.Domain.Creatures;

namespace Pantheon.Game.Presentation;

/// <summary>What a Creature's face and tail show (ADR-055).</summary>
/// <param name="Joy">From -1 (a frown) to 1 (a broad smile).</param>
/// <param name="Anger">From 0 to 1: brows drawn down and in.</param>
/// <param name="Fear">From 0 to 1: brows up, eyes wide.</param>
/// <param name="Sleep">From 0 (wide awake) to 1 (eyes shut).</param>
/// <param name="Wag">From 0 to 1: how its tail wags.</param>
public readonly record struct CreatureFace(float Joy, float Anger, float Fear, float Sleep, float Wag);

/// <summary>The mood a Creature's face shows most.</summary>
public enum CreatureMood
{
    /// <summary>At ease.</summary>
    Content,

    /// <summary>Happy.</summary>
    Joyful,

    /// <summary>Angry.</summary>
    Angry,

    /// <summary>Afraid.</summary>
    Afraid,

    /// <summary>Unhappy.</summary>
    Sad,

    /// <summary>Drowsy or asleep.</summary>
    Sleepy,
}

/// <summary>
/// How a Creature looks as it feels (spec §61 "expressive Creatures", ADR-055): its face from what
/// it is doing (a scowl in a fight, wide eyes in flight, a grin at play, shut eyes at rest), its
/// needs (hungry and bored is glum, tired is drowsy), its health, its nature (a kind Creature smiles
/// more, a cruel one scowls) and its god's last word to it (praise brings joy, a slap fear).
/// Derived from the state, never saved. Pure.
/// </summary>
public static class CreatureExpression
{
    /// <summary>How long a reward or a punishment shows on the face, in ticks.</summary>
    public const long FeedbackTicks = 300;

    /// <summary>A Creature's face now.</summary>
    /// <param name="creature">The Creature.</param>
    /// <param name="tick">The world's tick.</param>
    /// <param name="justJudged">
    /// A reward or punishment the client has just seen given, which shows even when it judged a deed
    /// still under way (and so marked no memory); <c>null</c> to read the memories only.
    /// </param>
    /// <returns>Its face.</returns>
    public static CreatureFace FaceOf(CreatureState creature, long tick, CreatureFeedback? justJudged = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var (joy, anger, fear) = ((0.3f * creature.Morality) - (0.6f * Over(creature.Needs.Hunger, 0.5f)) - (0.4f * Over(creature.Needs.Boredom, 0.6f)), Math.Max(0f, -0.45f * creature.Morality), creature.Health < 0.3f ? 0.6f : 0f);
        var sleep = Math.Clamp((creature.Needs.Fatigue - 0.6f) * 2f, 0f, 0.8f);
        var activity = creature.Activity;
        var doing = activity.Phase is CreaturePhase.Performing or CreaturePhase.Travelling;
        switch (doing ? activity.Action : (CreatureAction?)null)
        {
            case CreatureAction.Fight or CreatureAction.Attack:
                (anger, joy) = (1f, Math.Min(joy, -0.3f));
                break;
            case CreatureAction.Flee:
                (fear, joy) = (1f, Math.Min(joy, -0.5f));
                break;
            case CreatureAction.Play or CreatureAction.Befriend:
                joy = 1f;
                break;
            case CreatureAction.Rest when activity.Phase == CreaturePhase.Performing:
                sleep = 1f;
                break;
            case CreatureAction.Feed or CreatureAction.HelpBuild:
                joy += 0.4f;
                break;
            case CreatureAction.EatFromStores or CreatureAction.Forage:
                joy += 0.3f;
                break;
        }

        var last = justJudged ?? creature.Memories.Where(m => m.Feedback != CreatureFeedback.None && tick - m.Tick <= FeedbackTicks).OrderByDescending(m => m.Tick).FirstOrDefault()?.Feedback;
        (joy, fear) = last switch
        {
            CreatureFeedback.Rewarded => (joy + 0.8f, fear),
            CreatureFeedback.Punished => (joy - 0.6f, Math.Max(fear, 0.6f)),
            _ => (joy, fear),
        };

        joy = Math.Clamp(joy, -1f, 1f);
        return new CreatureFace(joy, Math.Clamp(anger, 0f, 1f), Math.Clamp(fear, 0f, 1f), sleep, Math.Clamp(joy, 0f, 1f));
    }

    /// <summary>The mood a face shows most.</summary>
    /// <param name="face">The face.</param>
    /// <returns>The mood.</returns>
    public static CreatureMood MoodOf(CreatureFace face)
    {
        var (mood, strength) = (CreatureMood.Content, 0.35f);
        foreach (var (candidate, value) in new[] { (CreatureMood.Sleepy, face.Sleep), (CreatureMood.Angry, face.Anger), (CreatureMood.Afraid, face.Fear), (CreatureMood.Joyful, face.Joy), (CreatureMood.Sad, -face.Joy) })
        {
            if (value > strength)
            {
                (mood, strength) = (candidate, value);
            }
        }

        return mood;
    }

    /// <summary>How far a need is past a threshold, as a share of the rest of the way.</summary>
    private static float Over(float need, float threshold) => Math.Clamp((need - threshold) / (1f - threshold), 0f, 1f);
}
