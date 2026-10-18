namespace Pantheon.Domain.Miracles;

/// <summary>The need a miracle answers. People who needed it feel more Love for the god who helped.</summary>
public enum MiracleNeed
{
    /// <summary>The miracle answers no need; Love is not scaled.</summary>
    None,

    /// <summary>Hunger: the emptier the stores, the greater the need.</summary>
    Food,

    /// <summary>Illness: the lower the health, the greater the need.</summary>
    Health,

    /// <summary>Drought: the drier the farmland, the greater the need.</summary>
    Water,

    /// <summary>Danger: fire on the farmland or the ground shaking.</summary>
    Danger,
}
