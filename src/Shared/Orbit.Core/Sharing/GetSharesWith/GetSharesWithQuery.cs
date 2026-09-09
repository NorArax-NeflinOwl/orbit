using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.Sharing.GetSharesWith;

/// <summary>
/// Everything one person has handed another, of every kind. Asked by the contact's own page, which is
/// where "what have I given them" is a question somebody actually has - until this, the only way to
/// find out was to open each note, list, event and shelf in turn and read its sharing panel.
/// </summary>
public sealed record GetSharesWithQuery(Guid OwnerUserId, Guid RecipientUserId)
    : IRequest<IReadOnlyList<SharedWithSomebody>>;

/// <summary>
/// One thing somebody has handed somebody else.
/// </summary>
/// <param name="ItemTitle">
/// Empty for a thing deleted since it was shared, and for a private one, which has no readable title
/// anywhere the server can reach. The page names it by kind rather than showing a blank.
/// </param>
/// <param name="IsAccepted">
/// Whether they have taken it up. An offer nobody accepted is still listed - it is still something the
/// owner has given away, and withdrawing it before it is taken up is the most likely reason to be here.
/// </param>
public sealed record SharedWithSomebody(
    SharedItemKind Kind, Guid ShareId, Guid ItemId, string ItemTitle, bool IsAccepted, DateTimeOffset SharedAtUtc);
