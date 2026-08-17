using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.GetReadReceipt;

/// <summary>
/// Asks how far recipientUserId has read into the messages senderUserId sent them - see
/// IChatMessageRepository.GetReadUpToUtcAsync for what the returned timestamp means.
/// </summary>
public sealed record GetReadReceiptQuery(Guid SenderUserId, Guid RecipientUserId) : IRequest<DateTimeOffset?>;
