using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.CreateChatGroup;

/// <summary>MemberUserIds are added alongside the creator, who is always the group's first admin.</summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record CreateChatGroupCommand(Guid CreatedByUserId, string Name, IReadOnlyList<Guid> MemberUserIds) : IRequest<Guid>;
