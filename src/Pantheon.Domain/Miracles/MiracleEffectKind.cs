namespace Pantheon.Domain.Miracles;

/// <summary>What a miracle does to the world.</summary>
public enum MiracleEffectKind
{
    /// <summary>Adds <c>Amount</c> food to each settlement it reaches, up to its storage.</summary>
    AddFood,

    /// <summary>Raises the health of each settlement it reaches by <c>Amount</c>.</summary>
    Heal,

    /// <summary>Raises the blessing of each region it reaches to at least <c>Amount</c>; blessed land yields more.</summary>
    Bless,

    /// <summary>Kills up to <c>Amount</c> people in each settlement it reaches.</summary>
    Smite,

    /// <summary>Calls a rain cloud of the miracle's radius over the target, carrying <c>Amount</c> humidity (ADR-013).</summary>
    Rain,

    /// <summary>Sets every region the miracle reaches alight at <c>Amount</c> intensity, where there is fuel to burn (ADR-014).</summary>
    Ignite,

    /// <summary>Starts an earthquake of magnitude <c>Amount</c> at the target (ADR-014).</summary>
    Quake,

    /// <summary>Shields every settlement it reaches from disasters for <c>Amount</c> simulated seconds (ADR-014).</summary>
    Shield,

    /// <summary>Puts out the fires it reaches, lifts <c>Amount</c> of their scorch and waters their soil (Nature, ADR-021).</summary>
    Regrow,

    /// <summary>Brings up to <c>Amount</c> newcomers, who share its beliefs, to each settlement it reaches where there are homes for them (Life, ADR-021).</summary>
    Populate,

    /// <summary>Carries the strongest rain or storm cloud within <c>Amount</c> metres of the target to the target (Storm, ADR-021).</summary>
    Redirect,

    /// <summary>Takes <c>Amount</c> health from every other god's Creature it reaches, except allies' (War, ADR-021).</summary>
    Wound,

    /// <summary>Removes <c>Amount</c> of the Faith and adds as much Doubt in every other god its witnesses follow, except allies (Death, ADR-021).</summary>
    Terrify,

    /// <summary>For <c>Amount</c> simulated seconds the caster's followers in each settlement it reaches cannot fall away (Protection, ADR-021).</summary>
    Sanctuary,

    /// <summary>Gives the caster's Creature <c>Amount</c> experience when the miracle reaches it (Knowledge, ADR-021).</summary>
    Teach,

    /// <summary>Makes each pair of gods followed where it lands, other than the caster and its allies, remember <c>Amount</c> wrongs done by the other that neither did (Trickery, ADR-021).</summary>
    Discord,

    /// <summary>Fills the wells and cisterns of each settlement it reaches with <c>Amount</c> water, up to their storage (ADR-022).</summary>
    Water,

    /// <summary>Soaks the soil of every region it reaches to at least <c>Amount</c> (ADR-022).</summary>
    Soak,

    /// <summary>Sends a wave over the land within its radius that lies less than <c>Amount</c> metres above the sea: it floods, the lowest deepest (Ocean, ADR-022).</summary>
    Flood,

    /// <summary>
    /// Punishes the followers of gods who wronged the caster, in each settlement it reaches: <c>Amount</c> of them die, and as
    /// much of their Faith in that god gives way to Doubt. A god wronged the caster when they are at war or the caster bears it a grudge (Justice, ADR-022).
    /// </summary>
    Judge,
}
