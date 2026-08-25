using Orbit.Core.Abstractions;

namespace Orbit.Core.Sharing.ClaimPublicShareLink;

/// <summary>
/// Turns "I am reading this through a link" into "this is in my account": creates an ordinary read-only
/// share of the item for the signed-in caller. The link itself is untouched, and the item is not copied
/// - the caller ends up holding a grant against the owner's one true copy, exactly as if the owner had
/// shared it with them by name.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record ClaimPublicShareLinkCommand(string Token, Guid ClaimingUserId) : IRequest<ClaimPublicShareLinkResult>;

/// <param name="AlreadyHeld">True when the caller already had access, so claiming was a no-op rather than a new grant.</param>
public sealed record ClaimPublicShareLinkResult(bool Claimed, SharedItemType ItemType, Guid ItemId, bool AlreadyHeld)
{
    /// <summary>The link doesn't resolve - unknown, revoked, or pointing at something since deleted or made private.</summary>
    public static ClaimPublicShareLinkResult NotFound() => new(Claimed: false, default, Guid.Empty, AlreadyHeld: false);
}
