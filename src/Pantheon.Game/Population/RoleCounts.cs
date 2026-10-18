namespace Pantheon.Game.Population;

/// <summary>How many representatives take each role.</summary>
/// <param name="Builders">Representatives at the building site.</param>
/// <param name="Farmers">Representatives in the fields.</param>
/// <param name="Villagers">Representatives who do not work.</param>
public readonly record struct RoleCounts(int Builders, int Farmers, int Villagers)
{
    /// <summary>All representatives.</summary>
    public int Total => Builders + Farmers + Villagers;
}
