using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.DeleteMessage;

[ClientAction(ClientActionCategory.Edit)]
public sealed record DeleteChatMessageCommand(Guid ActorUserId, Guid MessageId) : IRequest<bool>;
