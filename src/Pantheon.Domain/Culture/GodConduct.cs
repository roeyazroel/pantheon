namespace Pantheon.Domain.Culture;

/// <summary>
/// What a god has done, as the world remembers it (spec §3.2, §11, ADR-024): weights that build
/// with its acts and fade slowly, from which its traits emerge.
/// </summary>
public sealed class GodConduct
{
    /// <summary>Help given to people in need.</summary>
    public required float Help { get; set; }

    /// <summary>Gifts given to people who did not need them.</summary>
    public required float Gifts { get; set; }

    /// <summary>Harm done to people.</summary>
    public required float Harm { get; set; }

    /// <summary>Harm done to the god's own followers.</summary>
    public required float Cruelty { get; set; }

    /// <summary>Harm done to the followers of gods that wronged it.</summary>
    public required float Vengeance { get; set; }

    /// <summary>Disasters the god set loose: fires, quakes and floods.</summary>
    public required float Upheaval { get; set; }

    /// <summary>Alliances broken and discord sown.</summary>
    public required float Deceit { get; set; }

    /// <summary>Peace made and alliances kept.</summary>
    public required float Faithfulness { get; set; }

    /// <summary>Wars declared.</summary>
    public required float Aggression { get; set; }

    /// <summary>A god that has done nothing yet.</summary>
    /// <returns>Empty conduct.</returns>
    public static GodConduct None() => new()
    {
        Help = 0f, Gifts = 0f, Harm = 0f, Cruelty = 0f, Vengeance = 0f, Upheaval = 0f, Deceit = 0f, Faithfulness = 0f, Aggression = 0f,
    };
}
