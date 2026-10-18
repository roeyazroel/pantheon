namespace Pantheon.Game.Population;

/// <summary>What a representative citizen does, following the settlement's labour split.</summary>
public enum CitizenRole
{
    /// <summary>Works on the house under construction.</summary>
    Builder,

    /// <summary>Works in the fields.</summary>
    Farmer,

    /// <summary>An inhabitant who does not work (children, elders); does chores near home.</summary>
    Villager,
}
