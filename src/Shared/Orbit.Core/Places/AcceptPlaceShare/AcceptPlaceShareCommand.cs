using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.AcceptPlaceShare;

/// <summary>False when the share does not exist or was offered to somebody else.</summary>
public sealed record AcceptPlaceShareCommand(Guid RecipientUserId, Guid ShareId) : IRequest<bool>;
