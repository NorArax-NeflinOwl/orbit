using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.ApproveConversation;

public sealed record ApproveConversationCommand(Guid ApprovingUserId, Guid OtherUserId) : IRequest<bool>;
