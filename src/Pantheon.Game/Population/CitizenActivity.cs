namespace Pantheon.Game.Population;

/// <summary>What a representative citizen is doing at a moment of its routine.</summary>
public enum CitizenActivity
{
    /// <summary>Resting in front of its house.</summary>
    AtHome,

    /// <summary>On the way between home, work and the shrine.</summary>
    Walking,

    /// <summary>At work: in a field, at the building site or doing chores.</summary>
    Working,

    /// <summary>Gathered around the shrine.</summary>
    Gathering,
}
