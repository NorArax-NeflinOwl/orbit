using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Places;
using Orbit.Core.Tasks;

namespace Orbit.Core.Transfer.ImportArchive;

/// <summary>
/// Adds everything in the archive to the caller's account as new items. Nothing is matched against what
/// is already there and nothing is replaced: an import run twice leaves two copies, which is a mess
/// someone can fix, unlike an import that overwrote the wrong thing.
///
/// Refuses an archive whose version this doesn't know, rather than reading what it recognises and
/// silently dropping the rest.
/// </summary>
public sealed class ImportArchiveCommandHandler : IRequestHandler<ImportArchiveCommand, ImportArchiveResult>
{
    private readonly INoteRepository _noteRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IPlaceRepository _placeRepository;

    public ImportArchiveCommandHandler(
        INoteRepository noteRepository,
        ITaskRepository taskRepository,
        ICalendarEventRepository calendarEventRepository,
        IInventoryRepository inventoryRepository,
        IInventoryItemRepository inventoryItemRepository,
        IPlaceRepository placeRepository)
    {
        _noteRepository = noteRepository;
        _taskRepository = taskRepository;
        _calendarEventRepository = calendarEventRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _placeRepository = placeRepository;
    }

    public async Task<ImportArchiveResult> HandleAsync(ImportArchiveCommand request, CancellationToken cancellationToken)
    {
        var archive = request.Archive;
        if (archive.Version != OrbitArchive.CurrentVersion)
        {
            throw new InvalidRequestException(
                $"This file was written by a different version of Orbit (version {archive.Version}) and can't be read here.");
        }

        var noteCount = await ImportNotesAsync(archive, request.UserId, cancellationToken);
        var createdTaskLists = await ImportTaskListsAsync(archive, request.UserId, cancellationToken);
        var calendarEventCount = await ImportCalendarEventsAsync(archive, request.UserId, cancellationToken);
        var inventoryCount = await ImportInventoriesAsync(archive, request.UserId, cancellationToken);
        var placeCount = await ImportPlacesAsync(archive, request.UserId, createdTaskLists, cancellationToken);

        return new ImportArchiveResult(noteCount, archive.TaskLists.Count, calendarEventCount, inventoryCount, placeCount);
    }

    private async Task<int> ImportNotesAsync(OrbitArchive archive, Guid userId, CancellationToken cancellationToken)
    {
        foreach (var archived in archive.Notes)
        {
            var note = Note.Create(
                userId, archived.Title,
                archived.Content.Select(line => new NoteContentLine(line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed)).ToList(),
                archived.IsPrivate, ToPayload(archived.EncryptedContent));
            await _noteRepository.AddAsync(note, cancellationToken);
        }

        return archive.Notes.Count;
    }

    /// <summary>
    /// Two passes, because a task item can link to another list: the lists have to exist before anything
    /// can point at them, and the archive carries links by title or sealed half (see ArchivedTaskItem). A
    /// link whose list didn't come along in the same file is dropped rather than guessed at.
    ///
    /// Hands back the lists it made, which is how the places imported after it find theirs.
    /// </summary>
    private async Task<CreatedTaskLists> ImportTaskListsAsync(
        OrbitArchive archive, Guid userId, CancellationToken cancellationToken)
    {
        var createdTaskLists = new CreatedTaskLists();
        var created = new List<TaskList>();

        foreach (var archived in archive.TaskLists)
        {
            var taskList = TaskList.Create(
                userId, archived.Title, [], archived.IsGroup, archived.IsPrivate, ToPayload(archived.EncryptedContent),
                ParsePriority(archived.Priority));
            await _taskRepository.AddAsync(taskList, cancellationToken);
            created.Add(taskList);
            createdTaskLists.Add(archived, taskList.Id);
        }

        for (var index = 0; index < created.Count; index++)
        {
            var archived = archive.TaskLists[index];
            if (archived.IsPrivate || archived.Items.Count == 0)
            {
                // A private list's items live inside its sealed payload, and writing readable ones back
                // would contradict what it says about itself.
                continue;
            }

            var taskList = created[index];
            taskList.Update(
                archived.Title,
                archived.Items.Select(item => ToTaskItem(item, createdTaskLists)).ToList(),
                archived.IsGroup, archived.IsPrivate, ToPayload(archived.EncryptedContent),
                ParsePriority(archived.Priority));
            await _taskRepository.UpdateAsync(taskList, cancellationToken);
        }

        return createdTaskLists;
    }

    private async Task<int> ImportCalendarEventsAsync(OrbitArchive archive, Guid userId, CancellationToken cancellationToken)
    {
        foreach (var archived in archive.CalendarEvents)
        {
            var details = new CalendarEventDetails(
                archived.Title,
                archived.Description,
                archived.Location is { } location
                    ? new EventLocation(location.Address, location.Latitude ?? 0, location.Longitude ?? 0)
                    : null,
                archived.Color,
                archived.StartUtc,
                archived.EndUtc,
                archived.IsAllDay,
                Recurrence: null,
                Guests: [],
                archived.ReminderMinutesBeforeStart,
                // The file's creation channel is read past: an event no longer tells its owner it was
                // made, so importing one must not either.
                ParseChannel(archived.ReminderNotificationChannel));

            await _calendarEventRepository.AddAsync(CalendarEvent.Create(userId, details), cancellationToken);
        }

        return archive.CalendarEvents.Count;
    }

    private async Task<int> ImportInventoriesAsync(OrbitArchive archive, Guid userId, CancellationToken cancellationToken)
    {
        foreach (var archived in archive.Inventories)
        {
            var inventory = Inventory.Create(userId, archived.Name, archived.IsPrivate, ToPayload(archived.EncryptedContent));
            await _inventoryRepository.AddAsync(inventory, cancellationToken);

            if (archived.IsPrivate)
            {
                continue;
            }

            foreach (var item in archived.Items)
            {
                await _inventoryItemRepository.AddAsync(
                    InventoryItem.Create(
                        inventory.Id, item.Name, item.ProductType, item.AllCategories, item.Quantity, item.MinimumQuantity,
                        ParseUnit(item.Unit), item.ExpiryDate, ParseChannel(item.ExpiryNotificationChannel)),
                    cancellationToken);
            }
        }

        return archive.Inventories.Count;
    }

    /// <summary>
    /// A private place comes back sealed under the half the file carried, the way a private note does:
    /// readable again in the account that sealed it, and in any other account a place nobody there can
    /// open. Its opened words are read past - Place stores a sealed place's columns empty whatever it is
    /// handed, and the clients strip them before sending (see OrbitArchive.WithPrivatePlacesClosed).
    ///
    /// One the file calls private but gives no sealed half for is left out and not counted. The server
    /// cannot seal it, and storing it readable would publish what the file itself says is private -
    /// Place refuses exactly that pairing, and the importer does not look for a way round it.
    /// </summary>
    private async Task<int> ImportPlacesAsync(
        OrbitArchive archive, Guid userId, CreatedTaskLists createdTaskLists, CancellationToken cancellationToken)
    {
        var imported = 0;
        foreach (var archived in archive.AllPlaces)
        {
            var sealedContent = ToPayload(archived.EncryptedContent);
            if (archived.IsPrivate && sealedContent is null)
            {
                continue;
            }

            var place = Place.Create(
                userId, archived.Name, archived.Description,
                new EventLocation(archived.Where.Address, archived.Where.Latitude ?? 0, archived.Where.Longitude ?? 0),
                archived.Colour, ParsePriority(archived.Priority),
                createdTaskLists.IdsOf(archived.TaskListTitles, archived.SealedTaskLists),
                archived.IsPrivate, sealedContent);
            await _placeRepository.AddAsync(place, cancellationToken);
            imported++;
        }

        return imported;
    }

    private static TaskItem ToTaskItem(ArchivedTaskItem item, CreatedTaskLists createdTaskLists)
        => TaskItem.Create(
            item.Description,
            item.DueDateUtc,
            item.IsCompleted,
            createdTaskLists.IdsOf(item.AllLinkedTaskListTitles, item.LinkedSealedTaskLists),
            new TaskItemReminders(
                ParseChannel(item.OverdueNotificationChannel),
                item.RemindDaily,
                ParseChannel(item.DailyReminderNotificationChannel),
                item.DailyReminderTimeOfDay),
            categories: item.AllCategories,
            isFailed: item.IsFailed);

    /// <summary>An unrecognised channel reads as None: a file should not be able to switch on notifications this account never asked for.</summary>
    private static NotificationChannel ParseChannel(string channel)
        => Enum.TryParse<NotificationChannel>(channel, out var parsed) ? parsed : NotificationChannel.None;

    /// <summary>An unrecognised unit reads as pieces, the same way an unrecognised channel reads as None.</summary>
    private static InventoryUnit ParseUnit(string unit)
        => Enum.TryParse<InventoryUnit>(unit, out var parsed) ? parsed : InventoryUnit.Piece;

    private static ItemPriority ParsePriority(string priority)
        => Enum.TryParse<ItemPriority>(priority, out var parsed) ? parsed : ItemPriority.Normal;

    private static EncryptedPayload? ToPayload(ArchivedEncryptedContent? encryptedContent)
        => encryptedContent is null
            || string.IsNullOrWhiteSpace(encryptedContent.Ciphertext)
            || string.IsNullOrWhiteSpace(encryptedContent.Nonce)
                ? null
                : new EncryptedPayload(encryptedContent.Ciphertext, encryptedContent.Nonce);

    /// <summary>
    /// The lists this import made, found again the two ways a file names them: an open list by its title,
    /// a private one by the nonce of its sealed half (see ArchivedTaskItem.LinkedSealedTaskLists). A
    /// private list is never found by title - its title is empty, and an older file that wrote that empty
    /// title for a link would otherwise land it on whichever private list came first.
    /// </summary>
    private sealed class CreatedTaskLists
    {
        private readonly Dictionary<string, Guid> _idsByTitle = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Guid> _idsBySealedHalf = new(StringComparer.Ordinal);

        public void Add(ArchivedTaskList archived, Guid id)
        {
            if (archived.IsPrivate)
            {
                if (archived.EncryptedContent is { } sealedContent)
                {
                    _idsBySealedHalf.TryAdd(sealedContent.Nonce, id);
                }
            }
            else if (archived.Title.Length > 0)
            {
                _idsByTitle.TryAdd(archived.Title, id);
            }
        }

        public IReadOnlyList<Guid> IdsOf(IEnumerable<string> titles, IEnumerable<string>? sealedHalves)
            => [.. titles.Select(title => _idsByTitle.TryGetValue(title, out var id) ? id : (Guid?)null)
                .Concat((sealedHalves ?? []).Select(nonce => _idsBySealedHalf.TryGetValue(nonce, out var id) ? id : (Guid?)null))
                .OfType<Guid>()
                .Distinct()];
    }
}
