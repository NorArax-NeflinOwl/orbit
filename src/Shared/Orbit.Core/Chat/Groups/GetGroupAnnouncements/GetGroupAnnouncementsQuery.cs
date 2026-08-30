using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.GetGroupAnnouncements;

/// <summary>
/// The "somebody joined" lines of a group conversation, optionally only those after
/// <paramref name="SinceUtc"/> - the same cursor GetGroupConversationQuery takes, so a client polling
/// both can ask each from the same point.
///
/// Asked for separately from the messages rather than merged into them: the two are different shapes,
/// and folding them into one response would have changed what every already-installed client receives
/// from the conversation endpoint.
/// </summary>
public sealed record GetGroupAnnouncementsQuery(Guid UserId, Guid GroupId, DateTimeOffset? SinceUtc = null)
    : IRequest<IReadOnlyList<ChatGroupAnnouncement>>;
