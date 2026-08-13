using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Orbit.Api.HealthChecks;

/// <summary>
/// Warns when the drive hosting the SQLite database is close to running out of space, since SQLite
/// writes fail ungracefully once the disk is full.
/// </summary>
public sealed class DiskSpaceHealthCheck(IConfiguration configuration, IOptionsMonitor<HealthCheckSettings> settings) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var currentSettings = settings.CurrentValue.DiskSpace;
        if (!currentSettings.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Check disabled by configuration."));
        }

        var connectionString = configuration.GetConnectionString("Orbit") ?? "Data Source=orbit.db";
        var databasePath = ExtractDatabasePath(connectionString);
        var driveRoot = Path.GetPathRoot(Path.GetFullPath(databasePath));

        if (string.IsNullOrEmpty(driveRoot))
        {
            return Task.FromResult(HealthCheckResult.Degraded("Could not resolve the drive hosting the database."));
        }

        var drive = new DriveInfo(driveRoot);
        var data = new Dictionary<string, object>
        {
            ["freeBytes"] = drive.AvailableFreeSpace,
            ["minimumFreeBytes"] = currentSettings.MinimumFreeBytes
        };

        return Task.FromResult(drive.AvailableFreeSpace >= currentSettings.MinimumFreeBytes
            ? HealthCheckResult.Healthy($"{drive.AvailableFreeSpace / (1024 * 1024)} MB free.", data)
            : HealthCheckResult.Unhealthy($"Only {drive.AvailableFreeSpace / (1024 * 1024)} MB free.", data: data));
    }

    /// <summary>Pulls the file path out of a SQLite "Data Source=..." connection string.</summary>
    private static string ExtractDatabasePath(string connectionString)
    {
        const string dataSourcePrefix = "Data Source=";
        var dataSourceStart = connectionString.IndexOf(dataSourcePrefix, StringComparison.OrdinalIgnoreCase);
        return dataSourceStart < 0
            ? "orbit.db"
            : connectionString[(dataSourceStart + dataSourcePrefix.Length)..].Split(';')[0].Trim();
    }
}
