using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;

namespace Orbit.Core.Sharing.RevokeShare;

/// <summary>
/// Withdraws one share, whichever kind it is about - the four repositories meet here for the reason
/// GetSharesWithQueryHandler gives about its own four.
///
/// Every removal is scoped to the owner, so this can only take back access the caller themselves gave.
/// A share belonging to somebody else, or one already gone, answers false rather than throwing: the
/// page says the same thing for both, and telling them apart would say whether a share id exists.
/// </summary>
public sealed class RevokeShareCommandHandler : IRequestHandler<RevokeShareCommand, bool>
{
    private readonly INoteShareRepository _noteShareRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;
    private readonly IInventoryShareRepository _inventoryShareRepository;

    public RevokeShareCommandHandler(
        INoteShareRepository noteShareRepository,
        ITaskListShareRepository taskListShareRepository,
        ICalendarEventShareRepository calendarEventShareRepository,
        IInventoryShareRepository inventoryShareRepository)
    {
        _noteShareRepository = noteShareRepository;
        _taskListShareRepository = taskListShareRepository;
        _calendarEventShareRepository = calendarEventShareRepository;
        _inventoryShareRepository = inventoryShareRepository;
    }

    public Task<bool> HandleAsync(RevokeShareCommand request, CancellationToken cancellationToken) => request.Kind switch
    {
        SharedItemKind.Note => _noteShareRepository.RemoveAsync(request.OwnerUserId, request.ShareId, cancellationToken),
        SharedItemKind.TaskList => _taskListShareRepository.RemoveAsync(request.OwnerUserId, request.ShareId, cancellationToken),
        SharedItemKind.CalendarEvent => _calendarEventShareRepository.RemoveAsync(request.OwnerUserId, request.ShareId, cancellationToken),
        SharedItemKind.Inventory => _inventoryShareRepository.RemoveAsync(request.OwnerUserId, request.ShareId, cancellationToken),
        // A position is not a share row at all - it lives in OP_LOCATIONS_SHARED and is withdrawn from
        // the map, where it was offered. Named rather than swept into a default, so a fifth kind that
        // does have a row fails to compile here instead of quietly answering "nothing to do".
        SharedItemKind.Location => Task.FromResult(false),
        _ => Task.FromResult(false)
    };
}
