namespace Orbit.Contracts.Sharing;

/// <summary>
/// What somebody was offered, as the page a share's notification leads to reads it - see Orbit.Web's
/// ShareInvitation.razor and Orbit.Core.Sharing.GetShareOffer.
/// </summary>
/// <param name="ItemId">
/// What was offered, so accepting can land on the thing itself rather than on the list it will appear
/// in. Only ever answered to the person it was offered to.
/// </param>
/// <param name="ItemTitle">Empty for something deleted since it was offered - the offer still stands.</param>
public sealed record ShareOfferDto(Guid ItemId, string ItemTitle, bool IsAccepted);

/// <summary>
/// One thing the caller has handed one other person, as the contact's own page lists it.
/// </summary>
/// <param name="Kind">
/// Which kind of thing, by the name it carries inside an address - see
/// Orbit.Core.Notifications.SharedItemPath, whose names are stable and are what the page builds its
/// link from.
/// </param>
/// <param name="ItemTitle">
/// Empty for something deleted since it was shared, and for a private thing, which has no readable
/// title anywhere the server can reach. The page names those by kind rather than drawing a blank.
/// </param>
/// <param name="IsAccepted">
/// Whether they have taken it up. An offer nobody accepted is listed too: it is still access somebody
/// has been given, and withdrawing it before it is taken up is the likeliest reason to be reading this.
/// </param>
public sealed record SharedWithContactDto(
    string Kind, Guid ShareId, Guid ItemId, string ItemTitle, bool IsAccepted, DateTimeOffset SharedAtUtc);
