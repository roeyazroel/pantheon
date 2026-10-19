using System.Collections.Generic;

namespace Pantheon.Domain.Migration;

/// <summary>People on the move between settlements (spec §31, §82, ADR-016).</summary>
public sealed class MigrationState
{
    /// <summary>Groups on the road, ordered by id.</summary>
    public required List<MigrationGroup> Groups { get; init; }

    /// <summary>Id the next group will get.</summary>
    public required int NextGroupId { get; set; }
}
