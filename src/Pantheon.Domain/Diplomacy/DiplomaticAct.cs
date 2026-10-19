namespace Pantheon.Domain.Diplomacy;

/// <summary>Something one god did that another god remembers (spec §40–41, §113: "AI remembers betrayal").</summary>
public enum DiplomaticAct
{
    /// <summary>It helped the remembering god's followers in need.</summary>
    Helped,

    /// <summary>It courted the remembering god's followers with miracles they did not need.</summary>
    Courted,

    /// <summary>It struck the remembering god's followers.</summary>
    Harmed,

    /// <summary>It won over a settlement that followed the remembering god.</summary>
    Converted,

    /// <summary>Its Creature attacked the remembering god's followers.</summary>
    CreatureAttacked,

    /// <summary>It broke an alliance with the remembering god.</summary>
    Betrayed,

    /// <summary>It declared war on the remembering god.</summary>
    DeclaredWar,

    /// <summary>It made an alliance with the remembering god.</summary>
    Allied,

    /// <summary>It made peace with the remembering god.</summary>
    MadePeace,

    /// <summary>It harmed, converted from, attacked, betrayed or declared war on the remembering god's ally (ADR-015).</summary>
    WrongedAlly,

    /// <summary>It took in migrants who followed the remembering god (ADR-016).</summary>
    Sheltered,

    /// <summary>It turned away migrants who followed the remembering god (ADR-016).</summary>
    TurnedAway,

    /// <summary>Its Creature defeated and banished the remembering god's Creature (ADR-017).</summary>
    CreatureDefeated,

    /// <summary>It took a holy site the remembering god kept (ADR-021).</summary>
    TookHolySite,

    /// <summary>It saved the remembering god's followers in great need, so a favour is owed (spec §41, ADR-025).</summary>
    Rescued,

    /// <summary>It broke a treaty with the remembering god (spec §40).</summary>
    BrokeTreaty,

    /// <summary>It broke a promise to the remembering god, or refused a favour it owed.</summary>
    BrokePromise,

    /// <summary>It voted in the council against the remembering god (a public ballot).</summary>
    VotedAgainst,

    /// <summary>It defied a decree of the Divine Council.</summary>
    DefiedCouncil,

    /// <summary>It kept its word: it granted a request or kept a promise.</summary>
    KeptWord,

    /// <summary>It signed a treaty with the remembering god.</summary>
    SignedTreaty,

    /// <summary>It plotted against the remembering god in a secret treaty that came to light (spec §39).</summary>
    PlottedAgainst,

    /// <summary>Its people waged holy war on the remembering god's people (spec §46, ADR-026).</summary>
    HolyWar,

    /// <summary>It worked against the remembering god's people in secret and was found out (spec §45, ADR-026).</summary>
    CovertAct,

    /// <summary>It dictated peace to the remembering god.</summary>
    Dictated,
}
