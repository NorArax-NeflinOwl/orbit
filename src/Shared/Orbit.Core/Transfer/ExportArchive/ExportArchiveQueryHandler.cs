using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Tasks;

namespace Orbit.Core.Transfer.ExportArchive;

/// <summary>
/// Reads only what this user owns. Things merely shared with them are left out: they belong to someone
/// else, and an export that quietly copied another person's note into a file would be a way of taking
/// it - the share is the access, and it stays where it is.
/// </summary>
public sealed class ExportArchiveQueryHandler : IRequestHandler<ExportArchiveQuery, OrbitArchive>
{
    private readonly INoteRepository _noteRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public ExportArchiveQueryHandler(
        INoteRepository noteRepository,
        ITaskRepository taskRepository,
        ICalendarEventRepository calendarEventRepository,
        IInventoryRepository inventoryRepository,
        IInventoryItemRepository inventoryItemRepository)
    {
        _noteRepository = noteRepository;
        _taskRepository = taskRepository;
        _calendarEventRepository = calendarEventRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
    }

    public async Task<OrbitArchive> HandleAsync(ExportArchiveQuery request, CancellationToken cancellationToken)
    {
        var notes = await _noteRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var taskLists = await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var calendarEvents = await _calendarEventRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var inventories = await _inventoryRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);

        var ownTaskLists = taskLists.Where(taskList => taskList.UserId == request.UserId).ToList();
        var taskListTitlesById = ownTaskLists.ToDictionary(taskList => taskList.Id, taskList => taskList.Title);

        return new OrbitArchive(
            OrbitArchive.CurrentVersion,
            DateTimeOffset.UtcNow,
            notes.Where(note => note.UserId == request.UserId).Select(ToArchived).ToList(),
            ownTaskLists.Select(taskList => ToArchived(taskList, taskListTitlesById)).ToList(),
            calendarEvents.Where(calendarEvent => calendarEvent.UserId == request.UserId).Select(ToArchived).ToList(),
            await ToArchivedInventoriesAsync(inventories, request.UserId, cancellationToken));
    }

    private async Task<IReadOnlyList<ArchivedInventory>> ToArchivedInventoriesAsync(
        IReadOnlyList<Inventory> inventories, Guid userId, CancellationToken cancellationToken)
    {
        var archived = new List<ArchivedInventory>();
        foreach (var inventory in inventories.Where(inventory => inventory.UserId == userId))
        {
            // A private inventory has no item rows at all - they were removed when it became private
            // (see UpdateInventoryCommandHandler), and its contents live inside the sealed payload.
            var items = inventory.IsPrivate
                ? []
                : await _inventoryItemRepository.GetAllAsync(inventory.Id, cancellationToken);

            archived.Add(new ArchivedInventory(
                inventory.Name, inventory.IsPrivate, ToArchived(inventory.EncryptedContent),
                items.Select(item => new ArchivedInventoryItem(
                    item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity,
                    item.ExpiryDate, item.ExpiryNotificationChannel.ToString(), item.Unit.ToString())).ToList()));
        }

        return archived;
    }

    private static ArchivedNote ToArchived(Note note)
        => new(
            note.Title,
            note.Content.Select(line => new ArchivedNoteLine(line.Text, line.IsChecklistItem, line.IsChecked)).ToList(),
            note.IsPrivate,
            ToArchived(note.EncryptedContent));

    private static ArchivedTaskList ToArchived(TaskList taskList, IReadOnlyDictionary<Guid, string> taskListTitlesById)
        => new(
            taskList.Title,
            taskList.Items.Select(item => new ArchivedTaskItem(
                item.Description,
                item.DueDateUtc,
                item.IsCompleted,
                // Both shapes: the first title on its own for a reader that only knows the old field,
                // and all of them for one that knows the new.
                TitlesOf(item, taskListTitlesById).FirstOrDefault(),
                item.OverdueNotificationChannel.ToString(),
                item.RemindDaily,
                item.DailyReminderNotificationChannel.ToString(),
                item.DailyReminderTimeOfDay,
                TitlesOf(item, taskListTitlesById),
                item.Categories)).ToList(),
            taskList.IsGroup,
            taskList.IsPrivate,
            ToArchived(taskList.EncryptedContent),
            taskList.Priority.ToString());

    /// <summary>
    /// The lists an entry stands for, by title, because a file has no ids worth keeping - it is read
    /// into a different account with different ones. A link to a list that is not in the export is
    /// dropped rather than written as a title nothing will match.
    /// </summary>
    private static IReadOnlyList<string> TitlesOf(TaskItem item, IReadOnlyDictionary<Guid, string> taskListTitlesById)
        => [.. item.LinkedTaskListIds
            .Select(linkedId => taskListTitlesById.TryGetValue(linkedId, out var title) ? title : null)
            .OfType<string>()];

    private static ArchivedCalendarEvent ToArchived(CalendarEvent calendarEvent)
    {
        var details = calendarEvent.Details;

        // Guests are left out: they are ids of other accounts, which mean nothing in a file and nothing
        // in whatever account it is imported into.
        return new ArchivedCalendarEvent(
            details.Title, details.Description, details.Color, details.StartUtc, details.EndUtc, details.IsAllDay,
            details.Location is { } location ? new ArchivedEventLocation(location.Address ?? string.Empty, location.Latitude, location.Longitude) : null,
            details.ReminderMinutesBeforeStart,
            // Nothing announces an event to its own owner any more - see ArchivedCalendarEvent.
            CreationNotificationChannel: nameof(Orbit.Core.Notifications.NotificationChannel.None),
            details.ReminderNotificationChannel.ToString());
    }

    private static ArchivedEncryptedContent? ToArchived(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new ArchivedEncryptedContent(encryptedContent.Ciphertext, encryptedContent.Nonce);
}
