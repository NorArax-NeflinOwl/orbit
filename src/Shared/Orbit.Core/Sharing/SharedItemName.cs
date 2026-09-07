using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;

namespace Orbit.Core.Sharing;

/// <summary>
/// What one of the four shareable kinds is called, read as its owner. Only the name: everything else
/// about an item is its own screen's business.
///
/// Not PublicSharedItemReader's job even though that also reads four kinds. That one projects a whole
/// item for a page anybody with a link can open, and refuses anything private for exactly that reason;
/// this answers "what was I offered" for somebody who has already been offered it - a question the chat
/// invitation has always answered, since the sharer's own browser writes the title into it.
/// </summary>
public sealed class SharedItemName
{
    private readonly INoteRepository _noteRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public SharedItemName(
        INoteRepository noteRepository,
        ITaskRepository taskRepository,
        ICalendarEventRepository calendarEventRepository,
        IInventoryRepository inventoryRepository)
    {
        _noteRepository = noteRepository;
        _taskRepository = taskRepository;
        _calendarEventRepository = calendarEventRepository;
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// The item's name, or null when it is no longer there - deleted between the offer and the reading
    /// of it, which is an ordinary thing to happen and not an error. The caller says so rather than
    /// inventing a name.
    /// </summary>
    public async Task<string?> ReadAsync(
        SharedItemKind kind, Guid ownerUserId, Guid itemId, CancellationToken cancellationToken)
        => kind switch
        {
            SharedItemKind.Note =>
                (await _noteRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken))?.Title,
            SharedItemKind.TaskList =>
                (await _taskRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken))?.Title,
            SharedItemKind.CalendarEvent =>
                (await _calendarEventRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken))?.Details.Title,
            SharedItemKind.Inventory =>
                (await _inventoryRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken))?.Name,
            _ => null
        };
}
