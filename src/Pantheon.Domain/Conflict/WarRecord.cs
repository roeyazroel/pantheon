namespace Pantheon.Domain.Conflict;

/// <summary>A war between two gods: who started it, for what, and how it goes (spec §44, ADR-026).</summary>
public sealed class WarRecord
{
    /// <summary>The god that declared it.</summary>
    public required int AggressorGodId { get; init; }

    /// <summary>The god it was declared on.</summary>
    public required int DefenderGodId { get; init; }

    /// <summary>What the aggressor fights for.</summary>
    public required WarGoal Goal { get; init; }

    /// <summary>The holy site, for <see cref="WarGoal.HolySite"/>.</summary>
    public required int? SiteId { get; init; }

    /// <summary>Tick it began.</summary>
    public required long StartedTick { get; init; }

    /// <summary>The aggressor's war score: what it has won.</summary>
    public required float AggressorScore { get; set; }

    /// <summary>The defender's war score.</summary>
    public required float DefenderScore { get; set; }

    /// <summary>Whether the aggressor has been told it may dictate peace.</summary>
    public required bool GoalAnnounced { get; set; }

    /// <summary>Whether a god is one of the two.</summary>
    /// <param name="godId">The god.</param>
    /// <returns><c>true</c> for a side.</returns>
    public bool Involves(int godId) => AggressorGodId == godId || DefenderGodId == godId;

    /// <summary>A side's score.</summary>
    /// <param name="godId">The god.</param>
    /// <returns>Its score.</returns>
    public float ScoreOf(int godId) => godId == AggressorGodId ? AggressorScore : DefenderScore;

    /// <summary>Adds to a side's score.</summary>
    /// <param name="godId">The god.</param>
    /// <param name="points">Points.</param>
    public void Add(int godId, float points)
    {
        if (godId == AggressorGodId)
        {
            AggressorScore += points;
        }
        else if (godId == DefenderGodId)
        {
            DefenderScore += points;
        }
    }
}
