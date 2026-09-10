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
}
