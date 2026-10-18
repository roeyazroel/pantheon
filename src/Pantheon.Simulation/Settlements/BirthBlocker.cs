namespace Pantheon.Simulation.Settlements;

/// <summary>Why a settlement can have no births in a step.</summary>
public enum BirthBlocker
{
    /// <summary>Births can happen.</summary>
    None,

    /// <summary>Nobody lives in the settlement.</summary>
    NoPeople,

    /// <summary>Every place in the houses is taken.</summary>
    NoFreeHousing,

    /// <summary>The harvest does not cover consumption, or the stores hold too little food per person.</summary>
    NoFoodSurplus,
}
