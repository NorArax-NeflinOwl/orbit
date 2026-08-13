using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orbit.Api.HealthChecks;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Data;
using Xunit;

namespace Orbit.Api.Tests.HealthChecks;

public sealed class DatabaseHealthCheckTests
{
    private const string UnreachableConnectionString = "Data Source=/orbit-api-tests-unreachable-path/does-not-exist/orbit.db";

    [Fact]
    public async Task CheckHealthAsync_returns_healthy_without_connecting_when_disabled()
    {
        var settings = new HealthCheckSettings { Database = new DatabaseHealthCheckSettings { Enabled = false } };
        await using var dbContext = CreateDbContext(UnreachableConnectionString);
        var healthCheck = new DatabaseHealthCheck(dbContext, new TestOptionsMonitor<HealthCheckSettings>(settings));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Check disabled by configuration.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_database_is_reachable()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"orbit-health-check-tests-{Guid.NewGuid():N}.db");

        try
        {
            var settings = new HealthCheckSettings { Database = new DatabaseHealthCheckSettings { Enabled = true } };
            await using var dbContext = CreateDbContext($"Data Source={databasePath}");
            // The SQLite provider's CanConnectAsync only checks whether the file already exists (it opens
            // a read-only connection internally), so the file has to be created first, exactly like
            // Program.cs does with EnsureCreated() on startup.
            await dbContext.Database.EnsureCreatedAsync();
            var healthCheck = new DatabaseHealthCheck(dbContext, new TestOptionsMonitor<HealthCheckSettings>(settings));

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            // Disposing the DbContext returns its connection to Microsoft.Data.Sqlite's pool instead of
            // releasing the file handle, which makes File.Delete fail on Windows with a sharing violation
            // unless the pool is cleared first.
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_returns_unhealthy_when_database_is_unreachable()
    {
        var settings = new HealthCheckSettings { Database = new DatabaseHealthCheckSettings { Enabled = true } };
        await using var dbContext = CreateDbContext(UnreachableConnectionString);
        var healthCheck = new DatabaseHealthCheck(dbContext, new TestOptionsMonitor<HealthCheckSettings>(settings));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static OrbitDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<OrbitDbContext>().UseSqlite(connectionString).Options;
        return new OrbitDbContext(options);
    }
}
