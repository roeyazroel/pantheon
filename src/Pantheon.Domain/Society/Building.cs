namespace Pantheon.Domain.Society;

/// <summary>A building a settlement has raised (ADR-023).</summary>
public sealed class Building
{
    /// <summary>What it is.</summary>
    public required BuildingKind Kind { get; init; }

    /// <summary>The god a temple is raised to; <c>null</c> for any other building.</summary>
    public required int? GodId { get; init; }
}
