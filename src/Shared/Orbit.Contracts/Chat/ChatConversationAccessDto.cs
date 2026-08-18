namespace Orbit.Contracts.Chat;

/// <summary>
/// Whether a conversation between the caller and another user is still a pending chat request - see
/// Orbit.Core.Chat.ChatConversationAccess. The API returns null (no body) instead of this shape when
/// the pair has never exchanged a message, since nothing is gated in that case.
/// </summary>
public sealed record ChatConversationAccessDto(Guid InitiatedByUserId, bool IsApproved);
