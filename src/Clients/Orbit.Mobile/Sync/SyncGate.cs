using System.Collections.Concurrent;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Stops two synchronisation runs for the same entity type overlapping.
///
/// Not a tidiness measure. Each run loads the outbox into its own database context, so two overlapping
/// runs both see the same queued create, both find its row's server id still null, and both send it -
/// producing two items on the server out of one. The milder version of the same race is two runs
/// deleting the same queued entry, where the second finds nothing to delete and EF throws.
///
/// A second run that arrives while one is in progress is dropped rather than queued: it would ask the
/// server the same question the run in flight is already asking, and the app synchronises on a timer, on
/// screen open and on pull-to-refresh, so another chance is never far away.
/// </summary>
public sealed class SyncGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public async Task<TResult> RunAsync<TResult>(
        string entityType, Func<Task<TResult>> run, TResult whenAlreadyRunning)
    {
        var gate = _gates.GetOrAdd(entityType, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(TimeSpan.Zero))
        {
            return whenAlreadyRunning;
        }

        try
        {
            return await run();
        }
        finally
        {
            gate.Release();
        }
    }
}
