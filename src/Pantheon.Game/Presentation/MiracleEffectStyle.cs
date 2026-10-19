namespace Pantheon.Game.Presentation;

/// <summary>How a miracle looks when it is cast (ADR-055 adds the signature looks after <see cref="Bolt"/>).</summary>
public enum MiracleEffectStyle
{
    /// <summary>Motes falling from the sky onto the target.</summary>
    Shower,

    /// <summary>Motes rising from the ground at the target.</summary>
    Rise,

    /// <summary>A ring of light spreading over the radius.</summary>
    Wave,

    /// <summary>A bolt from the sky with a flickering flash (also the weather's lightning).</summary>
    Bolt,

    /// <summary>A god's bolt: a bolt from the sky with a flash, a shock ring and sparks.</summary>
    Strike,

    /// <summary>A dark cloud gathers and rain pours from it.</summary>
    Rain,

    /// <summary>A golden pillar, and grain falling about it.</summary>
    Harvest,

    /// <summary>A spiral of light rising under a soft dome.</summary>
    Healing,

    /// <summary>A burst of flames, embers and smoke.</summary>
    Flames,

    /// <summary>The ground heaves: a dust burst and a shock ring.</summary>
    Quake,

    /// <summary>A dome of light rising over the radius.</summary>
    Shield,

    /// <summary>A fountain of water springing up.</summary>
    Fountain,

    /// <summary>A pillar of light from the sky, and motes rising into it.</summary>
    Holy,

    /// <summary>A dark swirl, and shadows rising.</summary>
    Shadow,

    /// <summary>A storm cloud wheeling over the target.</summary>
    Storm,
}
