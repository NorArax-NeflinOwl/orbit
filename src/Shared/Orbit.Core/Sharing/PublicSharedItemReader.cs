using System.Globalization;
using Orbit.Core.Folders;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Places;
using Orbit.Core.Tasks;
using Orbit.Core.Users;

namespace Orbit.Core.Sharing;

/// <summary>
/// Turns any of the shareable kinds into the one flat shape a public link shows, and answers whether a
/// given user may make a link for it at all. The repositories meet here rather than in each command, so
/// "what a link may show" is decided in one place instead of one per kind.
/// </summary>
public sealed class PublicSharedItemReader
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly INoteRepository _noteRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IPlaceRepository _placeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFolderRepository _folderRepository;

    public PublicSharedItemReader(
        INoteRepository noteRepository,
        ITaskRepository taskRepository,
        ICalendarEventRepository calendarEventRepository,
        IInventoryRepository inventoryRepository,
        IInventoryItemRepository inventoryItemRepository,
        IPlaceRepository placeRepository,
        IUserRepository userRepository,
        IFolderRepository folderRepository)
    {
        _noteRepository = noteRepository;
        _taskRepository = taskRepository;
        _calendarEventRepository = calendarEventRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _placeRepository = placeRepository;
        _userRepository = userRepository;
        _folderRepository = folderRepository;
    }

    /// <summary>
    /// Whether ownerUserId may publish this item: they must own it outright, and it must not be
    /// private. Someone who merely holds a share of it may not - a link they made would outlive the
    /// share it came from and would not be the owner's to revoke.
    /// </summary>
    public async Task<bool> CanPublishAsync(
        Guid ownerUserId, SharedItemType itemType, Guid itemId, CancellationToken cancellationToken)
        => itemType switch
        {
            SharedItemType.Note => IsOwnedAndPublishable(
                await _noteRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken) is { } note
                    ? (note.UserId, note.IsPrivate)
                    : null,
                ownerUserId),
            SharedItemType.TaskList => IsOwnedAndPublishable(
                await _taskRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken) is { } taskList
                    ? (taskList.UserId, taskList.IsPrivate)
                    : null,
                ownerUserId),
            SharedItemType.CalendarEvent => IsOwnedAndPublishable(
                await _calendarEventRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken) is { } calendarEvent
                    ? (calendarEvent.UserId, false)
                    : null,
                ownerUserId),
            SharedItemType.Place => IsOwnedAndPublishable(
                await _placeRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken) is { } place
                    ? (place.UserId, place.IsPrivate)
                    : null,
                ownerUserId),
            // A folder has no privacy of its own - it is a tab, and what is sealed inside it is left out
            // when the link is read (see ReadFolderAsync). Owning it is the whole question here.
            SharedItemType.Folder => IsOwnedAndPublishable(
                await _folderRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken) is { } folder
                    ? (folder.UserId, false)
                    : null,
                ownerUserId),
            _ => IsOwnedAndPublishable(
                await _inventoryRepository.GetByIdAsync(ownerUserId, itemId, cancellationToken) is { } inventory
                    ? (inventory.UserId, inventory.IsPrivate)
                    : null,
                ownerUserId)
        };

    /// <summary>
    /// The item as a reader with the link sees it, or null if it has been deleted since the link was
    /// made - which reads to the page as "this link no longer points at anything", the same as a
    /// revoked one.
    /// </summary>
    public async Task<PublicSharedItem?> ReadAsync(PublicShareLink link, CancellationToken cancellationToken)
    {
        var owner = await _userRepository.GetByIdAsync(link.OwnerUserId, cancellationToken);
        var ownerDisplayName = owner?.DisplayName ?? "Someone";

        return link.ItemType switch
        {
            SharedItemType.Note => await ReadNoteAsync(link, ownerDisplayName, cancellationToken),
            SharedItemType.TaskList => await ReadTaskListAsync(link, ownerDisplayName, cancellationToken),
            SharedItemType.CalendarEvent => await ReadCalendarEventAsync(link, ownerDisplayName, cancellationToken),
            SharedItemType.Place => await ReadPlaceAsync(link, ownerDisplayName, cancellationToken),
            SharedItemType.Folder => await ReadFolderAsync(link, ownerDisplayName, cancellationToken),
            _ => await ReadInventoryAsync(link, ownerDisplayName, cancellationToken)
        };
    }

    /// <summary>
    /// A folder, and through it everything filed under it - each thing projected the way its own link
    /// would show it, so one page reads as a folder's worth of notes or lists one after another.
    ///
    /// **What is sealed is left out**, and so is what has been put away: a link is read by anybody who
    /// has it, and the archive is not what the owner meant to hand over. A folder that has since been
    /// emptied still opens, and says so by having nothing in it - the alternative is a link that reads
    /// as revoked because the owner tidied up.
    ///
    /// The calendar's folders are here too, although an event is never sealed: a folder is a folder.
    /// </summary>
    private async Task<PublicSharedItem?> ReadFolderAsync(
        PublicShareLink link, string ownerDisplayName, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(link.OwnerUserId, link.ItemId, cancellationToken);
        if (folder is null || folder.UserId != link.OwnerUserId)
        {
            return null;
        }

        var items = await WhatIsFiledUnderAsync(folder, ownerDisplayName, cancellationToken);
        var subtitle = items.Count == 1 ? "1 item" : $"{items.Count} items";
        return new PublicSharedItem(
            SharedItemType.Folder, folder.Name, subtitle, [], ownerDisplayName, folder.UpdatedAtUtc, items);
    }

    /// <inheritdoc cref="ReadFolderAsync"/>
    private async Task<IReadOnlyList<PublicSharedItem>> WhatIsFiledUnderAsync(
        Folder folder, string ownerDisplayName, CancellationToken cancellationToken)
    {
        switch (folder.Scope)
        {
            case FolderScope.Notes:
                var notes = await _noteRepository.GetAllAsync(folder.UserId, updatedSinceUtc: null, cancellationToken);
                return
                [
                    .. notes
                        .Where(note => note.FolderId == folder.Id && !note.IsPrivate && !note.IsArchived)
                        .Select(note => ProjectNote(note, ownerDisplayName))
                ];
            case FolderScope.Tasks:
                var taskLists = await _taskRepository.GetAllAsync(folder.UserId, updatedSinceUtc: null, cancellationToken);
                return
                [
                    .. taskLists
                        .Where(taskList => taskList.FolderId == folder.Id && !taskList.IsPrivate && !taskList.IsArchived)
                        .Select(taskList => ProjectTaskList(taskList, ownerDisplayName))
                ];
            case FolderScope.Calendar:
                var events = await _calendarEventRepository.GetAllAsync(folder.UserId, updatedSinceUtc: null, cancellationToken);
                return
                [
                    .. events
                        .Where(calendarEvent => calendarEvent.FolderId == folder.Id && !calendarEvent.IsArchived)
                        .Select(calendarEvent => ProjectCalendarEvent(calendarEvent, ownerDisplayName))
                ];
            default:
                var inventories = await _inventoryRepository.GetAllAsync(folder.UserId, updatedSinceUtc: null, cancellationToken);
                var shelves = new List<PublicSharedItem>();
                foreach (var inventory in inventories
                    .Where(inventory => inventory.FolderId == folder.Id && !inventory.IsPrivate && !inventory.IsArchived))
                {
                    shelves.Add(ProjectInventory(
                        inventory,
                        await _inventoryItemRepository.GetAllAsync(inventory.Id, cancellationToken),
                        ownerDisplayName));
                }

                return shelves;
        }
    }

    private async Task<PublicSharedItem?> ReadNoteAsync(PublicShareLink link, string ownerDisplayName, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(link.OwnerUserId, link.ItemId, cancellationToken);
        if (note is null || note.UserId != link.OwnerUserId || note.IsPrivate)
        {
            // Turning an already-published note private has to close the link with it, not merely stop
            // new ones being made.
            return null;
        }

        return ProjectNote(note, ownerDisplayName);
    }

    /// <summary>
    /// One note as a link shows it. Split from the read above so a folder's link can show a note it
    /// already has in hand without fetching it a second time - see ReadFolderAsync.
    /// </summary>
    private static PublicSharedItem ProjectNote(Note note, string ownerDisplayName)
    {
        var lines = note.Content
            .Select(line => new PublicSharedItemLine(
                line.Text, line.IsChecklistItem, line.IsChecked, Detail: null, line.IsFailed, line.Style,
                line.Marks, line.Table, line.Picture, line.Separator))
            .ToList();

        return new PublicSharedItem(
            SharedItemType.Note, note.Title, Subtitle: null, lines, ownerDisplayName, note.UpdatedAtUtc);
    }

    private async Task<PublicSharedItem?> ReadTaskListAsync(PublicShareLink link, string ownerDisplayName, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(link.OwnerUserId, link.ItemId, cancellationToken);
        if (taskList is null || taskList.UserId != link.OwnerUserId || taskList.IsPrivate)
        {
            return null;
        }

        return ProjectTaskList(taskList, ownerDisplayName);
    }

    /// <inheritdoc cref="ProjectNote"/>
    private static PublicSharedItem ProjectTaskList(TaskList taskList, string ownerDisplayName)
    {
        var lines = taskList.Items
            .Select(item => new PublicSharedItemLine(
                item.Description, IsChecklistItem: true, item.IsCompleted, FormatDueDate(item.DueDateUtc), item.IsFailed))
            .ToList();

        var completedCount = taskList.Items.Count(item => item.IsCompleted);
        var subtitle = taskList.Items.Count == 0
            ? "No items"
            : $"{completedCount} of {taskList.Items.Count} done";

        return new PublicSharedItem(
            SharedItemType.TaskList, taskList.Title, subtitle, lines, ownerDisplayName, taskList.UpdatedAtUtc);
    }

    private async Task<PublicSharedItem?> ReadCalendarEventAsync(PublicShareLink link, string ownerDisplayName, CancellationToken cancellationToken)
    {
        var calendarEvent = await _calendarEventRepository.GetByIdAsync(link.OwnerUserId, link.ItemId, cancellationToken);
        if (calendarEvent is null || calendarEvent.UserId != link.OwnerUserId)
        {
            return null;
        }

        return ProjectCalendarEvent(calendarEvent, ownerDisplayName);
    }

    /// <inheritdoc cref="ProjectNote"/>
    private static PublicSharedItem ProjectCalendarEvent(CalendarEvent calendarEvent, string ownerDisplayName)
    {
        var details = calendarEvent.Details;
        var lines = new List<PublicSharedItemLine>();
        if (!string.IsNullOrWhiteSpace(details.Description))
        {
            lines.Add(new PublicSharedItemLine(details.Description, IsChecklistItem: false, IsChecked: false, Detail: null));
        }

        if (details.Location is { } location && !string.IsNullOrWhiteSpace(location.Address))
        {
            lines.Add(new PublicSharedItemLine(location.Address, IsChecklistItem: false, IsChecked: false, Detail: "Location"));
        }

        // Guests are named nowhere: who else was invited is the owner's business, and a link can reach
        // anyone.
        return new PublicSharedItem(
            SharedItemType.CalendarEvent, details.Title, FormatEventTime(details), lines, ownerDisplayName,
            calendarEvent.UpdatedAtUtc);
    }

    private async Task<PublicSharedItem?> ReadInventoryAsync(PublicShareLink link, string ownerDisplayName, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryRepository.GetByIdAsync(link.OwnerUserId, link.ItemId, cancellationToken);
        if (inventory is null || inventory.UserId != link.OwnerUserId || inventory.IsPrivate)
        {
            return null;
        }

        var items = await _inventoryItemRepository.GetAllAsync(inventory.Id, cancellationToken);
        return ProjectInventory(inventory, items, ownerDisplayName);
    }

    /// <inheritdoc cref="ProjectNote"/>
    private static PublicSharedItem ProjectInventory(
        Inventory inventory, IReadOnlyList<InventoryItem> items, string ownerDisplayName)
    {
        var lines = items
            .Select(item => new PublicSharedItemLine(
                item.Name, IsChecklistItem: false, IsChecked: false,
                $"{item.Quantity.ToString("0.##", DisplayCulture)} · {string.Join(", ", item.Categories)}"))
            .ToList();

        var subtitle = items.Count == 1 ? "1 item" : $"{items.Count} items";

        return new PublicSharedItem(
            SharedItemType.Inventory, inventory.Name, subtitle, lines, ownerDisplayName, inventory.UpdatedAtUtc);
    }

    /// <summary>
    /// A place, which is the shortest of these: what it is called, where it is, and whatever the reader
    /// wrote about it. The point itself is not shown - a public link is read by anybody who has it, and
    /// coordinates are the one thing on a place that is worth being careful with. The address is what
    /// the owner wrote down to be read.
    /// </summary>
    private async Task<PublicSharedItem?> ReadPlaceAsync(
        PublicShareLink link, string ownerDisplayName, CancellationToken cancellationToken)
    {
        var place = await _placeRepository.GetByIdAsync(link.OwnerUserId, link.ItemId, cancellationToken);
        if (place is null || place.UserId != link.OwnerUserId || place.IsPrivate)
        {
            // Sealing a place that already had a link closes the link with it, rather than merely
            // stopping new ones being made - the same rule a note follows.
            return null;
        }

        var lines = new List<PublicSharedItemLine>();
        if (!string.IsNullOrWhiteSpace(place.Description))
        {
            lines.Add(new PublicSharedItemLine(place.Description, IsChecklistItem: false, IsChecked: false, Detail: null));
        }

        return new PublicSharedItem(
            SharedItemType.Place, place.Name, place.Where.Address, lines, ownerDisplayName, place.UpdatedAtUtc);
    }

    private static bool IsOwnedAndPublishable((Guid OwnerUserId, bool IsPrivate)? item, Guid ownerUserId)
        => item is { } value && value.OwnerUserId == ownerUserId && !value.IsPrivate;

    private static string? FormatDueDate(DateTimeOffset? dueDateUtc)
        => dueDateUtc is null ? null : $"Due {dueDateUtc.Value.ToLocalTime().ToString("MMM d, HH:mm", DisplayCulture)}";

    private static string FormatEventTime(CalendarEventDetails details)
        => details.IsAllDay
            ? details.StartUtc.ToLocalTime().ToString("MMM d, yyyy", DisplayCulture) + " · All day"
            : $"{details.StartUtc.ToLocalTime().ToString("MMM d, yyyy HH:mm", DisplayCulture)} – {details.EndUtc.ToLocalTime().ToString("HH:mm", DisplayCulture)}";
}
