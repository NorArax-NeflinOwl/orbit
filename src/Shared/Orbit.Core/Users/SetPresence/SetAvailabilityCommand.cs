using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPresence;

/// <summary>Changes what the caller chose to be - available, or asking not to be disturbed.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record SetAvailabilityCommand(Guid UserId, PresenceAvailability Availability) : IRequest<bool>;
