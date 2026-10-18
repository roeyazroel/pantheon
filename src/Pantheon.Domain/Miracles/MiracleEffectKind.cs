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
}
