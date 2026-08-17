using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.SendMessage;

public sealed record SendMessageCommand(Guid SenderUserId, Guid RecipientUserId, string CiphertextBase64, string NonceBase64)
    : IRequest<ChatMessage?>;
