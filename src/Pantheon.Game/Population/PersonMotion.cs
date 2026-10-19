namespace Pantheon.Game.Population;

/// <summary>How a person's figure moves (ADR-049); the person shader animates each.</summary>
public enum PersonMotion
{
    /// <summary>Standing, breathing.</summary>
    Standing,

    /// <summary>Walking: legs and arms swing with the stride.</summary>
    Walking,

    /// <summary>Sitting on the ground by the door.</summary>
    Sitting,

    /// <summary>Hoeing a field.</summary>
    Hoeing,

    /// <summary>Hammering at the building site.</summary>
    Hammering,

    /// <summary>Bending to chores.</summary>
    Chores,

    /// <summary>Praying at the shrine, arms raised now and then.</summary>
    Praying,
}

/// <summary>Which motion shows what a crowd's figure is doing.</summary>
public static class PersonMotions
{
    /// <summary>The motion for an activity and a role.</summary>
    /// <param name="activity">What the figure is doing.</param>
    /// <param name="role">Its work.</param>
    /// <returns>The motion.</returns>
    public static PersonMotion Of(CitizenActivity activity, CitizenRole role) => activity switch
    {
        CitizenActivity.AtHome => PersonMotion.Sitting,
        CitizenActivity.Walking => PersonMotion.Walking,
        CitizenActivity.Gathering => PersonMotion.Praying,
        CitizenActivity.Working => role switch
        {
            CitizenRole.Farmer => PersonMotion.Hoeing,
            CitizenRole.Builder => PersonMotion.Hammering,
            CitizenRole.Villager => PersonMotion.Chores,
        },
    };
}
