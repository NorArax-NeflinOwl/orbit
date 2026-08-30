using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orbit.Api.Diagnostics;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Diagnostics;
using Xunit;

namespace Orbit.Api.Tests.Diagnostics;

/// <summary>
/// How long an uploaded diagnostic log actually survives.
///
/// The window was swept only when somebody uploaded, on the reasoning that an upload is the only time
/// entries appear and therefore the only time there is anything to sweep. The first half is true; the
/// second does not follow. Entries age on their own, so a month with nobody reporting anything left the
/// month before it sitting there, and an account that stopped sending reports kept its logs forever -
/// which is the opposite of what a stated retention window is for.
/// </summary>
public sealed class DiagnosticLogRetentionTests
{
    [Fact]
    public async Task Logs_past_the_window_are_deleted_without_anybody_uploading()
    {
        var repository = new InMemoryDiagnosticLogRepository();
        await StoreAsync(repository, receivedDaysAgo: 31);

        await SweepAsync(repository, retentionDays: 30);

        Assert.Empty(repository.Entries);
    }

    [Fact]
    public async Task Logs_inside_the_window_are_left_alone()
    {
        var repository = new InMemoryDiagnosticLogRepository();
        await StoreAsync(repository, receivedDaysAgo: 29);

        await SweepAsync(repository, retentionDays: 30);

        Assert.Single(repository.Entries);
    }

    /// <summary>
    /// The number is configuration, not a constant: an operator who shortens the window has to see it
    /// take effect on what is already stored, not only on what arrives afterwards.
    /// </summary>
    [Fact]
    public async Task Shortening_the_window_applies_to_logs_already_stored()
    {
        var repository = new InMemoryDiagnosticLogRepository();
        await StoreAsync(repository, receivedDaysAgo: 10);

        await SweepAsync(repository, retentionDays: 7);

        Assert.Empty(repository.Entries);
    }

    private static Task StoreAsync(IDiagnosticLogRepository repository, int receivedDaysAgo)
        => repository.AddAsync(
            Guid.NewGuid(),
            new MobileDeviceInfo("1.0.0", "Android", "16", "Pixel 8"),
            [new DiagnosticLogEntry(DateTimeOffset.UtcNow.AddDays(-receivedDaysAgo), "Error", "It broke", null)],
            DateTimeOffset.UtcNow.AddDays(-receivedDaysAgo),
            CancellationToken.None);

    /// <summary>Runs one sweep - the service's own loop is a PeriodicTimer around exactly this call.</summary>
    private static Task SweepAsync(IDiagnosticLogRepository repository, int retentionDays)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repository);

        var service = new DiagnosticLogRetentionBackgroundService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new OptionsMonitorStub(new DiagnosticLogSettings { RetentionDays = retentionDays }),
            NullLogger<DiagnosticLogRetentionBackgroundService>.Instance);

        return service.SweepAsync(CancellationToken.None);
    }

    /// <summary>The one thing IOptionsMonitor is asked for here - the current value, never a change.</summary>
    private sealed class OptionsMonitorStub : IOptionsMonitor<DiagnosticLogSettings>
    {
        public OptionsMonitorStub(DiagnosticLogSettings value) => CurrentValue = value;

        public DiagnosticLogSettings CurrentValue { get; }

        public DiagnosticLogSettings Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<DiagnosticLogSettings, string?> listener) => null;
    }
}
