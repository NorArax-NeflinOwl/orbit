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
