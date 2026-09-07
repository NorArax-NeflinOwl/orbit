using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.Sharing.GetShareOffer;

/// <summary>
/// What somebody was offered, for the page a share's notification leads to - see Orbit.Web's
/// ShareInvitation.razor. Null when there is no such offer for this reader, which covers a withdrawn
/// one and one that was never theirs: they read the same on purpose, since answering differently would
/// say whether a share id exists.
/// </summary>
public sealed record GetShareOfferQuery(Guid RecipientUserId, SharedItemKind Kind, Guid ShareId)
    : IRequest<ShareOffer?>;

/// <param name="ItemId">
/// What was offered, so accepting can land on the thing itself rather than on the list it will appear
/// in. Only ever handed to the person it was offered to.
/// </param>
/// <param name="ItemTitle">
/// What it is called, or empty for one that has since been deleted - the same name the invitation in the
/// conversation carries, which the sharer's own browser wrote into it.
/// </param>
public sealed record ShareOffer(Guid ItemId, string ItemTitle, bool IsAccepted);
