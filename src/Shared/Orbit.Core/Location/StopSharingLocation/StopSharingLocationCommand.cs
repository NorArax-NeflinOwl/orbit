using Orbit.Core.Abstractions;

namespace Orbit.Core.Location.StopSharingLocation;

/// <summary>
/// Ends sharing with one recipient, or with everyone when RecipientUserId is null. The row is deleted
/// rather than flagged, so stopping means the position is gone from the database - not merely stale.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record StopSharingLocationCommand(Guid SharerUserId, Guid? RecipientUserId) : IRequest<bool>;
