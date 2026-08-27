using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPresence;

/// <summary>
/// Says the caller is still here, without changing what they chose to be. Sent by an open client every
/// so often; the gap since the last one is what turns a green dot yellow and then grey - see
/// <see cref="UserPresence"/>.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record PresenceHeartbeatCommand(Guid UserId) : IRequest<bool>;
