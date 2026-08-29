namespace Orbit.Core.Transfer;

/// <summary>
/// Everything one account holds, in one shape that can be written to a file and read back. Deliberately
/// its own model rather than a bundle of the API's DTOs: those change whenever an endpoint changes, and
/// a file someone saved last month has to keep opening.
///
/// Ids are absent on purpose. Importing creates new items rather than restoring old ones - it never
/// overwrites anything, so it can be run into an account that already has things in it without any of
/// them being at risk.
/// </summary>
/// <param name="Version">
/// Bumped only when a later reader could not otherwise make sense of an older file. An importer
/// refusing a version it doesn't know is better than one guessing at it.
/// </param>
public sealed record OrbitArchive(
    int Version,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<ArchivedNote> Notes,
    IReadOnlyList<ArchivedTaskList> TaskLists,
    IReadOnlyList<ArchivedCalendarEvent> CalendarEvents,
    IReadOnlyList<ArchivedWarehouse> Warehouses)
{
    public const int CurrentVersion = 1;
}

/// <param name="EncryptedContent">
/// Present only for a private item, whose readable fields travel empty. The archive carries the sealed
/// bytes unchanged, so importing back into the same account restores something still readable - and
/// importing into a different one restores something nobody there can open, which is the whole promise
/// of marking it private.
/// </param>
public sealed record ArchivedNote(
    string Title, IReadOnlyList<ArchivedNoteLine> Content, bool IsPrivate, ArchivedEncryptedContent? EncryptedContent);

public sealed record ArchivedNoteLine(string Text, bool IsChecklistItem, bool IsChecked);

public sealed record ArchivedTaskList(
    string Title, IReadOnlyList<ArchivedTaskItem> Items, bool IsGroup, bool IsPrivate,
    ArchivedEncryptedContent? EncryptedContent, string Priority);

/// <param name="LinkedTaskListTitle">
/// A title rather than an id: ids are not carried, and a link is only meaningful if the list it points
/// at came along in the same file. Resolved back on import, and dropped if it doesn't resolve.
/// </param>
public sealed record ArchivedTaskItem(
    string Description, DateTimeOffset? DueDateUtc, bool IsCompleted, string? LinkedTaskListTitle,
    string OverdueNotificationChannel, bool RemindDaily, string DailyReminderNotificationChannel, TimeOnly DailyReminderTimeOfDay);

public sealed record ArchivedCalendarEvent(
    string Title, string? Description, string? Color, DateTimeOffset StartUtc, DateTimeOffset EndUtc, bool IsAllDay,
    ArchivedEventLocation? Location, IReadOnlyList<int> ReminderMinutesBeforeStart,
    string CreationNotificationChannel, string ReminderNotificationChannel);

public sealed record ArchivedEventLocation(string Address, double? Latitude, double? Longitude);

public sealed record ArchivedWarehouse(
    string Name, bool IsPrivate, ArchivedEncryptedContent? EncryptedContent, IReadOnlyList<ArchivedWarehouseItem> Items);

/// <param name="Unit">
/// Defaulted, and last, so an archive written before units existed still imports - it says nothing about
/// what its amounts were counted in, and pieces is the honest reading of that rather than a refusal.
/// </param>
public sealed record ArchivedWarehouseItem(
    string Name, string ProductType, string Category, decimal Quantity, decimal? MinimumQuantity,
    DateTimeOffset? ExpiryDate, string ExpiryNotificationChannel, string Unit = "Piece");

public sealed record ArchivedEncryptedContent(string Ciphertext, string Nonce);
