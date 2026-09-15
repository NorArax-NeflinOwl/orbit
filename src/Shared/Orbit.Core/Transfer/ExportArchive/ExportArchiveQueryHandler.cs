using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Folders;
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
    private readonly IFolderRepository _folderRepository;

    public ExportArchiveQueryHandler(
        INoteRepository noteRepository,
        ITaskRepository taskRepository,
        ICalendarEventRepository calendarEventRepository,
        IInventoryRepository inventoryRepository,
        IInventoryItemRepository inventoryItemRepository,
        IPlaceRepository placeRepository,
        Orbit.Core.Tags.ITagColourRepository tagColourRepository,
        IFolderRepository folderRepository)
    {
        _noteRepository = noteRepository;
        _taskRepository = taskRepository;
        _calendarEventRepository = calendarEventRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _placeRepository = placeRepository;
        _tagColourRepository = tagColourRepository;
        _folderRepository = folderRepository;
    }

    public async Task<OrbitArchive> HandleAsync(ExportArchiveQuery request, CancellationToken cancellationToken)
    {
        var notes = await _noteRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var taskLists = await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var calendarEvents = await _calendarEventRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var inventories = await _inventoryRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var places = await _placeRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var tagColours = await _tagColourRepository.GetAllAsync(request.UserId, cancellationToken);
        var folders = await _folderRepository.GetAllAsync(request.UserId, cancellationToken);

        var ownTaskLists = taskLists.Where(taskList => taskList.UserId == request.UserId).ToList();
        var links = new TaskListLinks(ownTaskLists);
        var filing = new ExportedFolders(folders);

        return new OrbitArchive(
            OrbitArchive.CurrentVersion,
            DateTimeOffset.UtcNow,
            notes.Where(note => note.UserId == request.UserId).Select(note => ToArchived(note, filing)).ToList(),
            ownTaskLists.Select(taskList => ToArchived(taskList, links, filing)).ToList(),
            calendarEvents.Where(calendarEvent => calendarEvent.UserId == request.UserId)
                .Select(calendarEvent => ToArchived(calendarEvent, filing)).ToList(),
            await ToArchivedInventoriesAsync(inventories, request.UserId, filing, cancellationToken),
            places.Where(place => place.UserId == request.UserId).Select(place => ToArchived(place, links)).ToList(),
            tagColours.Select(colour => new ArchivedTagColour(colour.Tag, colour.Colour)).ToList(),
            folders.Select(folder => new ArchivedFolder(folder.Name, folder.Scope.ToString())).ToList());
    }

    /// <summary>
    /// The account's folders, ready to be named by whatever is filed in them. A name rather than an id,
    /// because the file carries none (see OrbitArchive) - and a folder that has since been deleted is
    /// named by nothing at all, the way a link to a list that did not come along in the same file is.
    /// </summary>
    private sealed class ExportedFolders
    {
        private readonly Dictionary<Guid, string> _namesById;

        public ExportedFolders(IReadOnlyList<Folder> folders)
            => _namesById = folders
                .GroupBy(folder => folder.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);

        public string? NameOf(Guid? folderId)
            => folderId is { } id && _namesById.TryGetValue(id, out var name) ? name : null;
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
        IReadOnlyList<Inventory> inventories, Guid userId, ExportedFolders filing, CancellationToken cancellationToken)
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
                    item.Unit.ToString(), item.Categories)).ToList(),
                filing.NameOf(inventory.FolderId),
                inventory.IsArchived));
        }

        return archived;
    }

    private static ArchivedNote ToArchived(Note note, ExportedFolders filing)
        => new(
            note.Title,
            // A picture's bytes are not in the file, and a line naming bytes that are not there would be
            // a broken picture on import - so pictures are left out of an export, and said so in
            // info/functionality.md.
            note.Content.Where(line => !line.IsAPicture).Select(line => new ArchivedNoteLine(
                line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed, line.Style.ToString(),
                ArchivedMarks(line.AllMarks),
                line.Table?.Rows
                    .Select(row => (IReadOnlyList<ArchivedTableCell>)row.Cells
                        .Select(cell => new ArchivedTableCell(cell.Text, ArchivedMarks(cell.AllMarks))).ToList())
                    .ToList()))
                .ToList(),
            note.IsPrivate,
            ToArchived(note.EncryptedContent),
            note.Tags,
            filing.NameOf(note.FolderId),
            note.IsArchived);

    private static ArchivedTaskList ToArchived(TaskList taskList, TaskListLinks links, ExportedFolders filing)
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
            taskList.Tags,
            filing.NameOf(taskList.FolderId),
            taskList.IsArchived);

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

    private static ArchivedCalendarEvent ToArchived(CalendarEvent calendarEvent, ExportedFolders filing)
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
            details.ReminderNotificationChannel.ToString(),
            filing.NameOf(calendarEvent.FolderId),
            calendarEvent.IsArchived);
    }

    private static ArchivedEncryptedContent? ToArchived(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new ArchivedEncryptedContent(encryptedContent.Ciphertext, encryptedContent.Nonce);

    /// <summary>A line's or a cell's marks as the file carries them - nothing at all for none, so the field is absent.</summary>
    private static IReadOnlyList<ArchivedTextRun>? ArchivedMarks(IReadOnlyList<NoteTextRun> marks)
        => marks.Count == 0
            ? null
            : marks.Select(run => new ArchivedTextRun(run.Start, run.Length, run.Mark.ToString())).ToList();
}
