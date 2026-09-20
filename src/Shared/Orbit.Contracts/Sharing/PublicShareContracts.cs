using Orbit.Contracts.Notes;

namespace Orbit.Contracts.Sharing;

/// <param name="ItemType">One of "Note", "TaskList", "CalendarEvent", "Inventory".</param>
public sealed record CreatePublicShareLinkRequest(string ItemType, Guid ItemId);

/// <param name="Token">The secret that makes the link work - the caller builds the URL around it, since only the browser knows what origin it is running on.</param>
public sealed record PublicShareLinkDto(string Token, DateTimeOffset CreatedAtUtc);

/// <summary>What a reader with the link sees - see Orbit.Core.Sharing.PublicSharedItem for what is deliberately left out.</summary>
public sealed record PublicSharedItemDto(
    string ItemType,
    string Title,
    string? Subtitle,
    IReadOnlyList<PublicSharedItemLineDto> Lines,
    string OwnerDisplayName,
    DateTimeOffset UpdatedAtUtc,
    /// <summary>
    /// Where ItemType is "Folder", everything filed under it - each shaped the way its own link would
    /// send it, so one page shows a folder's worth of notes or lists one after another. Null for every
    /// other kind, which is a single thing. Last and defaulted so an older client reading a link to
    /// something else is unaffected.
    /// </summary>
    IReadOnlyList<PublicSharedItemDto>? Items = null);

/// <param name="IsFailed">Crossed out rather than ticked - see Orbit.Core.Tasks.TaskItem.IsFailed.</param>
/// <param name="Style">
/// What the line is, where the item is a note - a heading, a line of a list, ordinary writing. The word
/// Orbit.Core.Notes.NoteLineStyle is named by; anything else reads as "Body" (see NoteLineStyles.Read).
/// Last and defaulted, because every other kind of item is a list of things and says nothing here.
/// </param>
/// <param name="Marks">
/// The marks on stretches of words inside the line, where the item is a note - see
/// Orbit.Contracts.Notes.NoteTextRunDto, which is the same shape a note's own lines travel with. Null for
/// everything else, as for a line nobody marked.
/// </param>
/// <param name="Table">The table this line is, where the item is a note and the line one of its tables - see NoteTableDto.</param>
/// <param name="Picture">The picture this line is, where the item is a note - its bytes are at GET /api/public/{token}/pictures/{id}.</param>
/// <param name="Separator">The rule this line is, where the item is a note - see NoteSeparatorLineDto.</param>
public sealed record PublicSharedItemLineDto(
    string Text, bool IsChecklistItem, bool IsChecked, string? Detail, bool IsFailed = false,
    string Style = "Body", IReadOnlyList<NoteTextRunDto>? Marks = null, NoteTableDto? Table = null,
    NotePictureLineDto? Picture = null, NoteSeparatorLineDto? Separator = null)
{
    /// <summary>The marks as something to read without a null check - see <see cref="Marks"/>.</summary>
    public IReadOnlyList<NoteTextRunDto> AllMarks => Marks ?? [];
}

/// <param name="AlreadyHeld">The caller already had access, so nothing new was granted.</param>
public sealed record ClaimPublicShareLinkResponse(string ItemType, Guid ItemId, bool AlreadyHeld);
