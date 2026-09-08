using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;

namespace Orbit.Core.Sharing.GetShareOffer;

/// <summary>
/// Reads one offer, whatever kind it is about. The four share repositories meet here rather than in the
/// page, for the reason PublicSharedItemReader gives about its own four: which repository answers
/// follows from the kind, and that is one fact rather than four.
///
/// Every lookup is scoped to the reader (<c>GetByIdAsync(recipientUserId, shareId)</c>), so an offer
/// made to somebody else is not there to be read - the same "not found" a withdrawn one gets.
/// </summary>
public sealed class GetShareOfferQueryHandler : IRequestHandler<GetShareOfferQuery, ShareOffer?>
{
    private readonly INoteShareRepository _noteShareRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;
    private readonly IInventoryShareRepository _inventoryShareRepository;
    private readonly SharedItemName _sharedItemName;

    public GetShareOfferQueryHandler(
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

    public async Task<ShareOffer?> HandleAsync(GetShareOfferQuery request, CancellationToken cancellationToken)
    {
        var offered = await ReadTheShareAsync(request, cancellationToken);
        if (offered is not { } share)
        {
            return null;
        }

        // Empty rather than absent for something deleted between the offer and the reading of it: the
        // offer is still real and can still be accepted - what it was called is the only thing lost.
        var title = await _sharedItemName.ReadAsync(request.Kind, share.OwnerUserId, share.ItemId, cancellationToken);
        return new ShareOffer(share.ItemId, title ?? string.Empty, share.IsAccepted);
    }

    private async Task<(Guid ItemId, Guid OwnerUserId, bool IsAccepted)?> ReadTheShareAsync(
        GetShareOfferQuery request, CancellationToken cancellationToken)
    {
        switch (request.Kind)
        {
            case SharedItemKind.Note:
                var note = await _noteShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
                return note is null ? null : (note.SourceNoteId, note.OwnerUserId, note.IsAccepted);

            case SharedItemKind.TaskList:
                var taskList = await _taskListShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
                return taskList is null ? null : (taskList.SourceTaskListId, taskList.OwnerUserId, taskList.IsAccepted);

            case SharedItemKind.CalendarEvent:
                var calendarEvent = await _calendarEventShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
                return calendarEvent is null ? null : (calendarEvent.SourceCalendarEventId, calendarEvent.OwnerUserId, calendarEvent.IsAccepted);

            case SharedItemKind.Inventory:
                var inventory = await _inventoryShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
                return inventory is null ? null : (inventory.SourceInventoryId, inventory.OwnerUserId, inventory.IsAccepted);

            // A position is not offered and has nothing to accept - see SharedItemLink.TheMap.
            default:
                return null;
        }
    }
}
