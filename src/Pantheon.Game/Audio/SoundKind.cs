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
    /// <summary>Peace: a slow, open major pad.</summary>
    Calm,

    /// <summary>War, a rival near victory, or the ground shaking: a dark pulsing minor pad.</summary>
    Tension,

    /// <summary>The player's god dominates or has won: a bright arpeggio.</summary>
    Triumph,
}
