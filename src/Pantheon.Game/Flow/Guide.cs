using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;

namespace Pantheon.Game.Flow;

/// <summary>A step of the first-minutes guide.</summary>
public enum GuideStep
{
    /// <summary>Who the player is and what the HUD shows.</summary>
    Welcome,

    /// <summary>Take up a first domain (ADR-021).</summary>
    Domain,

    /// <summary>Cast a miracle on a village within the god's influence.</summary>
    Cast,

    /// <summary>Reward or punish the Creature.</summary>
    Teach,

    /// <summary>Open the diplomacy panel.</summary>
    Diplomacy,

    /// <summary>How to win, and where the rest is.</summary>
    Win,

    /// <summary>The guide is over or hidden.</summary>
    Done,
}

/// <summary>
/// The first-minutes guide (ADR-020): a few steps that teach the game by doing it. A step that asks
/// for an action ends when the player does it; the others end with "Next". Presentation state only.
/// </summary>
public sealed class Guide
{
    /// <summary>The step shown now.</summary>
    public GuideStep Step { get; private set; } = GuideStep.Welcome;

    /// <summary>Whether the guide shows anything.</summary>
    public bool IsVisible => Step != GuideStep.Done;

    /// <summary>Whether "Next" moves on (the steps that ask for no action).</summary>
    public bool CanSkipStep => Step is GuideStep.Welcome or GuideStep.Win;

    /// <summary>What the step says.</summary>
    /// <param name="godName">The player's god.</param>
    /// <param name="creatureName">Its Creature.</param>
    /// <returns>The text; empty when done.</returns>
    public string Text(string godName, string creatureName) => Step switch
    {
        GuideStep.Welcome => $"You are {godName}. The villages near your shrine believe in you: their worship is your Divine Power, shown at the top with your followers.",
        GuideStep.Domain => "Choose what you are a god of: press G and take up a domain. Each changes how you play and opens a miracle of its own; you gain more as you rise.",
        GuideStep.Cast => "Pick a miracle with 1-9 and click a village inside your influence (O shows it). Food for the hungry, Heal for the sick, Lightning for those who doubt you.",
        GuideStep.Teach => $"{creatureName} is your Creature and decides for itself. When it does something you like, reward it (Z); punish what you dislike (X). It learns.",
        GuideStep.Diplomacy => "Three other gods want the same people. Press P to see what they think of you, and to ally, make peace or declare war.",
        GuideStep.Win => "Hold 60% of all belief for ten minutes to win. Holy sites (G) give power to the god whose influence holds them. H shows the world's history, and Esc pauses.",
        GuideStep.Done => string.Empty,
    };

    /// <summary>Moves past a step that asks for no action.</summary>
    public void Next()
    {
        if (CanSkipStep)
        {
            Step = Step == GuideStep.Win ? GuideStep.Done : Step + 1;
        }
    }

    /// <summary>Hides the guide for good.</summary>
    public void Hide() => Step = GuideStep.Done;

    /// <summary>Starts the guide over, for a new game.</summary>
    public void Restart() => Step = GuideStep.Welcome;

    /// <summary>Moves on when the player has done what the step asks.</summary>
    /// <param name="events">Events since the last call.</param>
    /// <param name="localGodId">The player's god.</param>
    /// <param name="creatureId">The player's Creature.</param>
    /// <param name="diplomacyOpen">Whether the diplomacy panel is open.</param>
    public void Observe(IReadOnlyList<WorldEvent> events, int? localGodId, int? creatureId, bool diplomacyOpen)
    {
        ArgumentNullException.ThrowIfNull(events);
        var done = Step switch
        {
            GuideStep.Domain => Exists(events, e => e is DomainChosen chosen && chosen.GodId == localGodId),
            GuideStep.Cast => Exists(events, e => e is MiracleCast cast && cast.GodId == localGodId),
            GuideStep.Teach => Exists(events, e => e is CreatureChanged { Change: CreatureChange.Rewarded or CreatureChange.Punished } judged && judged.CreatureId == creatureId),
            GuideStep.Diplomacy => diplomacyOpen,
            GuideStep.Welcome or GuideStep.Win or GuideStep.Done => false,
        };
        if (done)
        {
            Step++;
        }
    }

    /// <summary>Whether any event matches.</summary>
    private static bool Exists(IReadOnlyList<WorldEvent> events, Func<WorldEvent, bool> test)
    {
        foreach (var worldEvent in events)
        {
            if (test(worldEvent))
            {
                return true;
            }
        }

        return false;
    }
}
