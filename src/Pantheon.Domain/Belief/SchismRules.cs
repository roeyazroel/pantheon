namespace Pantheon.Domain.Belief;

/// <summary>
/// When followers break away from their god (spec §34, ADR-021), part of
/// <c>game/Data/divine_rules.json</c>. Rates are per belief step; shares are 0..1.
/// </summary>
public sealed record SchismRules
{
    /// <summary>Doubt of the majority's god at which its followers split.</summary>
    public required float Doubt { get; init; }

    /// <summary>Least adherence of the god for a schism: only a god that holds the settlement can lose it this way.</summary>
    public required float MinimumAdherence { get; init; }

    /// <summary>Least population of a settlement that can split.</summary>
    public required int MinimumPopulation { get; init; }

    /// <summary>Share of the god's followers who keep the Old Faith.</summary>
    public required float Share { get; init; }

    /// <summary>Share of the remaining followers' Doubt that leaves with the schismatics.</summary>
    public required float DoubtRelief { get; init; }

    /// <summary>
    /// Doubt a god plants in people who love it when it harms them, times their Love: the god's
    /// deeds contradict what they know of it.
    /// </summary>
    public required float BetrayalDoubt { get; init; }

    /// <summary>Love of the god at which the Old Faith returns to it.</summary>
    public required float ReconcileLove { get; init; }

    /// <summary>Doubt of the god at or below which the Old Faith may return to it.</summary>
    public required float ReconcileDoubt { get; init; }

    /// <summary>Share of the Old Faith that returns to the god per step once it is reconciled.</summary>
    public required float ReturnPerStep { get; init; }

    /// <summary>Share of the Old Faith that fades into unbelief per step otherwise.</summary>
    public required float FadePerStep { get; init; }

    /// <summary>Simulated seconds after a schism before the same settlement can split again.</summary>
    public required float CooldownSeconds { get; init; }
}
