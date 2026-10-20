using System.Collections.Generic;
using System.Threading.Tasks;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Game.Minds.Agents;

/// <summary>
/// One god's or Creature's mind as the agents keep it (ADR-072): what woke it and waits for its next
/// turn, the question it is asked now, when it last thought and spoke, and what came of its last turn,
/// for the debug view. None of it is saved: a god's journal, in the world, is what it remembers.
/// </summary>
public sealed class MindState
{
    /// <summary>Creates the mind's state.</summary>
    /// <param name="mind">Whose mind it is.</param>
    internal MindState(Mind mind) => Mind = mind;

    /// <summary>Whose mind it is.</summary>
    public Mind Mind { get; }

    /// <summary>The turns it has taken.</summary>
    public int Turns { get; internal set; }

    /// <summary>Whether it is being asked now.</summary>
    public bool Thinking => Asking is not null;

    /// <summary>How many hooks wait for its next turn.</summary>
    public int Waiting => Hooks.Count;

    /// <summary>What woke it for its last turn.</summary>
    public IReadOnlyList<string> LastHooks { get; internal set; } = [];

    /// <summary>Why it acted as it did in its last turn, in its own words.</summary>
    public string LastReason { get; internal set; } = string.Empty;

    /// <summary>What came of its last turn's acts or choice.</summary>
    public IReadOnlyList<string> LastResults { get; internal set; } = [];

    /// <summary>The last words it said, by its model or the scribes.</summary>
    public string LastSaid { get; internal set; } = string.Empty;

    /// <summary>Why its last question went unanswered, if it did.</summary>
    public string? LastProblem { get; internal set; }

    /// <summary>What woke it since its last turn.</summary>
    internal List<Hook> Hooks { get; } = [];

    /// <summary>When the first of its waiting hooks came, on the agents' clock.</summary>
    internal double? FirstHookAt { get; set; }

    /// <summary>When it was last asked, on the agents' clock.</summary>
    internal double LastTurnAt { get; set; } = double.NegativeInfinity;

    /// <summary>The question it is being asked, until its answer is taken.</summary>
    internal Task<ModelAnswer>? Asking { get; set; }

    /// <summary>The world it was asked about, so an answer about a world since replaced is dropped.</summary>
    internal WorldState? AskedWorld { get; set; }

    /// <summary>What woke it for the question it is being asked.</summary>
    internal IReadOnlyList<Hook> Asked { get; set; } = [];

    /// <summary>A Creature's choices in the question it is being asked, by their place.</summary>
    internal IReadOnlyList<CreatureOption> Choices { get; set; } = [];

    /// <summary>When a god next looks about it unprompted; below 0 until first seen.</summary>
    internal long NextReflectTick { get; set; } = -1;

    /// <summary>The first tick the scribes may speak for it again.</summary>
    internal long NextScribeTick { get; set; }

    /// <summary>Whether a Creature was hungry when last looked at, so growing hungry wakes it once.</summary>
    internal bool Hungry { get; set; }

    /// <summary>Whether a Creature was tired when last looked at.</summary>
    internal bool Tired { get; set; }
}
