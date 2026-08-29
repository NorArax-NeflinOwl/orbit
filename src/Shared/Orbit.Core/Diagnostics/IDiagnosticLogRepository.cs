namespace Orbit.Core.Diagnostics;

public interface IDiagnosticLogRepository
{
    /// <summary>Stores one upload's worth of entries, all sharing the same device information.</summary>
    Task AddAsync(
        Guid userId, MobileDeviceInfo device, IReadOnlyList<DiagnosticLogEntry> entries, DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Drops entries received before <paramref name="olderThanUtc"/>, and returns how many went. Called on
    /// upload rather than from a background service: diagnostic logs only arrive when someone sends them,
    /// so that is exactly when there is something to clean up, and it keeps retention from needing a
    /// timer of its own.
    /// </summary>
    Task<int> DeleteReceivedBeforeAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken);
}
