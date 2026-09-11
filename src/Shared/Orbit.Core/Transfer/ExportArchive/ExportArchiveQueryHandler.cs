using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Places;
using Orbit.Core.Tasks;

namespace Orbit.Core.Transfer.ExportArchive;

/// <summary>
/// Reads only what this user owns. Things merely shared with them are left out: they belong to someone
/// else, and an export that quietly copied another person's note into a file would be a way of taking
/// it - the share is the access, and it stays where it is. Places follow the same rule, though the file
/// is where a place's content ends up readable: a place somebody handed over is theirs to write out.
/// </summary>
public sealed class ExportArchiveQueryHandler : IRequestHandler<ExportArchiveQuery, OrbitArchive>
{
    private readonly INoteRepository _noteRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IPlaceRepository _placeRepository;
    private readonly Orbit.Core.Tags.ITagColourRepository _tagColourRepository;

    public ExportArchiveQueryHandler(
        INoteRepository noteRepository,
        ITaskRepository taskRepository,
        ICalendarEventRepository calendarEventRepository,
        IInventoryRepository inventoryRepository,
        IInventoryItemRepository inventoryItemRepository,
        IPlaceRepository placeRepository,
        Orbit.Core.Tags.ITagColourRepository tagColourRepository)
    {
        _noteRepository = noteRepository;
        _taskRepository = taskRepository;
        _calendarEventRepository = calendarEventRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _placeRepository = placeRepository;
        _tagColourRepository = tagColourRepository;
    }

    public async Task<OrbitArchive> HandleAsync(ExportArchiveQuery request, CancellationToken cancellationToken)
    {
        var notes = await _noteRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var taskLists = await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var calendarEvents = await _calendarEventRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var inventories = await _inventoryRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var places = await _placeRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var tagColours = await _tagColourRepository.GetAllAsync(request.UserId, cancellationToken);

        var ownTaskLists = taskLists.Where(taskList => taskList.UserId == request.UserId).ToList();
        var links = new TaskListLinks(ownTaskLists);

        return new OrbitArchive(
            OrbitArchive.CurrentVersion,
            DateTimeOffset.UtcNow,
            notes.Where(note => note.UserId == request.UserId).Select(ToArchived).ToList(),
            ownTaskLists.Select(taskList => ToArchived(taskList, links)).ToList(),
            calendarEvents.Where(calendarEvent => calendarEvent.UserId == request.UserId).Select(ToArchived).ToList(),
            await ToArchivedInventoriesAsync(inventories, request.UserId, cancellationToken),
            places.Where(place => place.UserId == request.UserId).Select(place => ToArchived(place, links)).ToList(),
            tagColours.Select(colour => new ArchivedTagColour(colour.Tag, colour.Colour)).ToList());
    }

    /// <summary>
    /// A private place goes out as the server holds it - empty words beside the sealed half - because
    /// the server has no key to do anything else. Opening it is the browser's part; see ArchivedPlace.
    /// </summary>
    private static ArchivedPlace ToArchived(Place place, TaskListLinks links)
        => new(
            place.Name,
            place.Description,
            new ArchivedEventLocation(place.Where.Address ?? string.Empty, place.Where.Latitude, place.Where.Longitude),
            place.Colour,
            place.Priority.ToString(),
            links.TitlesOf(place.TaskListIds),
            place.IsPrivate,
            ToArchived(place.EncryptedContent),
            links.SealedOnesOf(place.TaskListIds));

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
                    // The first of them in the old single field as well, so an archive this build writes
                    // still imports into one that predates several - see ArchivedInventoryItem.Category.
                    item.Name, item.ProductType, item.Categories.FirstOrDefault() ?? string.Empty, item.Quantity,
                    item.MinimumQuantity, item.ExpiryDate, item.ExpiryNotificationChannel.ToString(),
                    item.Unit.ToString(), item.Categories)).ToList()));
        }

        return archived;
    }

    private static ArchivedNote ToArchived(Note note)
        => new(
            note.Title,
            note.Content.Select(line => new ArchivedNoteLine(line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed)).ToList(),
            note.IsPrivate,
            ToArchived(note.EncryptedContent),
            note.Tags);

    private static ArchivedTaskList ToArchived(TaskList taskList, TaskListLinks links)
        => new(
            taskList.Title,
            taskList.Items.Select(item => new ArchivedTaskItem(
                item.Description,
                item.DueDateUtc,
                item.IsCompleted,
                // Both shapes: the first title on its own for a reader that only knows the old field,
                // and all of them for one that knows the new.
                links.TitlesOf(item.LinkedTaskListIds).FirstOrDefault(),
                item.OverdueNotificationChannel.ToString(),
                item.RemindDaily,
                item.DailyReminderNotificationChannel.ToString(),
                item.DailyReminderTimeOfDay,
                links.TitlesOf(item.LinkedTaskListIds),
                item.Categories,
                item.IsFailed,
                links.SealedOnesOf(item.LinkedTaskListIds),
                item.CompletedAtUtc)).ToList(),
            taskList.IsGroup,
            taskList.IsPrivate,
            ToArchived(taskList.EncryptedContent),
            taskList.Priority.ToString(),
            taskList.Tags);

    /// <summary>
    /// How a link to one of the exported lists is written, because a file has no ids worth keeping - it
    /// is read into a different account with different ones. An open list goes by its title. A private
    /// one goes by the nonce of its sealed half, since its title is empty on the server and an empty
    /// title would resolve on import to whichever private list came first (see
    /// ArchivedTaskItem.LinkedSealedTaskLists). A link to a list that is not in the export is written
    /// neither way, and dropped rather than written as something nothing will match.
    /// </summary>
    private sealed class TaskListLinks
    {
        private readonly Dictionary<Guid, string> _titlesById;
        private readonly Dictionary<Guid, string> _noncesById;

        public TaskListLinks(IReadOnlyList<TaskList> exported)
        {
            _titlesById = exported
                .Where(taskList => !taskList.IsPrivate && taskList.Title.Length > 0)
                .ToDictionary(taskList => taskList.Id, taskList => taskList.Title);
            _noncesById = exported
                .Where(taskList => taskList.IsPrivate && taskList.EncryptedContent is not null)
                .ToDictionary(taskList => taskList.Id, taskList => taskList.EncryptedContent!.Nonce);
        }

        public IReadOnlyList<string> TitlesOf(IEnumerable<Guid> taskListIds)
            => [.. taskListIds.Select(id => _titlesById.GetValueOrDefault(id)).OfType<string>()];

        public IReadOnlyList<string> SealedOnesOf(IEnumerable<Guid> taskListIds)
            => [.. taskListIds.Select(id => _noncesById.GetValueOrDefault(id)).OfType<string>()];
    }

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
