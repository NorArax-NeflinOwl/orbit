using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Places;
using Orbit.Core.Tasks;

namespace Orbit.Core.Sharing.ClaimPublicShareLink;

/// <summary>
/// The share it creates is accepted on the spot, unlike one the owner offers by name: the person
/// claiming it asked for it themselves, so there is nothing left to agree to. It is always ReadOnly -
/// a link is handed around and can reach anyone, so it must never be a route to editing someone's
/// item. Asking for edit access is a separate conversation with the owner.
/// </summary>
public sealed class ClaimPublicShareLinkCommandHandler : IRequestHandler<ClaimPublicShareLinkCommand, ClaimPublicShareLinkResult>
{
    private readonly IPublicShareLinkRepository _publicShareLinkRepository;
    private readonly PublicSharedItemReader _publicSharedItemReader;
    private readonly INoteShareRepository _noteShareRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;
    private readonly IInventoryShareRepository _inventoryShareRepository;
    private readonly IPlaceShareRepository _placeShareRepository;
    private readonly TaskListShareCascade _taskListShareCascade;
    private readonly ISharedItemNotifier _sharedItemNotifier;

    public ClaimPublicShareLinkCommandHandler(
        IPublicShareLinkRepository publicShareLinkRepository,
        PublicSharedItemReader publicSharedItemReader,
        INoteShareRepository noteShareRepository,
        ITaskListShareRepository taskListShareRepository,
        ICalendarEventShareRepository calendarEventShareRepository,
        IInventoryShareRepository inventoryShareRepository,
        IPlaceShareRepository placeShareRepository,
        TaskListShareCascade taskListShareCascade,
        ISharedItemNotifier sharedItemNotifier)
    {
        _publicShareLinkRepository = publicShareLinkRepository;
        _publicSharedItemReader = publicSharedItemReader;
        _noteShareRepository = noteShareRepository;
        _taskListShareRepository = taskListShareRepository;
        _calendarEventShareRepository = calendarEventShareRepository;
        _inventoryShareRepository = inventoryShareRepository;
        _placeShareRepository = placeShareRepository;
        _taskListShareCascade = taskListShareCascade;
        _sharedItemNotifier = sharedItemNotifier;
    }

    public async Task<ClaimPublicShareLinkResult> HandleAsync(ClaimPublicShareLinkCommand request, CancellationToken cancellationToken)
    {
        var link = await _publicShareLinkRepository.GetByTokenAsync(request.Token, cancellationToken);
        if (link is null || link.IsRevoked)
        {
            return ClaimPublicShareLinkResult.NotFound();
        }

        // Read it the same way the page does, so a link pointing at something deleted or since made
        // private can't be claimed either.
        var item = await _publicSharedItemReader.ReadAsync(link, cancellationToken);
        if (item is null)
        {
            return ClaimPublicShareLinkResult.NotFound();
        }

        // A folder's link is for reading. There is no such thing as a share of a folder - it is the
        // owner's own tab (see SharedItemType.Folder) - and granting the reader a copy of every thing
        // under it is a different and larger promise than the button makes: what it says is "your own
        // read-only copy", singular, and what would arrive is a folder's worth of them, unfiled. The
        // way to keep what is in somebody's folder is to be given it in Orbit, which is the other half
        // of what sharing a folder means. The page offers no button here; this is the guard behind it,
        // and it also keeps a folder out of the inventory branch below, which is where anything
        // unrecognised would otherwise land.
        if (link.ItemType == SharedItemType.Folder)
        {
            return ClaimPublicShareLinkResult.NotFound();
        }

        if (link.OwnerUserId == request.ClaimingUserId)
        {
            // Their own item: it is already in their account, and a share row from them to themselves
            // would show up as something shared with them.
            return new ClaimPublicShareLinkResult(Claimed: false, link.ItemType, link.ItemId, AlreadyHeld: true);
        }

        var alreadyHeld = await GrantReadOnlyAccessAsync(link, request.ClaimingUserId, cancellationToken);
        if (!alreadyHeld)
        {
            // Straight to the thing: claiming a link grants access there and then, so there is nothing
            // to accept and nothing an invitation page could offer.
            await _sharedItemNotifier.NotifyAsync(
                request.ClaimingUserId, link.OwnerUserId, ToSharedItemKind(link.ItemType), item.Title,
                SharedItemLink.StraightToIt(link.ItemId), cancellationToken);
        }

        return new ClaimPublicShareLinkResult(Claimed: true, link.ItemType, link.ItemId, alreadyHeld);
    }

    /// <summary>Returns whether the caller already had a grant, in which case nothing new is written.</summary>
    private async Task<bool> GrantReadOnlyAccessAsync(PublicShareLink link, Guid claimingUserId, CancellationToken cancellationToken)
    {
        switch (link.ItemType)
        {
            case SharedItemType.Note:
            {
                if (await _noteShareRepository.FindExistingAsync(link.ItemId, claimingUserId, cancellationToken) is not null)
                {
                    return true;
                }

                var share = NoteShare.Create(link.ItemId, link.OwnerUserId, claimingUserId);
                share.MarkAccepted();
                await _noteShareRepository.AddAsync(share, cancellationToken);
                return false;
            }

            case SharedItemType.TaskList:
            {
                // The lists it gathers, and the inventory it is measured against, come with it - a link
                // to a group list that opened onto rows nobody could follow would not be that list.
                await _taskListShareCascade.GrantAsync(
                    link.OwnerUserId, link.ItemId, claimingUserId, ShareAccessLevel.ReadOnly,
                    acceptImmediately: true, cancellationToken);

                if (await _taskListShareRepository.FindExistingAsync(link.ItemId, claimingUserId, cancellationToken) is not null)
                {
                    return true;
                }

                var share = TaskListShare.Create(link.ItemId, link.OwnerUserId, claimingUserId);
                share.MarkAccepted();
                await _taskListShareRepository.AddAsync(share, cancellationToken);
                return false;
            }

            case SharedItemType.CalendarEvent:
            {
                if (await _calendarEventShareRepository.FindExistingAsync(link.ItemId, claimingUserId, cancellationToken) is not null)
                {
                    return true;
                }

                var share = CalendarEventShare.Create(link.ItemId, link.OwnerUserId, claimingUserId);
                share.MarkAccepted();
                await _calendarEventShareRepository.AddAsync(share, cancellationToken);
                return false;
            }

            case SharedItemType.Place:
            {
                if (await _placeShareRepository.FindExistingAsync(link.ItemId, claimingUserId, cancellationToken) is not null)
                {
                    return true;
                }

                var share = PlaceShare.Create(link.ItemId, link.OwnerUserId, claimingUserId);
                share.MarkAccepted();
                await _placeShareRepository.AddAsync(share, cancellationToken);
                return false;
            }

            default:
            {
                if (await _inventoryShareRepository.FindExistingAsync(link.ItemId, claimingUserId, cancellationToken) is not null)
                {
                    return true;
                }

                var share = InventoryShare.Create(link.ItemId, link.OwnerUserId, claimingUserId);
                share.MarkAccepted();
                await _inventoryShareRepository.AddAsync(share, cancellationToken);
                return false;
            }
        }
    }

    private static SharedItemKind ToSharedItemKind(SharedItemType itemType) => itemType switch
    {
        SharedItemType.Note => SharedItemKind.Note,
        SharedItemType.TaskList => SharedItemKind.TaskList,
        SharedItemType.CalendarEvent => SharedItemKind.CalendarEvent,
        SharedItemType.Place => SharedItemKind.Place,
        _ => SharedItemKind.Inventory
    };
}
