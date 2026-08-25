using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.ManageChatGroupMembers;

/// <summary>
/// The three things an admin does to a group's membership. Separate commands rather than one with a
/// verb, so each reads as what it is at the call site and carries only what it needs.
/// ActorUserId is whoever is asking; ChatGroup decides whether they may.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record AddChatGroupMemberCommand(Guid ActorUserId, Guid GroupId, Guid UserId) : IRequest<bool>;

[ClientAction(ClientActionCategory.Edit)]
public sealed record RemoveChatGroupMemberCommand(Guid ActorUserId, Guid GroupId, Guid UserId) : IRequest<bool>;

[ClientAction(ClientActionCategory.Edit)]
public sealed record ChangeChatGroupMemberRoleCommand(Guid ActorUserId, Guid GroupId, Guid UserId, ChatGroupRole Role) : IRequest<bool>;
