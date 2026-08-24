using Orbit.Core.Abstractions;

namespace Orbit.Core.Notifications.MarkAllNotificationsRead;

[ClientAction(ClientActionCategory.Edit)]
public sealed record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<bool>;
