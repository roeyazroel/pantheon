namespace Pantheon.Domain.Creatures;

/// <summary>A path a Creature may take up as it grows (spec §24, ADR-030).</summary>
public enum CreatureSpecialisation
{
    /// <summary>Shields its god's villages: disasters harm them less.</summary>
    Guardian,

    /// <summary>Fights harder.</summary>
    Warrior,

    /// <summary>Its attacks kill more and terrify.</summary>
    Destroyer,

    /// <summary>Heals the people of the villages it helps.</summary>
    Healer,

    /// <summary>Heals the land where it goes: harm, blight and wildlife.</summary>
    NatureKeeper,

    /// <summary>Wins the godless of the villages it visits to its god.</summary>
    Missionary,

    /// <summary>Sows doubt in rival gods among the villages it visits.</summary>
    Trickster,

    /// <summary>Its god's villages near it defend themselves better.</summary>
    Commander,
}

/// <summary>
/// How far a Creature's deeds have called it to each path (spec §24, ADR-030): every deed, fight and
/// reward adds to the paths it belongs to, and at the calling levels the Creature takes up the path
/// it has walked most.
/// </summary>
public sealed class CreatureCallings
{
    /// <summary>Towards <see cref="CreatureSpecialisation.Guardian"/>.</summary>
    public float Guardian { get; set; }

    /// <summary>Towards <see cref="CreatureSpecialisation.Warrior"/>.</summary>
    public float Warrior { get; set; }

    /// <summary>Towards <see cref="CreatureSpecialisation.Destroyer"/>.</summary>
    public float Destroyer { get; set; }

    /// <summary>Towards <see cref="CreatureSpecialisation.Healer"/>.</summary>
    public float Healer { get; set; }

    /// <summary>Towards <see cref="CreatureSpecialisation.NatureKeeper"/>.</summary>
    public float NatureKeeper { get; set; }

    /// <summary>Towards <see cref="CreatureSpecialisation.Missionary"/>.</summary>
    public float Missionary { get; set; }

    /// <summary>Towards <see cref="CreatureSpecialisation.Trickster"/>.</summary>
    public float Trickster { get; set; }

    /// <summary>Towards <see cref="CreatureSpecialisation.Commander"/>.</summary>
    public float Commander { get; set; }
}
