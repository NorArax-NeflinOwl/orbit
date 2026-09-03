namespace Orbit.Core.Chat.Groups;

/// <summary>
/// A line in a group conversation that nobody wrote: somebody joined, and possibly had the history
/// handed to them on the way in. It sits alongside the messages rather than among them because it is a
/// different kind of thing - there is no ciphertext here and no key involved, only facts the server
/// already knows from the membership table, which is why storing it in plain text gives nothing away
/// that was ever private.
///
/// <see cref="HistoryShared"/> starts false and is set once the re-encrypted history actually lands
/// (see ShareGroupHistoryCommandHandler). Sharing cannot be recorded up front: the copies are sealed in
/// the sharer's browser and arrive after the member is already in, so promising it at join time would
/// leave the group told about a history that never turned up.
/// </summary>
public sealed class ChatGroupAnnouncement
{
    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }

    /// <summary>Who joined.</summary>
    public Guid JoinedUserId { get; private set; }

    /// <summary>Who added them, and so who shared the history if any was shared.</summary>
    public Guid AddedByUserId { get; private set; }

    public bool HistoryShared { get; private set; }

    public DateTimeOffset AnnouncedAtUtc { get; private set; }

    private ChatGroupAnnouncement(
        Guid id, Guid groupId, Guid joinedUserId, Guid addedByUserId, bool historyShared, DateTimeOffset announcedAtUtc)
    {
        Id = id;
        GroupId = groupId;
        JoinedUserId = joinedUserId;
        AddedByUserId = addedByUserId;
        HistoryShared = historyShared;
        AnnouncedAtUtc = announcedAtUtc;
    }

    public static ChatGroupAnnouncement MemberJoined(Guid groupId, Guid joinedUserId, Guid addedByUserId)
        => new(Guid.NewGuid(), groupId, joinedUserId, addedByUserId, historyShared: false, DateTimeOffset.UtcNow);

    /// <summary>Rebuilds an announcement from already-persisted values.</summary>
    public static ChatGroupAnnouncement FromPersistence(
        Guid id, Guid groupId, Guid joinedUserId, Guid addedByUserId, bool historyShared, DateTimeOffset announcedAtUtc)
        => new(id, groupId, joinedUserId, addedByUserId, historyShared, announcedAtUtc);

    /// <summary>
    /// Says the history behind this join has now been handed over, which is what turns one line of the
    /// conversation into two facts. Idempotent, so a second share into the same join says nothing new.
    /// </summary>
    public void MarkHistoryShared() => HistoryShared = true;
}
