using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventory;
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
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public ExportArchiveQueryHandler(
        INoteRepository noteRepository,
        ITaskRepository taskRepository,
        ICalendarEventRepository calendarEventRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryRepository inventoryRepository)
    {
        _noteRepository = noteRepository;
        _taskRepository = taskRepository;
        _calendarEventRepository = calendarEventRepository;
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<OrbitArchive> HandleAsync(ExportArchiveQuery request, CancellationToken cancellationToken)
    {
        var notes = await _noteRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var taskLists = await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var calendarEvents = await _calendarEventRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var warehouses = await _warehouseRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);

        var ownTaskLists = taskLists.Where(taskList => taskList.UserId == request.UserId).ToList();
        var taskListTitlesById = ownTaskLists.ToDictionary(taskList => taskList.Id, taskList => taskList.Title);

        return new OrbitArchive(
            OrbitArchive.CurrentVersion,
            DateTimeOffset.UtcNow,
            notes.Where(note => note.UserId == request.UserId).Select(ToArchived).ToList(),
            ownTaskLists.Select(taskList => ToArchived(taskList, taskListTitlesById)).ToList(),
            calendarEvents.Where(calendarEvent => calendarEvent.UserId == request.UserId).Select(ToArchived).ToList(),
            await ToArchivedWarehousesAsync(warehouses, request.UserId, cancellationToken));
    }

    private async Task<IReadOnlyList<ArchivedWarehouse>> ToArchivedWarehousesAsync(
        IReadOnlyList<Warehouse> warehouses, Guid userId, CancellationToken cancellationToken)
    {
        var archived = new List<ArchivedWarehouse>();
        foreach (var warehouse in warehouses.Where(warehouse => warehouse.UserId == userId))
        {
            // A private warehouse has no item rows at all - they were removed when it became private
            // (see UpdateWarehouseCommandHandler), and its contents live inside the sealed payload.
            var items = warehouse.IsPrivate
                ? []
                : await _inventoryRepository.GetAllAsync(warehouse.Id, cancellationToken);

            archived.Add(new ArchivedWarehouse(
                warehouse.Name, warehouse.IsPrivate, ToArchived(warehouse.EncryptedContent),
                items.Select(item => new ArchivedWarehouseItem(
                    item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity,
                    item.ExpiryDate, item.ExpiryNotificationChannel.ToString())).ToList()));
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
                item.LinkedTaskListId is { } linkedId && taskListTitlesById.TryGetValue(linkedId, out var title) ? title : null,
                item.OverdueNotificationChannel.ToString(),
                item.RemindDaily,
                item.DailyReminderNotificationChannel.ToString(),
                item.DailyReminderTimeOfDay)).ToList(),
            taskList.IsGroup,
            taskList.IsPrivate,
            ToArchived(taskList.EncryptedContent),
            taskList.Priority.ToString());

    private static ArchivedCalendarEvent ToArchived(CalendarEvent calendarEvent)
    {
        var details = calendarEvent.Details;

        // Guests are left out: they are ids of other accounts, which mean nothing in a file and nothing
        // in whatever account it is imported into.
        return new ArchivedCalendarEvent(
            details.Title, details.Description, details.Color, details.StartUtc, details.EndUtc, details.IsAllDay,
            details.Location is { } location ? new ArchivedEventLocation(location.Address ?? string.Empty, location.Latitude, location.Longitude) : null,
            details.ReminderMinutesBeforeStart,
            details.CreationNotificationChannel.ToString(),
            details.ReminderNotificationChannel.ToString());
    }

    private static ArchivedEncryptedContent? ToArchived(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new ArchivedEncryptedContent(encryptedContent.Ciphertext, encryptedContent.Nonce);
}
