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
}
