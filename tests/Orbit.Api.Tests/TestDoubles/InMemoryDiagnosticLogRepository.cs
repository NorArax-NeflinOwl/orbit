using Orbit.Core.Diagnostics;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>Uploaded log entries, kept in memory with the received time the sweep reads.</summary>
internal sealed class InMemoryDiagnosticLogRepository : IDiagnosticLogRepository
{
    private readonly List<(DateTimeOffset ReceivedAtUtc, DiagnosticLogEntry Entry)> _entries = [];

    public IReadOnlyList<DiagnosticLogEntry> Entries => [.. _entries.Select(stored => stored.Entry)];

    public Task AddAsync(
        Guid userId, MobileDeviceInfo device, IReadOnlyList<DiagnosticLogEntry> entries,
        DateTimeOffset receivedAtUtc, CancellationToken cancellationToken)
    {
        _entries.AddRange(entries.Select(entry => (receivedAtUtc, entry)));
        return Task.CompletedTask;
    }

    public Task<int> DeleteReceivedBeforeAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken)
    {
        var removed = _entries.RemoveAll(stored => stored.ReceivedAtUtc < olderThanUtc);
        return Task.FromResult(removed);
    }
}
