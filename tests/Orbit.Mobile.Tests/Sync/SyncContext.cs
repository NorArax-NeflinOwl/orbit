using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>A phone with a local database, and a server it can sometimes reach.</summary>
internal sealed class SyncContext : IDisposable
{
    private readonly LocalStore _localStore = new();

    public SyncContext()
    {
        Clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        Server = new FakeNotesServer(Clock);
        Notes = new LocalNoteRepository(_localStore, Clock);
        Synchronizer = new NoteSynchronizer(
            _localStore, new NotesClient(Server.ToHttpClient()), Clock,
            NullLogger<NoteSynchronizer>.Instance);
    }

    public FakeTimeProvider Clock { get; }
    public FakeNotesServer Server { get; }
    public LocalNoteRepository Notes { get; }
    public NoteSynchronizer Synchronizer { get; }
    /// <summary>A fresh look at the database, as the next screen or the next launch would see it.</summary>
    public OrbitLocalDbContext DbContext => _localStore.CreateDbContext();

    public Task<SyncResult> SynchroniseAsync() => Synchronizer.SynchroniseAsync(CancellationToken.None);

    /// <summary>Requests the phone made, stripped to just the ones that change something.</summary>
    public IReadOnlyList<string> WriteRequests()
        => Server.ReceivedRequests.Where(request => !request.Contains("/changes")).ToList();

    public void GoOffline() => Server.IsUnreachable = true;

    public void ComeBackOnline() => Server.IsUnreachable = false;

    public void Dispose()
    {
        Server.Dispose();
        _localStore.Dispose();
    }
}
