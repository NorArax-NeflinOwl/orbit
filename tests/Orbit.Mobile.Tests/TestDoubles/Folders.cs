using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A folder synchroniser for the screens whose tests are not about folders reaching the server. It can
/// reach nobody, which leaves every folder exactly where the test put it - a folder is made on the
/// phone before anything is asked of a server, and the outbox is what carries it there afterwards.
/// </summary>
internal static class Folders
{
    internal static FolderSynchronizer SynchronizerAgainstNobody(LocalStore localStore, TimeProvider clock)
        => new(
            localStore, new FoldersClient(StubHttpMessageHandler.Unreachable().ToHttpClient()), clock,
            new SyncGate(), NullLogger<FolderSynchronizer>.Instance);

    /// <summary>
    /// A folder synchroniser for a screen that has to *reach* a server without the test being about
    /// folders: one whose account has none rather than one it cannot get to, since an unreachable
    /// synchroniser reports the whole sync as failed - which is what several dashboard tests check the
    /// screen does not say.
    ///
    /// It answers out of <see cref="FakeFoldersServer"/> rather than out of a stub with one body,
    /// because folders have no change feed: a stub answering an empty list deletes the folder the test
    /// just made on the very next sync. See that class for the whole trap.
    /// </summary>
    internal static FolderSynchronizer SynchronizerAnsweringLikeTheServer(
        LocalStore localStore, TimeProvider clock, SyncGate gate, FakeFoldersServer server)
        => new(
            localStore, new FoldersClient(server.ToHttpClient()), clock, gate,
            NullLogger<FolderSynchronizer>.Instance);
}
