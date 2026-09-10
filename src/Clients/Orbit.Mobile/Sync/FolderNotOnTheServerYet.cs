namespace Orbit.Mobile.Sync;

/// <summary>
/// Thrown when something queued names a folder the server has not been told about yet - a folder made
/// with no connection, whose own create is still in the queue.
///
/// An <see cref="HttpRequestException"/> with no status, deliberately, because that is exactly what
/// this is to the rules that read it: nothing was asked, so <see cref="SyncFailure.IsWorthRetrying"/>
/// keeps the change queued and <see cref="SyncFailure.WasAnswered"/> does not count it against the
/// give-up limit. Somebody who filed a note into a new folder on a train has not been refused anything.
///
/// It converges because <see cref="EverythingSynchronizer"/> sends folders first: by the time notes
/// replay, a folder that could be created has been.
/// </summary>
public sealed class FolderNotOnTheServerYet : HttpRequestException
{
    public FolderNotOnTheServerYet(Guid folderLocalId)
        : base($"Folder {folderLocalId} has not reached the server yet, so nothing can be filed into it.")
    {
    }
}
