using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.MarkConversationAsRead;

public sealed record MarkConversationAsReadCommand(Guid ReaderUserId, Guid OtherUserId) : IRequest<bool>;
