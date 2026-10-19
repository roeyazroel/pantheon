namespace Pantheon.Domain.Politics;

/// <summary>A treaty in force between two gods (spec §38–40, ADR-025).</summary>
public sealed class Treaty
{
    /// <summary>Unique id.</summary>
    public required int Id { get; init; }

    /// <summary>The kind of treaty.</summary>
    public required TreatyKind Kind { get; init; }

    /// <summary>The god that proposed it; the defender in a defensive pact.</summary>
    public required int FirstGodId { get; init; }

    /// <summary>The god that accepted it; the protected god in a defensive pact.</summary>
    public required int SecondGodId { get; init; }

    /// <summary>The god a joint war is against, or a defensive pact guards against (<c>null</c>: any aggressor).</summary>
    public required int? TargetGodId { get; init; }

    /// <summary>The god that pays a tribute; <c>null</c> for other treaties.</summary>
    public required int? PayerGodId { get; init; }

    /// <summary>Divine Power a tribute pays every minute.</summary>
    public required float Amount { get; init; }

    /// <summary>Whether only the two gods know of it; it stops being secret once revealed (spec §39).</summary>
    public required bool Secret { get; set; }

    /// <summary>Tick it was signed.</summary>
    public required long SignedTick { get; init; }

    /// <summary>Tick it ends; <c>null</c> when it holds until broken.</summary>
    public required long? EndsTick { get; init; }

    /// <summary>Whether a god is one of its two parties.</summary>
    /// <param name="godId">The god.</param>
    /// <returns><c>true</c> for a party.</returns>
    public bool Binds(int godId) => FirstGodId == godId || SecondGodId == godId;

    /// <summary>The other party.</summary>
    /// <param name="godId">One party.</param>
    /// <returns>The other.</returns>
    public int PartnerOf(int godId) => FirstGodId == godId ? SecondGodId : FirstGodId;
}
