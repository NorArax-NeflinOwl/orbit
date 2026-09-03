namespace Orbit.Core.Chat.Groups;

/// <summary>
/// One person's place in a group: which group, who, in what role, and since when. A record because a
/// membership is a value - changing a role replaces it rather than mutating it in place (see
/// ChatGroup.ChangeRole), which keeps every rule about roles inside the group that owns them.
/// </summary>
/// <param name="IsArchived">
/// Whether this member has put the group away. Per member rather than per group: one person tidying
/// their own list must not take the group off everybody else's.
/// </param>
public sealed record ChatGroupMembership(
    Guid GroupId, Guid UserId, ChatGroupRole Role, DateTimeOffset JoinedAtUtc, bool IsArchived = false);
