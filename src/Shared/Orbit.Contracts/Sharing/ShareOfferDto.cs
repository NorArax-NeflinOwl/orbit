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
