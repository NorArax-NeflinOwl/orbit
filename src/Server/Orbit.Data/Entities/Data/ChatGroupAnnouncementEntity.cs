namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of <see cref="Orbit.Core.Chat.Groups.ChatGroupAnnouncement"/>. Unlike a chat
/// message this holds no ciphertext: everything in it is already known to the server from the
/// membership table, so there is nothing here to seal.
/// </summary>
public sealed class ChatGroupAnnouncementEntity
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid JoinedUserId { get; set; }
    public Guid AddedByUserId { get; set; }
    public bool HistoryShared { get; set; }
    public DateTimeOffset AnnouncedAtUtc { get; set; }
}
