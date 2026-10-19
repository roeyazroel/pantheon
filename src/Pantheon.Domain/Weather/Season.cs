namespace Pantheon.Domain.Weather;

/// <summary>A season of the year (spec §17, ADR-028).</summary>
public enum Season
{
    /// <summary>The land wakes: snow melts and crops are sown.</summary>
    Spring,

    /// <summary>Warm and dry: crops grow, fires and heat waves threaten.</summary>
    Summer,

    /// <summary>The harvest: the wettest season, and hurricanes come in from the sea.</summary>
    Autumn,

    /// <summary>Cold: snow lies, crops wait, stores must last and the cold sickens.</summary>
    Winter,
}
