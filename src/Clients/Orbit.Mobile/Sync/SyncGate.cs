using System.Collections.Concurrent;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Stops two synchronisation runs for the same entity type overlapping, by making the second wait rather
/// than run alongside.
///
/// Overlapping is not a tidiness problem. Each run loads the outbox into its own database context, so two
/// at once both see the same queued create, both find its row's server id still null, and both send it -
/// two items on the server out of one. The milder version is two runs deleting the same queued entry,
/// where the second finds nothing to delete and EF throws.
///
/// <b>Waiting, not dropping.</b> Dropping was the first attempt and was wrong: a screen that queues a
/// change and then asks for a sync would have that request thrown away whenever a sync started slightly
/// earlier was still running - and that earlier run began before the change existed, so nothing sent it.
/// The change then sat queued while the screen said "Synced". Serialising costs a short wait; dropping
/// cost correctness.
/// </summary>
public sealed class SyncGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public async Task<TResult> RunAsync<TResult>(
        string entityType, Func<Task<TResult>> run, CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(entityType, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

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
