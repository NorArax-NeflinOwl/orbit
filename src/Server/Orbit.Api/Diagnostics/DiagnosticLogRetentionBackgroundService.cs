using Microsoft.Extensions.Options;
using Orbit.Core.Diagnostics;

namespace Orbit.Api.Diagnostics;

/// <summary>
/// Deletes uploaded diagnostic logs once they pass the retention window (see
/// <see cref="DiagnosticLogSettings.RetentionDays"/>).
///
/// The upload endpoint sweeps too, and that used to be the only thing that did - on the reasoning that
/// an upload is the only time entries appear and therefore the only time there is anything to sweep.
/// The first half is true and the second does not follow: entries age whether or not anyone uploads. A
/// month with no reports left every entry from the month before it sitting there, and an app that
/// stopped being used kept its logs forever. Retention that only runs when new data arrives is not
/// retention.
///
/// Hourly, and reporting no heartbeat to HostedServiceHealthTracker, for the same reasons
/// NotificationRetentionBackgroundService gives: the window is measured in days, and a missed sweep
/// costs nothing anybody can see.
/// </summary>
public sealed class DiagnosticLogRetentionBackgroundService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptionsMonitor<DiagnosticLogSettings> _settings;
    private readonly ILogger<DiagnosticLogRetentionBackgroundService> _logger;

    public DiagnosticLogRetentionBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IOptionsMonitor<DiagnosticLogSettings> settings,
        ILogger<DiagnosticLogRetentionBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A single failed sweep must not stop this background service - the next tick tries again.
                _logger.LogError(exception, "Failed to delete expired diagnostic log entries");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One sweep. Public so a test can run it without waiting an hour for the timer - what is worth
    /// covering here is the rule, not the PeriodicTimer around it.
    /// </summary>
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDiagnosticLogRepository>();

        var deletedCount = await repository.DeleteReceivedBeforeAsync(
            DateTimeOffset.UtcNow - TimeSpan.FromDays(_settings.CurrentValue.RetentionDays), cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {DeletedCount} expired diagnostic log entries", deletedCount);
        }
    }
}
