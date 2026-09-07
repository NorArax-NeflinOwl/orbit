namespace Orbit.Core.Notifications;

/// <summary>
/// Where the notification about one share should lead. Three shapes, because a share arrives in three
/// states and only one of them is "here it is":
///
/// <list type="bullet">
///   <item><see cref="ToAccept"/> - an offer nobody has taken up yet. It leads to the invitation page,
///     which is where it can be accepted; the item itself is not the recipient's to open until then, so
///     pointing at it would land them on "no longer exists".</item>
///   <item><see cref="StraightToIt"/> - something they already hold, which is what claiming a public
///     link produces: the grant is immediate, so the notification opens the thing.</item>
///   <item><see cref="TheMap"/> - a position, which needs no accepting and is read on the map.</item>
/// </list>
///
/// A record rather than two nullable ids on <see cref="ISharedItemNotifier.NotifyAsync"/>: the caller
/// says which of the three this is, and cannot say two of them at once.
/// </summary>
public sealed record SharedItemLink
{
    private SharedItemLink(Guid? shareId, Guid? itemId)
    {
        PendingShareId = shareId;
        ItemId = itemId;
    }

    /// <summary>The offer to take up, when there is one still open.</summary>
    public Guid? PendingShareId { get; }

    /// <summary>The thing itself, when the recipient already holds it.</summary>
    public Guid? ItemId { get; }

    public static SharedItemLink ToAccept(Guid shareId) => new(shareId, null);

    public static SharedItemLink StraightToIt(Guid itemId) => new(null, itemId);

    public static readonly SharedItemLink TheMap = new(null, null);
}
