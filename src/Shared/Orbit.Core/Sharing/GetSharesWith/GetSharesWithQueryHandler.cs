using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;

namespace Orbit.Core.Sharing.GetSharesWith;

/// <summary>
/// The four share repositories meet here rather than in the page, the same way GetShareOfferQueryHandler
/// gathers them for one offer: which repository answers follows from the kind, and that is one fact
/// rather than four.
///
/// Every lookup is scoped to the owner, so this can only ever list what the caller themselves gave away.
/// </summary>
public sealed class GetSharesWithQueryHandler : IRequestHandler<GetSharesWithQuery, IReadOnlyList<SharedWithSomebody>>
{
    private readonly INoteShareRepository _noteShareRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;
    private readonly IInventoryShareRepository _inventoryShareRepository;
    private readonly SharedItemName _sharedItemName;

    public GetSharesWithQueryHandler(
        INoteShareRepository noteShareRepository,
        ITaskListShareRepository taskListShareRepository,
        ICalendarEventShareRepository calendarEventShareRepository,
        IInventoryShareRepository inventoryShareRepository,
        SharedItemName sharedItemName)
    {
        _noteShareRepository = noteShareRepository;
        _taskListShareRepository = taskListShareRepository;
        _calendarEventShareRepository = calendarEventShareRepository;
        _inventoryShareRepository = inventoryShareRepository;
        _sharedItemName = sharedItemName;
    }

    public async Task<IReadOnlyList<SharedWithSomebody>> HandleAsync(
        GetSharesWithQuery request, CancellationToken cancellationToken)
    {
        var found = new List<(SharedItemKind Kind, Guid ShareId, Guid ItemId, bool IsAccepted, DateTimeOffset SharedAtUtc)>();

        foreach (var share in await _noteShareRepository.GetSharesToAsync(request.OwnerUserId, request.RecipientUserId, cancellationToken))
        {
            found.Add((SharedItemKind.Note, share.Id, share.SourceNoteId, share.IsAccepted, share.CreatedAtUtc));
        }

        foreach (var share in await _taskListShareRepository.GetSharesToAsync(request.OwnerUserId, request.RecipientUserId, cancellationToken))
        {
            found.Add((SharedItemKind.TaskList, share.Id, share.SourceTaskListId, share.IsAccepted, share.CreatedAtUtc));
        }

        foreach (var share in await _calendarEventShareRepository.GetSharesToAsync(request.OwnerUserId, request.RecipientUserId, cancellationToken))
        {
            found.Add((SharedItemKind.CalendarEvent, share.Id, share.SourceCalendarEventId, share.IsAccepted, share.CreatedAtUtc));
        }

        foreach (var share in await _inventoryShareRepository.GetSharesToAsync(request.OwnerUserId, request.RecipientUserId, cancellationToken))
        {
            found.Add((SharedItemKind.Inventory, share.Id, share.SourceInventoryId, share.IsAccepted, share.CreatedAtUtc));
        }

        var listed = new List<SharedWithSomebody>();
        foreach (var share in found.OrderByDescending(share => share.SharedAtUtc))
        {
            // Empty rather than absent for something deleted since it was shared, and for a private
            // thing, whose title the server holds no readable copy of - the page names those by kind.
            // The row belongs on the list either way: it is still access somebody has, and taking it
            // back is exactly why they are reading this.
            var title = await _sharedItemName.ReadAsync(share.Kind, request.OwnerUserId, share.ItemId, cancellationToken);
            listed.Add(new SharedWithSomebody(
                share.Kind, share.ShareId, share.ItemId, title ?? string.Empty, share.IsAccepted, share.SharedAtUtc));
        }

        return listed;
    }
}
