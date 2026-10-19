namespace Pantheon.Game.Hud;

/// <summary>One button of the miracle bar.</summary>
public sealed record MiracleSlot
{
    /// <summary>Key that selects it: 1–9, then 10, 11 and 12 for the keys 0, - and = (ADR-021); 0 when it has none.</summary>
    public required int Hotkey { get; init; }

    /// <summary>Miracle id.</summary>
    public required string MiracleId { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Tooltip: description, base cost, radius, cooldown and requirements.</summary>
    public required string Tooltip { get; init; }

    /// <summary>Cost at full influence, less for a miracle of the god's own domains.</summary>
    public required float BaseCost { get; init; }

    /// <summary>Whether it is the selected miracle.</summary>
    public required bool Selected { get; init; }

    /// <summary>Whether the god's rank is too low for it.</summary>
    public required bool Locked { get; init; }

    /// <summary>Seconds until it is ready; 0 when ready.</summary>
    public required double CooldownSeconds { get; init; }

    /// <summary>Whether the god has the power for it at full influence.</summary>
    public required bool Affordable { get; init; }
}
