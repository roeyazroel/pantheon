namespace Pantheon.Game.Audio;

/// <summary>The game's sounds (spec §60, ADR-021), synthesized from code rather than recorded.</summary>
public enum SoundKind
{
    /// <summary>A bell-like glimmer: a kind miracle.</summary>
    Chime,

    /// <summary>A rising shimmer: rain called, a domain taken up.</summary>
    Shimmer,

    /// <summary>A sharp crack and its echo: lightning or a spear from the sky.</summary>
    Bolt,

    /// <summary>Distant rolling thunder: a storm over a village.</summary>
    Thunder,

    /// <summary>A deep, shaking rumble: an earthquake.</summary>
    Rumble,

    /// <summary>Popping, hissing flames: a fire.</summary>
    Crackle,

    /// <summary>A Creature's roar: a fight.</summary>
    Roar,

    /// <summary>A soft two-note ping: news.</summary>
    Alert,

    /// <summary>A low falling call: bad news.</summary>
    Warning,

    /// <summary>A rising major arpeggio: a rank, a holy site, a victory.</summary>
    Fanfare,

    /// <summary>A looping wind.</summary>
    Wind,

    /// <summary>A looping rain.</summary>
    Rain,
}

/// <summary>The music's mood (spec §60's dynamic music).</summary>
public enum MusicMood
{
    /// <summary>The title screen: the game's main theme (ADR-071).</summary>
    Theme,

    /// <summary>Peace: a pastoral piece for strings, harp and flute.</summary>
    Calm,

    /// <summary>Peace under a cruel god: low strings, a low choir and a mournful oboe.</summary>
    Dark,

    /// <summary>A rival near victory, or a foe's Creature in the player's lands: an ostinato under swelling horns.</summary>
    Tension,

    /// <summary>War, or the player's Creature fighting: drums, driving strings, brass and choir.</summary>
    War,

    /// <summary>The land itself rising against the people: an earthquake, an eruption, a tornado, the beast.</summary>
    Disaster,

    /// <summary>The player's god dominates or has won: a fanfare for full orchestra.</summary>
    Triumph,
}
