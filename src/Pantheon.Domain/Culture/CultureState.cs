using System;

namespace Pantheon.Domain.Culture;

/// <summary>
/// A settlement's culture (spec §33, §35, ADR-024): how strongly it holds each doctrine, shaped
/// slowly by what its gods do and what it lives through, and how far it has turned from the gods.
/// </summary>
public sealed class CultureState
{
    /// <summary>Hold of pacifism (0..1).</summary>
    public required float Pacifism { get; set; }

    /// <summary>Hold of militarism (0..1).</summary>
    public required float Militarism { get; set; }

    /// <summary>Hold of sacrifice (0..1).</summary>
    public required float Sacrifice { get; set; }

    /// <summary>Hold of charity (0..1).</summary>
    public required float Charity { get; set; }

    /// <summary>Hold of nature worship (0..1).</summary>
    public required float NatureWorship { get; set; }

    /// <summary>Hold of knowledge seeking (0..1).</summary>
    public required float KnowledgeSeeking { get; set; }

    /// <summary>Hold of expansionism (0..1).</summary>
    public required float Expansionism { get; set; }

    /// <summary>Hold of isolationism (0..1).</summary>
    public required float Isolationism { get; set; }

    /// <summary>How far it has turned from the gods (0 = devout, 1 = wholly against them).</summary>
    public required float Secularism { get; set; }

    /// <summary>A new settlement's culture: no doctrine yet, and devout.</summary>
    /// <returns>The culture.</returns>
    public static CultureState Founded() => new()
    {
        Pacifism = 0f, Militarism = 0f, Sacrifice = 0f, Charity = 0f, NatureWorship = 0f, KnowledgeSeeking = 0f, Expansionism = 0f, Isolationism = 0f, Secularism = 0f,
    };

    /// <summary>The hold of one doctrine.</summary>
    /// <param name="doctrine">The doctrine.</param>
    /// <returns>Its hold (0..1).</returns>
    public float Get(Doctrine doctrine) => doctrine switch
    {
        Doctrine.Pacifism => Pacifism,
        Doctrine.Militarism => Militarism,
        Doctrine.Sacrifice => Sacrifice,
        Doctrine.Charity => Charity,
        Doctrine.NatureWorship => NatureWorship,
        Doctrine.KnowledgeSeeking => KnowledgeSeeking,
        Doctrine.Expansionism => Expansionism,
        Doctrine.Isolationism => Isolationism,
    };

    /// <summary>Sets the hold of one doctrine.</summary>
    /// <param name="doctrine">The doctrine.</param>
    /// <param name="hold">Its new hold (0..1).</param>
    public void Set(Doctrine doctrine, float hold)
    {
        switch (doctrine)
        {
            case Doctrine.Pacifism: Pacifism = hold; break;
            case Doctrine.Militarism: Militarism = hold; break;
            case Doctrine.Sacrifice: Sacrifice = hold; break;
            case Doctrine.Charity: Charity = hold; break;
            case Doctrine.NatureWorship: NatureWorship = hold; break;
            case Doctrine.KnowledgeSeeking: KnowledgeSeeking = hold; break;
            case Doctrine.Expansionism: Expansionism = hold; break;
            case Doctrine.Isolationism: Isolationism = hold; break;
            default: throw new ArgumentOutOfRangeException(nameof(doctrine), doctrine, "Unknown doctrine.");
        }
    }
}
