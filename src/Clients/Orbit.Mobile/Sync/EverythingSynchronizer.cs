using System.Net;
using Orbit.Core.Permissions;
using Orbit.Mobile.Permissions;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Brings every feature's local data up to date in one call.
///
/// The dashboard needs this and nothing else does: it summarises all five features at once, so without
/// it the landing screen shows whatever the last-visited screen happened to synchronise. On a phone
/// signed into fresh - or one whose cache was just emptied - that is an empty dashboard until the
/// reader visits Notes, then Tasks, then the calendar, each of which fills in its own row.
///
/// Failures are counted rather than thrown: one feature being unreachable is no reason to leave the
/// other four unsynchronised, and the reader is told by the corner indicator either way.
/// </summary>
public sealed class EverythingSynchronizer
{
    private readonly NoteSynchronizer _notes;
    private readonly TaskListSynchronizer _taskLists;
    private readonly CalendarEventSynchronizer _calendarEvents;
    private readonly WarehouseSynchronizer _warehouses;
    private readonly ChatSynchronizer _chat;
    private readonly UserPermissions _permissions;

    public EverythingSynchronizer(
        NoteSynchronizer notes, TaskListSynchronizer taskLists, CalendarEventSynchronizer calendarEvents,
        WarehouseSynchronizer warehouses, ChatSynchronizer chat, UserPermissions permissions)
    {
        _notes = notes;
        _taskLists = taskLists;
        _calendarEvents = calendarEvents;
        _warehouses = warehouses;
        _chat = chat;
        _permissions = permissions;
    }

    public async Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
    {
        var everything = SyncTally.Nothing;

        everything = everything.And(await TryAsync(() => _notes.SynchroniseAsync(cancellationToken)));
        everything = everything.And(await TryAsync(() => _taskLists.SynchroniseAsync(cancellationToken)));
        everything = everything.And(await TryAsync(() => _calendarEvents.SynchroniseAsync(cancellationToken)));
        everything = everything.And(await TryAsync(() => _warehouses.SynchroniseAsync(cancellationToken)));

        // Chat reports only whether it worked, so it contributes reachability rather than counts. The
        // dashboard shows contacts and groups, which is exactly what these two fill in.
        //
        // Skipped outright for an account that has not unlocked them. Not only to save two round trips:
        // the chat synchroniser answers false for a refusal exactly as it does for a dropped connection,
        // so asking anyway put "couldn't sync" in the corner of a phone that was perfectly in step with
        // everything it is allowed to have.
        var contacts = _permissions.Has(ApplicationPermission.Contacts)
            ? await TryAsync(() => _chat.SynchroniseContactsAsync(cancellationToken))
            : Refused;
        var groups = _permissions.Has(ApplicationPermission.Chat)
            ? await TryAsync(() => _chat.SynchroniseGroupsAsync(cancellationToken))
            : Refused;

        return everything.And(contacts).And(groups).ToResult();
    }

    /// <summary>
    /// Nothing happened, and the server is not to blame. Used for a feature this account has not
    /// unlocked: the request reached the server and was refused, which is an answer rather than a
    /// failure - reporting it as one put "couldn't sync" in the corner of a phone that was perfectly in
    /// step with everything it is allowed to have.
    /// </summary>
    private static readonly SyncResult Refused = new(0, 0, 0, 0, ReachedTheServer: true);

    private static readonly SyncResult Unreachable = new(0, 0, 0, 0, ReachedTheServer: false);

    private static async Task<SyncResult> TryAsync(Func<Task<SyncResult>> synchronise)
    {
        try
        {
            return await synchronise();
        }
        catch (HttpRequestException exception)
        {
            return exception.StatusCode is HttpStatusCode.Forbidden ? Refused : Unreachable;
        }
    }

    private static async Task<SyncResult> TryAsync(Func<Task<bool>> synchronise)
    {
        try
        {
            return new SyncResult(0, 0, 0, 0, ReachedTheServer: await synchronise());
        }
        catch (HttpRequestException exception)
        {
            return exception.StatusCode is HttpStatusCode.Forbidden ? Refused : Unreachable;
        }
    }

    /// <summary>
    /// The running total while the features are worked through. Separate from <see cref="SyncResult"/>
    /// because a part-way total is not a result yet, and because "did every one of them reach the
    /// server" is an and-fold rather than a count.
    /// </summary>
    private readonly record struct SyncTally(int Sent, int Received, int RemovedLocally, int GivenUp, bool ReachedTheServer)
    {
        public static readonly SyncTally Nothing = new(0, 0, 0, 0, ReachedTheServer: true);

        public SyncTally And(SyncResult one)
            => new(
                Sent + one.Sent, Received + one.Received, RemovedLocally + one.RemovedLocally,
                GivenUp + one.GivenUp, ReachedTheServer && one.ReachedTheServer);

        public SyncResult ToResult() => new(Sent, Received, RemovedLocally, GivenUp, ReachedTheServer);
    }
}
