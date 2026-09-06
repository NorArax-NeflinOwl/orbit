namespace Orbit.Data.Entities;

/// <summary>
/// One rate-limiting window for one caller, shared by every API instance - see
/// Orbit.Api.RateLimiting.PostgresRateLimitWindows, which is the only thing that reads or writes it.
///
/// It is an entity so that the schema is part of the model and a migration manages it. Nothing queries
/// it through EF: taking a permit has to be one atomic statement, and read-then-write through a change
/// tracker is exactly the race two replicas would lose.
/// </summary>
public sealed class RateLimitWindowEntity
{
    /// <summary>Which policy and which caller, joined - see RateLimiterPolicies for how it is built.</summary>
    public string Partition { get; set; } = string.Empty;

    /// <summary>The start of the window this count belongs to, truncated to the window length.</summary>
    public DateTimeOffset WindowStart { get; set; }

    public int Count { get; set; }
}
