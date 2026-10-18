namespace Pantheon.Domain.Creatures;

/// <summary>A Creature's needs (spec §111), each from 0 (satisfied) to 1 (desperate).</summary>
public sealed class CreatureNeeds
{
    /// <summary>Hunger: rises over time, eased by eating.</summary>
    public required float Hunger { get; set; }

    /// <summary>Fatigue: rises over time and with work, eased by rest.</summary>
    public required float Fatigue { get; set; }

    /// <summary>Boredom: rises over time, eased by play and new places.</summary>
    public required float Boredom { get; set; }
}
