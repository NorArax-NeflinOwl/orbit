using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.LeaveChatGroup;

/// <summary>
/// Walking out of a group and taking your copies of what was said in it with you.
///
/// Two things at once on purpose: leaving a group and then still holding every message from it is a
/// state nobody asks for, and offering them as separate buttons would mean somebody leaves and the
/// messages quietly stay. Unlike a one-to-one conversation, these copies really are the caller's own -
/// a group message is encrypted separately for each member (see ChatMessage.CreateForGroup), so
/// deleting the copies addressed to them takes nothing away from anybody else.
///
/// Answers false when the caller is not in the group, which is what an id nobody recognises looks like.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record LeaveChatGroupCommand(Guid UserId, Guid GroupId) : IRequest<bool>;
