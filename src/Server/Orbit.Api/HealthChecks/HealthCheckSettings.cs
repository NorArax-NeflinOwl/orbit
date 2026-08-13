namespace Orbit.Api.HealthChecks;

/// <summary>
/// Root configuration for all health checks, bound from the "HealthChecks" section of appsettings.json.
/// Read through <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/>, which reloads
/// automatically when the configuration file changes on disk, so enabling/disabling a check or editing
/// the external service list takes effect on the next probe without restarting the API.
/// </summary>
public sealed class HealthCheckSettings
{
    public DatabaseHealthCheckSettings Database { get; set; } = new();
    public DiskSpaceHealthCheckSettings DiskSpace { get; set; } = new();
    public ExternalServicesHealthCheckSettings ExternalServices { get; set; } = new();
    public HostedServicesHealthCheckSettings HostedServices { get; set; } = new();
}

public sealed class DatabaseHealthCheckSettings
{
    public bool Enabled { get; set; } = true;
}

public sealed class DiskSpaceHealthCheckSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Minimum free space on the drive hosting the SQLite database before the check turns unhealthy.</summary>
    public long MinimumFreeBytes { get; set; } = 100 * 1024 * 1024;
}

public sealed class ExternalServicesHealthCheckSettings
{
    public bool Enabled { get; set; } = true;
    public List<ExternalServiceEndpoint> Services { get; set; } = [];
}

/// <summary>
/// One externally reachable dependency to probe. <see cref="Name"/> also identifies it for the
/// GET /health/services/{name} endpoint, which can check it on demand regardless of <see cref="Enabled"/>.
/// </summary>
public sealed class ExternalServiceEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int TimeoutMs { get; set; } = 5000;
}

public sealed class HostedServicesHealthCheckSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>How long a background service can go without a heartbeat before it's reported unhealthy.</summary>
    public int StaleAfterSeconds { get; set; } = 120;
}
