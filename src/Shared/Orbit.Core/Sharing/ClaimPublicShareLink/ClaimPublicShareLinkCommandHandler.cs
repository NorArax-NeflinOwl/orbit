using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventory;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
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
    private readonly IWarehouseShareRepository _warehouseShareRepository;
    private readonly TaskListShareCascade _taskListShareCascade;
    private readonly ISharedItemNotifier _sharedItemNotifier;

    public ClaimPublicShareLinkCommandHandler(
        IPublicShareLinkRepository publicShareLinkRepository,
        PublicSharedItemReader publicSharedItemReader,
        INoteShareRepository noteShareRepository,
        ITaskListShareRepository taskListShareRepository,
        ICalendarEventShareRepository calendarEventShareRepository,
        IWarehouseShareRepository warehouseShareRepository,
        TaskListShareCascade taskListShareCascade,
        ISharedItemNotifier sharedItemNotifier)
    {
        _publicShareLinkRepository = publicShareLinkRepository;
        _publicSharedItemReader = publicSharedItemReader;
        _noteShareRepository = noteShareRepository;
        _taskListShareRepository = taskListShareRepository;
        _calendarEventShareRepository = calendarEventShareRepository;
        _warehouseShareRepository = warehouseShareRepository;
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

        if (link.OwnerUserId == request.ClaimingUserId)
        {
            // Their own item: it is already in their account, and a share row from them to themselves
            // would show up as something shared with them.
            return new ClaimPublicShareLinkResult(Claimed: false, link.ItemType, link.ItemId, AlreadyHeld: true);
        }

        var alreadyHeld = await GrantReadOnlyAccessAsync(link, request.ClaimingUserId, cancellationToken);
        if (!alreadyHeld)
        {
            await _sharedItemNotifier.NotifyAsync(
                request.ClaimingUserId, link.OwnerUserId, ToSharedItemKind(link.ItemType), item.Title, cancellationToken);
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

            default:
            {
                if (await _warehouseShareRepository.FindExistingAsync(link.ItemId, claimingUserId, cancellationToken) is not null)
                {
                    return true;
                }

                var share = WarehouseShare.Create(link.ItemId, link.OwnerUserId, claimingUserId);
                share.MarkAccepted();
                await _warehouseShareRepository.AddAsync(share, cancellationToken);
                return false;
            }
        }
    }

    private static SharedItemKind ToSharedItemKind(SharedItemType itemType) => itemType switch
    {
        SharedItemType.Note => SharedItemKind.Note,
        SharedItemType.TaskList => SharedItemKind.TaskList,
        SharedItemType.CalendarEvent => SharedItemKind.CalendarEvent,
        _ => SharedItemKind.Warehouse
    };
}
