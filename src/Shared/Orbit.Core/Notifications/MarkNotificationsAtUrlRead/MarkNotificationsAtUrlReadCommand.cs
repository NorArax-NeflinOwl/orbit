using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.MarkNotificationsAtUrlRead;

/// <summary>
/// Marks read whatever notifications pointed at Url. Sent by the client when it arrives somewhere, so
/// reaching the page a notification was about counts as having read it - previously only clicking the
/// entry in the panel did, and walking to the page yourself left the badge lit over nothing.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record MarkNotificationsAtUrlReadCommand(Guid UserId, string Url) : IRequest<bool>;
