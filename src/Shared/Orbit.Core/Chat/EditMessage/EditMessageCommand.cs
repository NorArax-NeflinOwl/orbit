using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.EditMessage;

[ClientAction(ClientActionCategory.Edit)]
public sealed record EditMessageCommand(Guid MessageId, Guid RequestingUserId, string CiphertextBase64, string NonceBase64)
    : IRequest<EditMessageResult>;
