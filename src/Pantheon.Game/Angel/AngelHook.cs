namespace Pantheon.Game.Angel;

/// <summary>What moves the angel to speak (ADR-074).</summary>
public enum AngelMoment
{
    /// <summary>It meets its god, the first time in a world.</summary>
    Welcome,

    /// <summary>Its god called it, or asked it something.</summary>
    Called,

    /// <summary>Nothing has been said for a while: the guide offers what to do next.</summary>
    Quiet,

    /// <summary>The god did a kind deed: a miracle that helped, a rescue, an offering, peace or an alliance.</summary>
    DeedKind,

    /// <summary>The god did a harsh deed: a miracle that harmed, people killed by its hand, a war, a betrayal.</summary>
    DeedHarsh,

    /// <summary>The angel grew lighter.</summary>
    StageRose,

    /// <summary>The angel grew darker.</summary>
    StageFell,

    /// <summary>The world came to see the god a new way.</summary>
    Trait,

    /// <summary>A village of the god's went hungry or thirsty.</summary>
    VillageHungry,

    /// <summary>A disaster, or another god's harmful miracle, struck a village of the god's.</summary>
    VillageStruck,

    /// <summary>A village came to follow the god.</summary>
    VillageGained,

    /// <summary>A village left the god, rose against it, or its people left.</summary>
    VillageLost,

    /// <summary>A rival's Creature came into the god's lands.</summary>
    Visitor,

    /// <summary>A god declared war on the god, or betrayed it.</summary>
    War,

    /// <summary>A god offered the god an alliance, peace, a treaty, or asked something of it.</summary>
    Offer,

    /// <summary>The god met another god.</summary>
    Met,

    /// <summary>The god's Creature won a fight.</summary>
    CreatureWon,

    /// <summary>The god's Creature lost a fight, or ran.</summary>
    CreatureLost,

    /// <summary>The god's Creature grew hungry or tired.</summary>
    CreatureNeeds,

    /// <summary>The god rose in rank.</summary>
    Rank,
}

/// <summary>Something that moved the angel (ADR-074).</summary>
/// <param name="Moment">What it is.</param>
/// <param name="Tick">When it happened.</param>
/// <param name="Text">What happened, in words for the angel's model, from its god's side.</param>
/// <param name="SettlementId">The village it concerns, if any.</param>
/// <param name="GodId">The other god it concerns, if any.</param>
/// <param name="CreatureId">The Creature it concerns, if any.</param>
/// <param name="Detail">A name for the scribes' lines: a miracle, a trait or a rank; empty for none.</param>
public sealed record AngelHook(AngelMoment Moment, long Tick, string Text, int? SettlementId = null, int? GodId = null, int? CreatureId = null, string Detail = "")
{
    /// <summary>How much it matters to the angel, for the scribes, who speak of one moment only: its first greeting and its god's call first, then danger, then deeds.</summary>
    public int Weight => Moment switch
    {
        AngelMoment.Welcome => 100,
        AngelMoment.Called => 95,
        AngelMoment.War => 80,
        AngelMoment.VillageStruck => 70,
        AngelMoment.VillageHungry => 68,
        AngelMoment.VillageLost => 66,
        AngelMoment.StageFell or AngelMoment.StageRose => 64,
        AngelMoment.Visitor => 60,
        AngelMoment.Offer => 56,
        AngelMoment.CreatureLost => 52,
        AngelMoment.DeedHarsh => 50,
        AngelMoment.Trait => 48,
        AngelMoment.DeedKind => 44,
        AngelMoment.Rank => 42,
        AngelMoment.VillageGained => 40,
        AngelMoment.Met => 36,
        AngelMoment.CreatureWon => 34,
        AngelMoment.CreatureNeeds => 30,
        AngelMoment.Quiet => 10,
    };
}
