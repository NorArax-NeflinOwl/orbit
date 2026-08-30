namespace Orbit.Contracts.Chat;

/// <summary>
/// A group as the client sees it. Role is "Member" or "Admin" (see Orbit.Core.Chat.Groups.ChatGroupRole);
/// OwnRole is the caller's own, so the UI can decide what to offer without re-deriving it from Members.
/// </summary>
/// <param name="LastMessageAtUtc">
/// When something last happened here, so the conversation list can sort groups against people rather
/// than in a block of their own - see Orbit.Core.Chat.Groups.ChatGroup.LastMessageAtUtc.
/// </param>
public sealed record ChatGroupDto(
    Guid Id, string Name, Guid CreatedByUserId, DateTimeOffset CreatedAtUtc, string OwnRole,
    IReadOnlyList<ChatGroupMemberDto> Members, DateTimeOffset LastMessageAtUtc = default);

public sealed record ChatGroupMemberDto(Guid UserId, string Role, DateTimeOffset JoinedAtUtc);

public sealed record CreateChatGroupRequest(string Name, IReadOnlyList<Guid> MemberUserIds);

public sealed record AddChatGroupMemberRequest(Guid UserId);

public sealed record ChangeChatGroupMemberRoleRequest(string Role);

/// <summary>
/// One group message, encrypted once per other member by the sender's browser - the server can't do the
/// fan-out itself, having no key to read the text with. See Orbit.Core.Chat.ChatMessage.CreateForGroup.
/// </summary>
public sealed record SendGroupMessageRequest(IReadOnlyList<GroupMessageCopyDto> Copies);

public sealed record GroupMessageCopyDto(Guid RecipientUserId, string CiphertextBase64, string NonceBase64);

/// <summary>
/// A line in a group conversation that nobody wrote - see Orbit.Core.Chat.Groups.ChatGroupAnnouncement.
/// HistoryShared says whether the person who added them also handed over what was said before they
/// arrived, which is what turns one line into two facts.
/// </summary>
public sealed record ChatGroupAnnouncementDto(
    Guid Id, Guid JoinedUserId, Guid AddedByUserId, bool HistoryShared, DateTimeOffset AnnouncedAtUtc);

/// <summary>
/// A group's past, re-encrypted for somebody who joined after it happened. Sent by the sharer's browser
/// because only it holds keys to any of this - see Orbit.Core.Chat.Groups.ShareGroupHistory.
/// </summary>
public sealed record ShareGroupHistoryRequest(Guid RecipientUserId, IReadOnlyList<SharedHistoryCopyDto> Copies);

/// <summary>
/// One re-encrypted message. Carries no sender and no timestamp: those are read from the copy the server
/// already holds, so re-sharing cannot restate who said what, or when.
/// </summary>
public sealed record SharedHistoryCopyDto(Guid GroupMessageId, string CiphertextBase64, string NonceBase64);
