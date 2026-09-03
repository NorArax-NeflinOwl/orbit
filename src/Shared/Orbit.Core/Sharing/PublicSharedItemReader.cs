using System.Globalization;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Tasks;
using Orbit.Core.Users;

namespace Orbit.Core.Sharing;

/// <summary>
/// Turns any of the four shareable kinds into the one flat shape a public link shows, and answers
/// whether a given user may make a link for it at all. The four repositories meet here rather than in
/// each command, so "what a link may show" is decided in one place instead of four.
/// </summary>
public sealed class PublicSharedItemReader
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly INoteRepository _noteRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IUserRepository _userRepository;

    public PublicSharedItemReader(
        INoteRepository noteRepository,
        ITaskRepository taskRepository,
        ICalendarEventRepository calendarEventRepository,
        IInventoryRepository inventoryRepository,
        IInventoryItemRepository inventoryItemRepository,
        IUserRepository userRepository)
    {
        _noteRepository = noteRepository;
        _taskRepository = taskRepository;
        _calendarEventRepository = calendarEventRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _userRepository = userRepository;
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
            _ => await ReadInventoryAsync(link, ownerDisplayName, cancellationToken)
        };
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

        var lines = note.Content
            .Select(line => new PublicSharedItemLine(line.Text, line.IsChecklistItem, line.IsChecked, Detail: null))
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

        var lines = taskList.Items
            .Select(item => new PublicSharedItemLine(
                item.Description, IsChecklistItem: true, item.IsCompleted, FormatDueDate(item.DueDateUtc)))
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
        var lines = items
            .Select(item => new PublicSharedItemLine(
                item.Name, IsChecklistItem: false, IsChecked: false,
                $"{item.Quantity.ToString("0.##", DisplayCulture)} · {item.Category}"))
            .ToList();

        var subtitle = items.Count == 1 ? "1 item" : $"{items.Count} items";

        return new PublicSharedItem(
            SharedItemType.Inventory, inventory.Name, subtitle, lines, ownerDisplayName, inventory.UpdatedAtUtc);
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
