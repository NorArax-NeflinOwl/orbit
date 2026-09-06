using Npgsql;

namespace Orbit.Api.RateLimiting;

/// <summary>
/// Counts attempts in OS_RATE_LIMITS, so the budget is one budget however many replicas answer.
///
/// PostgreSQL rather than Redis because the database is already here - the same argument the instance
/// notice bus makes. The difference is that this one may not lose anything, so it is a table and a
/// statement rather than a notification: a rate limit that forgets is not a rate limit.
/// </summary>
public sealed class PostgresRateLimitWindows(
    NpgsqlDataSource dataSource,
    ILogger<PostgresRateLimitWindows> logger) : IRateLimitWindows
{
    /// <summary>
    /// The increment is the insert. ON CONFLICT makes the second and later attempts in a window update
    /// the row they collided with, inside the same statement and the same row lock, so the count cannot
    /// be read stale by a replica that is about to write it back.
    /// </summary>
    private const string TakeOnePermit = """
        INSERT INTO "OS_RATE_LIMITS" ("OS_RL_PARTITION", "OS_RL_WINDOWSTART", "OS_RL_COUNT")
        VALUES (@partition, @windowStart, 1)
        ON CONFLICT ("OS_RL_PARTITION", "OS_RL_WINDOWSTART")
        DO UPDATE SET "OS_RL_COUNT" = "OS_RATE_LIMITS"."OS_RL_COUNT" + 1
        RETURNING "OS_RL_COUNT"
        """;

    public async Task<bool> TryTakeAsync(
        string partition, DateTimeOffset windowStart, int permitLimit, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(TakeOnePermit, connection);
            command.Parameters.AddWithValue("partition", partition);
            command.Parameters.AddWithValue("windowStart", windowStart);

            var spent = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            return spent <= permitLimit;
        }
        catch (Exception exception)
        {
            // Allowed rather than refused, and the reasoning is worth stating because the instinct runs
            // the other way for a security control. This shared count is the *second* of two gates: the
            // caller has already passed a per-instance limiter with the same budget, which is the whole
            // of what protected these endpoints before this class existed. Failing open therefore falls
            // back to that, and never below it.
            //
            // Failing closed would answer 429 to every sign-in and every shared link the moment the
            // database hiccups - and every one of these endpoints needs that same database to do its
            // work, so a caller who got through would be met by a 500 regardless. Turning a database
            // problem into a lockout buys nothing and costs the one thing the limiter is there to
            // protect: people being able to get in.
            logger.LogWarning(
                exception,
                "Could not reach the shared rate limit window for {Partition}. Falling back to this "
                + "instance's own limiter for that attempt.",
                partition);

            return true;
        }
    }

    public async Task<int> DeleteWindowsClosedBeforeAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """DELETE FROM "OS_RATE_LIMITS" WHERE "OS_RL_WINDOWSTART" < @cutoff""", connection);
        command.Parameters.AddWithValue("cutoff", cutoff);

        // Not caught here, unlike taking a permit: a failed sweep is the background service's problem to
        // log and retry on its next tick, and swallowing it would hide a table quietly growing forever.
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
