using System;

namespace Pantheon.Domain.Society;

/// <summary>How far a settlement has come in each field of technology (spec §36, ADR-023): 0 up to the rules' cap.</summary>
public sealed class TechnologyLevels
{
    /// <summary>Farming.</summary>
    public required float Agriculture { get; set; }

    /// <summary>Building.</summary>
    public required float Architecture { get; set; }

    /// <summary>Healing.</summary>
    public required float Medicine { get; set; }

    /// <summary>Arms.</summary>
    public required float Warfare { get; set; }

    /// <summary>Roads and boats.</summary>
    public required float Navigation { get; set; }

    /// <summary>Works.</summary>
    public required float Engineering { get; set; }

    /// <summary>Learning.</summary>
    public required float Science { get; set; }

    /// <summary>Rites.</summary>
    public required float Religion { get; set; }

    /// <summary>A settlement's starting technology: none in any field.</summary>
    /// <returns>New levels, all 0.</returns>
    public static TechnologyLevels None() => new()
    {
        Agriculture = 0f, Architecture = 0f, Medicine = 0f, Warfare = 0f, Navigation = 0f, Engineering = 0f, Science = 0f, Religion = 0f,
    };

    /// <summary>The level of one field.</summary>
    /// <param name="field">The field.</param>
    /// <returns>Its level.</returns>
    public float Get(TechnologyField field) => field switch
    {
        TechnologyField.Agriculture => Agriculture,
        TechnologyField.Architecture => Architecture,
        TechnologyField.Medicine => Medicine,
        TechnologyField.Warfare => Warfare,
        TechnologyField.Navigation => Navigation,
        TechnologyField.Engineering => Engineering,
        TechnologyField.Science => Science,
        TechnologyField.Religion => Religion,
    };

    /// <summary>Sets the level of one field.</summary>
    /// <param name="field">The field.</param>
    /// <param name="level">Its new level.</param>
    public void Set(TechnologyField field, float level)
    {
        switch (field)
        {
            case TechnologyField.Agriculture: Agriculture = level; break;
            case TechnologyField.Architecture: Architecture = level; break;
            case TechnologyField.Medicine: Medicine = level; break;
            case TechnologyField.Warfare: Warfare = level; break;
            case TechnologyField.Navigation: Navigation = level; break;
            case TechnologyField.Engineering: Engineering = level; break;
            case TechnologyField.Science: Science = level; break;
            case TechnologyField.Religion: Religion = level; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown technology field.");
        }
    }
}
