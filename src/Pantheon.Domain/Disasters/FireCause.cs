namespace Pantheon.Domain.Disasters;

/// <summary>What set a region alight.</summary>
public enum FireCause
{
    /// <summary>A thunderstorm's lightning on dry, vegetated land.</summary>
    Lightning,

    /// <summary>A settlement's own hearths on dry land.</summary>
    Hearth,

    /// <summary>An earthquake overturning a settlement's hearths.</summary>
    Earthquake,

    /// <summary>A god's miracle.</summary>
    Miracle,
}
