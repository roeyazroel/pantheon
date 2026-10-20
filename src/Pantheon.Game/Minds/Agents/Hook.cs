namespace Pantheon.Game.Minds.Agents;

/// <summary>What wakes a god's or a Creature's mind (ADR-072).</summary>
public enum HookKind
{
    /// <summary>Another god's Creature came into the god's lands, and it answered.</summary>
    Visitor,

    /// <summary>The god's Creature was noticed in another god's lands, and that god answered.</summary>
    Answered,

    /// <summary>A diplomatic move between the god and another: war, peace, an alliance, a proposal, a betrayal.</summary>
    Diplomacy,

    /// <summary>A treaty of the god's was signed or ended.</summary>
    Treaty,

    /// <summary>A village came to follow the god, or left it.</summary>
    Village,

    /// <summary>A harmful miracle or a disaster struck a village the god leads.</summary>
    Struck,

    /// <summary>The god's Creature's fight ended.</summary>
    CreatureFight,

    /// <summary>Another god spoke to the god.</summary>
    Words,

    /// <summary>The player talked with the god.</summary>
    Talked,

    /// <summary>The god met another god.</summary>
    Met,

    /// <summary>The god's time to look about it, when nothing else woke it.</summary>
    Reflect,

    /// <summary>The Creature met another Creature.</summary>
    Meeting,

    /// <summary>The Creature's fight ended.</summary>
    Fight,

    /// <summary>The Creature's god rewarded or punished it.</summary>
    Judged,

    /// <summary>The Creature grew: a level, or a calling.</summary>
    Grew,

    /// <summary>The Creature grew hungry or tired.</summary>
    Need,

    /// <summary>A village of its god near it was struck.</summary>
    Home,

    /// <summary>A god noticed the Creature in its lands, or the Creature was sent to answer a visitor.</summary>
    Noticed,
}

/// <summary>Whose mind it is: a god's or a Creature's.</summary>
public enum MindKind
{
    /// <summary>An AI god's.</summary>
    God,

    /// <summary>A Creature's.</summary>
    Creature,
}

/// <summary>A mind: an AI god's or a Creature's, by its id.</summary>
/// <param name="Kind">Whose.</param>
/// <param name="Id">The god's or the Creature's id.</param>
public readonly record struct Mind(MindKind Kind, int Id)
{
    /// <summary>A god's mind.</summary>
    /// <param name="id">The god.</param>
    /// <returns>Its mind.</returns>
    public static Mind God(int id) => new(MindKind.God, id);

    /// <summary>A Creature's mind.</summary>
    /// <param name="id">The Creature.</param>
    /// <returns>Its mind.</returns>
    public static Mind Creature(int id) => new(MindKind.Creature, id);
}

/// <summary>Something that woke a mind (ADR-072).</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Tick">When it happened.</param>
/// <param name="Text">What happened, in words from the mind's side, for its model.</param>
/// <param name="Voice">The scribes' moment for it (<see cref="VoiceLines"/>), or empty when the scribes say nothing of it.</param>
/// <param name="OtherGodId">The god it concerns: for a god, the one its words are for.</param>
/// <param name="OtherCreatureId">The Creature it concerns, if any.</param>
/// <param name="SettlementId">The village it concerns, if any.</param>
public sealed record Hook(HookKind Kind, long Tick, string Text, string Voice = "", int? OtherGodId = null, int? OtherCreatureId = null, int? SettlementId = null);
