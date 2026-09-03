namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a chat conversation's access-request state, mapped separately from
/// <see cref="Orbit.Core.Chat.ChatConversationAccess"/> so schema changes don't force changes onto
/// domain logic, and vice versa. One row per pair of users, regardless of direction - see
/// ChatConversationAccessRepository for how lookups account for that.
/// </summary>
public sealed class ChatConversationAccessEntity
{
    public Guid Id { get; set; }
    public Guid InitiatedByUserId { get; set; }
    public Guid OtherUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
}
