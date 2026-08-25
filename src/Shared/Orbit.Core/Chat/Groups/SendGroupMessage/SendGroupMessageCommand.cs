using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.SendGroupMessage;

/// <summary>
/// One group message, already encrypted once per recipient by the sender's browser. The server never
/// sees the text, so it can't do the fan-out itself - the client has to hand it a copy per member.
/// </summary>
public sealed record GroupMessageCopy(Guid RecipientUserId, string CiphertextBase64, string NonceBase64);

[ClientAction(ClientActionCategory.Save)]
public sealed record SendGroupMessageCommand(Guid SenderUserId, Guid GroupId, IReadOnlyList<GroupMessageCopy> Copies) : IRequest<bool>;
