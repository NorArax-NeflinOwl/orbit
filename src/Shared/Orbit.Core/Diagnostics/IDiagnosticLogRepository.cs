namespace Orbit.Core.Diagnostics;

public interface IDiagnosticLogRepository
{
    /// <summary>Stores one upload's worth of entries, all sharing the same device information.</summary>
    Task AddAsync(
        Guid userId, MobileDeviceInfo device, IReadOnlyList<DiagnosticLogEntry> entries, DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Drops entries received before <paramref name="olderThanUtc"/>, and returns how many went. Called
    /// hourly by DiagnosticLogRetentionBackgroundService and again on each upload. The upload call alone
    /// used to be the whole of it, on the reasoning that logs only arrive when somebody sends them - but
    /// they age without anyone sending anything, so an account that stopped reporting kept its logs.
    /// </summary>
    Task<int> DeleteReceivedBeforeAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken);
}
