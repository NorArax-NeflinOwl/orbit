using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.GetPlaceShareStatus;

/// <summary>
/// Null when the share does not exist or was offered to somebody else; otherwise whether it has been
/// taken up. What lets a conversation draw "Accept" against "already accepted" on an invitation.
/// </summary>
public sealed record GetPlaceShareStatusQuery(Guid RecipientUserId, Guid ShareId) : IRequest<bool?>;
