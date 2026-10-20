using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Events;

namespace Pantheon.Game.Flow;

/// <summary>The tutorial's steps (ADR-064), in order.</summary>
public enum TutorialStep
{
    /// <summary>Who the player is and what the game is.</summary>
    Welcome,

    /// <summary>Move the view.</summary>
    Move,

    /// <summary>Zoom in on the home village.</summary>
    Zoom,

    /// <summary>Turn the view.</summary>
    Turn,

    /// <summary>The god's plaque: power, worship, followers.</summary>
    Plaque,

    /// <summary>Cast Food on the village.</summary>
    Cast,

    /// <summary>Influence and the map overlays.</summary>
    Influence,

    /// <summary>Look at the Creature and follow it.</summary>
    Creature,

    /// <summary>Reward or punish the Creature.</summary>
    Teach,

    /// <summary>Set the Creature to work.</summary>
    Work,

    /// <summary>Pick something up with the hand.</summary>
    Hand,

    /// <summary>Speed up time.</summary>
    Speed,

    /// <summary>Open the diplomacy panel.</summary>
    Rivals,

    /// <summary>How to set the Creature on a rival's.</summary>
    Fight,

    /// <summary>How to win, and where the rest is.</summary>
    Win,

    /// <summary>The tutorial is over.</summary>
    Done,
}

/// <summary>What part of the screen a step points at (ADR-064).</summary>
public enum TutorialTarget
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>The god's plaque at the top left.</summary>
    Plaque,

    /// <summary>The first miracle on the bar: Food.</summary>
    FirstMiracle,

    /// <summary>The Creature's Follow button.</summary>
    FollowButton,

    /// <summary>The Creature's Reward and Punish buttons.</summary>
    JudgeButtons,

    /// <summary>The Creature's Work button.</summary>
    WorkButton,

    /// <summary>The pause and speed buttons at the top right.</summary>
    TimeControls,

    /// <summary>The rival gods' chips.</summary>
    Rivals,

    /// <summary>The race for victory at the top centre.</summary>
    Race,
}

/// <summary>What the client sees of the player each frame, for the tutorial to judge the steps by.</summary>
/// <param name="Focus">Where the camera looks, on the ground.</param>
/// <param name="Distance">The camera's distance, in metres.</param>
/// <param name="Yaw">The camera's turn, in radians.</param>
/// <param name="Following">Whether the camera follows the Creature.</param>
/// <param name="Holding">Whether the hand holds something.</param>
/// <param name="TimeScale">How fast the world runs.</param>
/// <param name="DiplomacyOpen">Whether the diplomacy panel is open.</param>
public readonly record struct TutorialView(Vector2 Focus, float Distance, float Yaw, bool Following, bool Holding, double TimeScale, bool DiplomacyOpen);

/// <summary>Something the client saw the player do that leaves no event in the world.</summary>
public enum TutorialSignal
{
    /// <summary>The player set the Creature to work.</summary>
    WorkOrdered,
}

/// <summary>
/// The tutorial (ADR-064): fifteen steps that teach the game by playing it, from moving the view to
/// winning. A step that asks for an action ends when the player does it, judged from the world's
/// events and what the client sees (the camera, the hand, the time, the panels); the others end with
/// Next. Any step may be skipped. It points at the part of the screen it speaks of. Presentation
/// state only: it never touches the world.
/// </summary>
public sealed class Tutorial
{
    // What counts as doing a step: moving the view this far, zooming this close, turning this much.
    private const float MoveMetres = 60f;
    private const float ZoomMetres = 160f;
    private const float TurnRadians = 0.6f;

    private TutorialView? _baseline;
    private bool _signalled;

    /// <summary>The step shown now.</summary>
    public TutorialStep Step { get; private set; } = TutorialStep.Done;

    /// <summary>Whether the tutorial is under way.</summary>
    public bool IsActive => Step != TutorialStep.Done;

    /// <summary>How many steps there are.</summary>
    public static int StepCount => (int)TutorialStep.Done;

    /// <summary>Whether the step asks for no action, so Next moves on.</summary>
    public bool WaitsForNext => Step is TutorialStep.Welcome or TutorialStep.Plaque or TutorialStep.Influence or TutorialStep.Fight or TutorialStep.Win;

    /// <summary>What the step points at.</summary>
    public TutorialTarget Target => Step switch
    {
        TutorialStep.Plaque => TutorialTarget.Plaque,
        TutorialStep.Cast => TutorialTarget.FirstMiracle,
        TutorialStep.Creature => TutorialTarget.FollowButton,
        TutorialStep.Teach => TutorialTarget.JudgeButtons,
        TutorialStep.Work => TutorialTarget.WorkButton,
        TutorialStep.Speed => TutorialTarget.TimeControls,
        TutorialStep.Rivals => TutorialTarget.Rivals,
        TutorialStep.Win => TutorialTarget.Race,
        TutorialStep.Welcome or TutorialStep.Move or TutorialStep.Zoom or TutorialStep.Turn or TutorialStep.Influence or TutorialStep.Hand or TutorialStep.Fight or TutorialStep.Done => TutorialTarget.None,
    };

    /// <summary>Whether the step points at the home village in the world (a beacon of light).</summary>
    public bool MarksHome => Step is TutorialStep.Zoom or TutorialStep.Cast or TutorialStep.Work;

    /// <summary>Starts the tutorial from the beginning, or from a step (for captures).</summary>
    /// <param name="step">The step to start from.</param>
    public void Start(TutorialStep step = TutorialStep.Welcome) => Enter(step);

    /// <summary>Ends the tutorial.</summary>
    public void Stop() => Enter(TutorialStep.Done);

    /// <summary>Moves on: past a step that asks for nothing, or skipping one that does.</summary>
    public void Next()
    {
        if (IsActive)
        {
            Enter(Step + 1);
        }
    }

    /// <summary>The step's title.</summary>
    /// <returns>A few words.</returns>
    public string Title() => Step switch
    {
        TutorialStep.Welcome => "Welcome, young god",
        TutorialStep.Move => "Moving around",
        TutorialStep.Zoom => "Coming closer",
        TutorialStep.Turn => "Turning the view",
        TutorialStep.Plaque => "Your power",
        TutorialStep.Cast => "Your first miracle",
        TutorialStep.Influence => "Where you can act",
        TutorialStep.Creature => "Your Creature",
        TutorialStep.Teach => "Teaching it",
        TutorialStep.Work => "Putting it to work",
        TutorialStep.Hand => "Your hand",
        TutorialStep.Speed => "Time",
        TutorialStep.Rivals => "Rival gods",
        TutorialStep.Fight => "Creature against Creature",
        TutorialStep.Win => "Winning",
        TutorialStep.Done => string.Empty,
    };

    /// <summary>What the step says.</summary>
    /// <param name="godName">The player's god.</param>
    /// <param name="creatureName">Its Creature.</param>
    /// <param name="home">Its home village.</param>
    /// <returns>The text; empty when done.</returns>
    public string Text(string godName, string creatureName, string home) => Step switch
    {
        TutorialStep.Welcome => $"You are {godName}, a minor spirit. The people of this island believe in gods: the more of them believe in you, the stronger you grow. This tutorial teaches you to play by playing. It takes about ten minutes, and you may skip any step.",
        TutorialStep.Move => "Move around the island: drag the land with the left mouse button, or slide two fingers on a trackpad. W A S D and the arrow keys move you too.",
        TutorialStep.Zoom => $"Come closer to your village, {home}, under the beacon of light: scroll the mouse wheel or pinch the trackpad. A double click flies you to a place.",
        TutorialStep.Turn => "Turn the view: drag with the right mouse button, or hold Option and slide two fingers on the trackpad. Q and E turn it, and Page Up and Page Down tilt it.",
        TutorialStep.Plaque => "Top left is your plaque: your Divine Power in gold, what your followers' worship earns you each second, and how many follow you. More followers raise your rank, and with it your miracles.",
        TutorialStep.Cast => $"Miracles cost Divine Power. Choose Food: press 1, or click it on the bar below. Then click {home} to feed its people. The ring shows where it will fall, green where you can reach.",
        TutorialStep.Influence => "You can work miracles only where your influence reaches: press O to see it, and again for the other maps. Helping people wins their belief; frightening them works too, but fear fades.",
        TutorialStep.Creature => $"{creatureName} is your Creature: it lives on its own, and learns from you. Press C, or Follow on its card, to look at it. Move the view to let it go.",
        TutorialStep.Teach => $"When {creatureName} does something you like, reward it: press Z. When it does something you do not, punish it: press X. It does more of what you reward, and less of what you punish.",
        TutorialStep.Work => $"{creatureName} can work for your villages. Point at {home} and press B, or click Work on its card and then the village: it farms, fetches wood or builds, whatever the village needs.",
        TutorialStep.Hand => "Your hand reaches into the world: point at a tree, a rock or a villager and press R to pick it up. Press R again to set it down, or Shift+R to throw it.",
        TutorialStep.Speed => "The buttons at the top right pause the world and run it faster. Try ×4 (or press ]). Space pauses.",
        TutorialStep.Rivals => "Other gods want these people too: their chips are under your plaque. Press P for diplomacy: alliances, treaties and wars.",
        TutorialStep.Fight => $"If a rival's Creature threatens your people, set {creatureName} on it: point near it and press Shift+V, or click Fight on its card. It hunts it down. A treaty can forbid it.",
        TutorialStep.Win => "To win, hold 60% of all belief for ten minutes: the bar at the top tracks it. Esc pauses, and your world saves itself. The Book of the World (Tab) tells you everything else. Good luck!",
        TutorialStep.Done => string.Empty,
    };

    /// <summary>Moves on when the player has done what the step asks, by what the client sees.</summary>
    /// <param name="view">What the client sees now.</param>
    public void See(TutorialView view)
    {
        if (!IsActive)
        {
            return;
        }

        if (_baseline is not { } start)
        {
            _baseline = view;
            return;
        }

        var done = Step switch
        {
            TutorialStep.Move => Vector2.Distance(view.Focus, start.Focus) >= MoveMetres,
            TutorialStep.Zoom => view.Distance <= ZoomMetres,
            TutorialStep.Turn => MathF.Abs(MathF.IEEERemainder(view.Yaw - start.Yaw, MathF.Tau)) >= TurnRadians,
            TutorialStep.Creature => view.Following,
            TutorialStep.Hand => view.Holding,
            TutorialStep.Speed => view.TimeScale > 1.0,
            TutorialStep.Rivals => view.DiplomacyOpen,
            TutorialStep.Work => _signalled,
            TutorialStep.Welcome or TutorialStep.Plaque or TutorialStep.Cast or TutorialStep.Influence or TutorialStep.Teach
                or TutorialStep.Fight or TutorialStep.Win or TutorialStep.Done => false,
        };
        if (done)
        {
            Enter(Step + 1);
        }
    }

    /// <summary>Moves on when the world's events show the player did what the step asks.</summary>
    /// <param name="events">Events since the last call.</param>
    /// <param name="localGodId">The player's god.</param>
    /// <param name="creatureId">The player's Creature.</param>
    public void Observe(IReadOnlyList<WorldEvent> events, int? localGodId, int? creatureId)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var worldEvent in events)
        {
            var done = (Step, worldEvent) switch
            {
                (TutorialStep.Cast, MiracleCast cast) => cast.GodId == localGodId,
                (TutorialStep.Teach, CreatureChanged { Change: CreatureChange.Rewarded or CreatureChange.Punished } judged) => judged.CreatureId == creatureId,
                _ => false,
            };
            if (done)
            {
                Enter(Step + 1);
                return;
            }
        }
    }

    /// <summary>Notes something the player did that leaves no event.</summary>
    /// <param name="signal">What it did.</param>
    public void Notice(TutorialSignal signal)
    {
        if (signal == TutorialSignal.WorkOrdered && Step == TutorialStep.Work)
        {
            _signalled = true;
        }
    }

    /// <summary>Enters a step: what the player did before it no longer counts.</summary>
    private void Enter(TutorialStep step)
    {
        Step = step;
        _baseline = null;
        _signalled = false;
    }
}
