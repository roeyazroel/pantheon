namespace Pantheon.Domain.Gods;

/// <summary>When a god may cast a miracle again.</summary>
public sealed class MiracleCooldown
{
    /// <summary>Id of the miracle (<c>miracles.json</c>).</summary>
    public required string MiracleId { get; init; }

    /// <summary>First tick at which the miracle can be cast again.</summary>
    public required long ReadyAtTick { get; set; }
}
