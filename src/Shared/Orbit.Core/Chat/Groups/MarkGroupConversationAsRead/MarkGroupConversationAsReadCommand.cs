using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.MarkGroupConversationAsRead;

/// <summary>Marks everything addressed to this reader in the group as read - the group counterpart of MarkConversationAsReadCommand.</summary>
public sealed record MarkGroupConversationAsReadCommand(Guid ReaderUserId, Guid GroupId) : IRequest<bool>;
