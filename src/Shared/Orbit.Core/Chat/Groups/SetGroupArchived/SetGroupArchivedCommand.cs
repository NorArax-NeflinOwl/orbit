using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.SetGroupArchived;

/// <summary>
/// Puts a group away on the caller's own list, or brings it back.
///
/// Per member rather than per group, and needing no rank: archiving says nothing about the group and
/// everything about one person's screen. Leaving is the other thing, and it is not this - a member who
/// archives is still in the group, still receives what is posted, and can bring it back.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record SetGroupArchivedCommand(Guid UserId, Guid GroupId, bool IsArchived) : IRequest<bool>;
