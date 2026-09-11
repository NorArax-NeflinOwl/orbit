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
/// <param name="Places">
/// The places this account keeps - see <see cref="ArchivedPlace"/>. Defaulted, and last, rather than a
/// reason to bump the version: a file written before places could be exported says nothing here and
/// reads as an account that kept none, and one written now still imports into an older Orbit, which
/// reads past a field it does not know.
/// </param>
public sealed record OrbitArchive(
    int Version,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<ArchivedNote> Notes,
    IReadOnlyList<ArchivedTaskList> TaskLists,
    IReadOnlyList<ArchivedCalendarEvent> CalendarEvents,
    IReadOnlyList<ArchivedInventory> Inventories,
    IReadOnlyList<ArchivedPlace>? Places = null)
{
    public const int CurrentVersion = 1;

    /// <summary>The places as something to read without a null check - see <see cref="Places"/>.</summary>
    public IReadOnlyList<ArchivedPlace> AllPlaces => Places ?? [];

    /// <summary>
    /// The archive as it may be handed back to the server: every private place with its readable half
    /// emptied, leaving only the sealed one. A file may carry a private place opened - that is what an
    /// export of places is (see <see cref="ArchivedPlace"/>) - but the server was never allowed to read
    /// one, and an import is no reason to start. It would throw the words away anyway (a sealed place
    /// stores its columns empty); this is so they are never sent in the first place.
    /// </summary>
    public OrbitArchive WithPrivatePlacesClosed()
        => Places is null
            ? this
            : this with { Places = [.. Places.Select(place => place.IsPrivate ? place.Closed() : place)] };
}

/// <param name="EncryptedContent">
/// Present only for a private item, whose readable fields travel empty. The archive carries the sealed
/// bytes unchanged, so importing back into the same account restores something still readable - and
/// importing into a different one restores something nobody there can open, which is the whole promise
/// of marking it private.
/// </param>
public sealed record ArchivedNote(
    string Title, IReadOnlyList<ArchivedNoteLine> Content, bool IsPrivate, ArchivedEncryptedContent? EncryptedContent);

/// <param name="IsFailed">Crossed out rather than ticked - see Orbit.Core.Notes.NoteContentLine.IsFailed.</param>
public sealed record ArchivedNoteLine(string Text, bool IsChecklistItem, bool IsChecked, bool IsFailed = false);

public sealed record ArchivedTaskList(
    string Title, IReadOnlyList<ArchivedTaskItem> Items, bool IsGroup, bool IsPrivate,
    ArchivedEncryptedContent? EncryptedContent, string Priority);

/// <param name="LinkedTaskListTitle">
/// A title rather than an id: ids are not carried, and a link is only meaningful if the list it points
/// at came along in the same file. Resolved back on import, and dropped if it doesn't resolve.
/// </param>
/// <param name="LinkedTaskListTitle">
/// The first list this entry stands for, by title. Kept so an archive written by this version still
/// imports into an older one, and so an archive written by an older one still reads here - a file is
/// read long after it is written, which is the whole point of having one.
/// </param>
/// <param name="LinkedTaskListTitles">
/// Every list it stands for. Defaulted, and last: an archive written before entries could name more
/// than one says nothing here, and the single title above is then the whole answer.
/// </param>
public sealed record ArchivedTaskItem(
    string Description, DateTimeOffset? DueDateUtc, bool IsCompleted, string? LinkedTaskListTitle,
    string OverdueNotificationChannel, bool RemindDaily, string DailyReminderNotificationChannel,
    TimeOnly DailyReminderTimeOfDay, IReadOnlyList<string>? LinkedTaskListTitles = null,
    /// <summary>
    /// What the entry is filed under - see Orbit.Core.Tasks.TaskItem.Categories. Defaulted and last for
    /// the same reason the titles above are: an archive written before categories existed says nothing
    /// here, and reads as an entry nobody has filed.
    /// </summary>
    IReadOnlyList<string>? Categories = null,
    /// <summary>
    /// Closed without being done - see Orbit.Core.Tasks.TaskItem.IsFailed. Defaulted and last for the
    /// same reason everything above it is: a file written before the cross existed says nothing here,
    /// and reads as an entry that was simply not done.
    /// </summary>
    bool IsFailed = false,
    /// <summary>
    /// The private lists it stands for, each by the nonce of its sealed half rather than by title: a
    /// private list's title is empty outside the key, so a title would name whichever private list came
    /// first. AES-GCM never repeats a nonce under one key, so it names that one sealing, and the list
    /// carries it in the same file. Defaulted and last for the reason everything above is; an older
    /// reader drops these links, as it drops any link it cannot resolve.
    /// </summary>
    IReadOnlyList<string>? LinkedSealedTaskLists = null,
    /// <summary>
    /// When the entry was ticked off - see Orbit.Core.Tasks.TaskItem.CompletedAtUtc. Defaulted and last
    /// for the reason everything above is: a file written before the time was kept says nothing here,
    /// and its ticked entries come back done at a time nobody knows, which is what they were.
    /// </summary>
    DateTimeOffset? CompletedAtUtc = null)
{
    /// <summary>The categories as something to read without a null check.</summary>
    public IReadOnlyList<string> AllCategories => Categories ?? [];

    /// <summary>Both shapes read as one, newest first - see the two parameters above.</summary>
    public IReadOnlyList<string> AllLinkedTaskListTitles
        => LinkedTaskListTitles is { Count: > 0 } titles
            ? titles
            : LinkedTaskListTitle is { } single ? [single] : [];
}

/// <param name="CreationNotificationChannel">
/// No longer used: an event no longer announces itself to its own owner. Kept on the record so a file
/// written before it went away still reads, and written back out as "None" so one written now still
/// opens in an older Orbit.
/// </param>
public sealed record ArchivedCalendarEvent(
    string Title, string? Description, string? Color, DateTimeOffset StartUtc, DateTimeOffset EndUtc, bool IsAllDay,
    ArchivedEventLocation? Location, IReadOnlyList<int> ReminderMinutesBeforeStart,
    string CreationNotificationChannel, string ReminderNotificationChannel);

public sealed record ArchivedEventLocation(string Address, double? Latitude, double? Longitude);

public sealed record ArchivedInventory(
    string Name, bool IsPrivate, ArchivedEncryptedContent? EncryptedContent, IReadOnlyList<ArchivedInventoryItem> Items);

/// <param name="Unit">
/// Defaulted, and last, so an archive written before units existed still imports - it says nothing about
/// what its amounts were counted in, and pieces is the honest reading of that rather than a refusal.
/// </param>
/// <param name="Category">
/// What an archive written before a shelf item could be filed under several things says. Still read on
/// the way in, and written as the first of <paramref name="Categories"/> on the way out, so an archive
/// this build makes still imports into an older one - the whole point of the format.
/// </param>
/// <param name="Categories">
/// Everything it is filed under. Defaulted and last for the same reason Unit is: an archive that
/// predates it says nothing here, and its single Category is the honest reading of that.
/// </param>
public sealed record ArchivedInventoryItem(
    string Name, string ProductType, string Category, decimal Quantity, decimal? MinimumQuantity,
    DateTimeOffset? ExpiryDate, string ExpiryNotificationChannel, string Unit = "Piece",
    IReadOnlyList<string>? Categories = null)
{
    /// <summary>Whichever shape the archive used, read as one - see <see cref="Categories"/>.</summary>
    public IReadOnlyList<string> AllCategories
        => Categories is { Count: > 0 } categories ? categories : Category.Length > 0 ? [Category] : [];
}

/// <summary>
/// One place - see Orbit.Core.Places.Place.
///
/// <b>The one part of an archive a private item does not travel sealed in.</b> Everything else keeps its
/// sealed bytes and nothing more, because that is what marking something private promised. A place is
/// private by default, so an export that did the same would be a file of empty rows - and the reader who
/// asks for their places asks to be able to read them somewhere else. The server still writes a private
/// place with its readable fields empty, because it cannot do otherwise; the browser that holds the key
/// fills them in before the file is saved, and says first that the file is then readable by anyone who
/// gets hold of it.
/// </summary>
/// <param name="TaskListTitles">
/// The lists it belongs to, by title, for the reason a task entry's links are carried that way - see
/// <see cref="ArchivedTaskItem.LinkedTaskListTitle"/>.
/// </param>
/// <param name="EncryptedContent">
/// Kept beside the opened fields for a private place, so importing the file back restores it sealed the
/// way a private note is restored: the server can store a sealed half it is handed, but it cannot make
/// one out of words.
/// </param>
/// <param name="SealedTaskLists">
/// The private lists it belongs to, by the nonce of each one's sealed half - see
/// <see cref="ArchivedTaskItem.LinkedSealedTaskLists"/>. Defaulted and last, so a file written before
/// these links travelled still reads, as a place that belongs to no private list.
/// </param>
public sealed record ArchivedPlace(
    string Name, string Description, ArchivedEventLocation Where, string Colour, string Priority,
    IReadOnlyList<string> TaskListTitles, bool IsPrivate, ArchivedEncryptedContent? EncryptedContent,
    IReadOnlyList<string>? SealedTaskLists = null)
{
    /// <summary>This place with its readable half emptied - what the server holds for a sealed one.</summary>
    public ArchivedPlace Closed()
        => this with { Name = string.Empty, Description = string.Empty, Where = new ArchivedEventLocation(string.Empty, 0, 0) };
}

public sealed record ArchivedEncryptedContent(string Ciphertext, string Nonce);
