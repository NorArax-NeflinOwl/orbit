using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Orbit.Api.RateLimiting;
using Xunit;

namespace Orbit.Api.Tests.RateLimiting;

/// <summary>
/// The part only a real PostgreSQL can answer: is the count actually one count, and is spending from it
/// atomic?
///
/// A fake store cannot prove either. The whole mechanism rests on ON CONFLICT ... DO UPDATE being one
/// statement under one row lock - two replicas asking at the same moment must not both read four spent
/// and both write five - and that is the database's behaviour, not this code's.
///
/// **It does nothing unless ORBIT_TEST_POSTGRES names a database**, so `dotnet test` stays a suite that
/// needs no services. That also means a green suite is not evidence about any of this; the command in
/// PostgresLiveUpdateBackplaneTests, pointed at this filter, is.
/// </summary>
public sealed class PostgresRateLimitWindowsTests
{
    private static string? ConnectionString => Environment.GetEnvironmentVariable("ORBIT_TEST_POSTGRES");

    private const int PermitLimit = 5;

    /// <summary>
    /// Two stores are two API instances. Between them they may spend the budget once, which is the
    /// difference this whole change exists to make.
    /// </summary>
    [Fact]
    public async Task Two_instances_spend_from_one_count()
    {
        if (ConnectionString is null)
        {
            return;
        }

        await using var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var first = WindowsOn(dataSource);
        var second = WindowsOn(dataSource);
        var partition = NewPartition();
        var window = DateTimeOffset.UtcNow;

        for (var attempt = 0; attempt < PermitLimit; attempt++)
        {
            var store = attempt % 2 == 0 ? first : second;
            Assert.True(await store.TryTakeAsync(partition, window, PermitLimit, CancellationToken.None));
        }

        Assert.False(await first.TryTakeAsync(partition, window, PermitLimit, CancellationToken.None));
        Assert.False(await second.TryTakeAsync(partition, window, PermitLimit, CancellationToken.None));
    }

    /// <summary>
    /// Concurrent attempts are the case the row lock exists for. Twenty at once against a budget of five
    /// must grant exactly five - a read-then-write would grant more, and would do it only sometimes,
    /// which is the worst way for a limit to be wrong.
    /// </summary>
    [Fact]
    public async Task Spending_at_the_same_moment_still_grants_the_budget_exactly_once()
    {
        if (ConnectionString is null)
        {
            return;
        }

        await using var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var partition = NewPartition();
        var window = DateTimeOffset.UtcNow;

        var attempts = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            WindowsOn(dataSource).TryTakeAsync(partition, window, PermitLimit, CancellationToken.None)));

        Assert.Equal(PermitLimit, attempts.Count(granted => granted));
    }

    [Fact]
    public async Task A_new_window_starts_with_the_whole_budget()
    {
        if (ConnectionString is null)
        {
            return;
        }

        await using var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var windows = WindowsOn(dataSource);
        var partition = NewPartition();
        var earlier = DateTimeOffset.UtcNow.AddMinutes(-5);

        for (var attempt = 0; attempt < PermitLimit; attempt++)
        {
            await windows.TryTakeAsync(partition, earlier, PermitLimit, CancellationToken.None);
        }

        Assert.False(await windows.TryTakeAsync(partition, earlier, PermitLimit, CancellationToken.None));
        Assert.True(await windows.TryTakeAsync(
            partition, DateTimeOffset.UtcNow, PermitLimit, CancellationToken.None));
    }

    /// <summary>
    /// The policy name is part of the partition precisely so that opening shared links cannot spend
    /// somebody's budget for signing in - both policies count into this one table.
    /// </summary>
    [Fact]
    public async Task One_callers_budget_is_not_another_callers()
    {
        if (ConnectionString is null)
        {
            return;
        }

        await using var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var windows = WindowsOn(dataSource);
        var window = DateTimeOffset.UtcNow;
        var spent = NewPartition();
        var untouched = NewPartition();

        for (var attempt = 0; attempt <= PermitLimit; attempt++)
        {
            await windows.TryTakeAsync(spent, window, PermitLimit, CancellationToken.None);
        }

        Assert.False(await windows.TryTakeAsync(spent, window, PermitLimit, CancellationToken.None));
        Assert.True(await windows.TryTakeAsync(untouched, window, PermitLimit, CancellationToken.None));
    }

    /// <summary>
    /// Without the sweep the table keeps a row per caller per minute for the life of the installation -
    /// see RateLimitWindowRetentionBackgroundService.
    /// </summary>
    [Fact]
    public async Task Closed_windows_are_swept_and_open_ones_are_left_alone()
    {
        if (ConnectionString is null)
        {
            return;
        }

        await using var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var windows = WindowsOn(dataSource);
        var partition = NewPartition();
        var longClosed = DateTimeOffset.UtcNow.AddHours(-3);
        var current = DateTimeOffset.UtcNow;

        await windows.TryTakeAsync(partition, longClosed, PermitLimit, CancellationToken.None);
        await windows.TryTakeAsync(partition, current, PermitLimit, CancellationToken.None);

        var deleted = await windows.DeleteWindowsClosedBeforeAsync(
            DateTimeOffset.UtcNow.AddHours(-1), CancellationToken.None);
        Assert.True(deleted >= 1);

        // The swept window is gone, so its budget starts over; the current one still remembers.
        Assert.True(await windows.TryTakeAsync(
            partition, longClosed, permitLimit: 1, CancellationToken.None));
        Assert.False(await windows.TryTakeAsync(
            partition, current, permitLimit: 1, CancellationToken.None));
    }

    private static PostgresRateLimitWindows WindowsOn(NpgsqlDataSource dataSource)
        => new(dataSource, NullLogger<PostgresRateLimitWindows>.Instance);

    /// <summary>Unique per test, so a rerun against the same database does not inherit its own counts.</summary>
    private static string NewPartition() => $"test:{Guid.NewGuid()}";
}
