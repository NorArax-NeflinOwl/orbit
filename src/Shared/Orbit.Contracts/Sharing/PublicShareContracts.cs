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
    DateTimeOffset UpdatedAtUtc);

/// <param name="IsFailed">Crossed out rather than ticked - see Orbit.Core.Tasks.TaskItem.IsFailed.</param>
/// <param name="Style">
/// What the line is, where the item is a note - a heading, a line of a list, ordinary writing. The word
/// Orbit.Core.Notes.NoteLineStyle is named by; anything else reads as "Body" (see NoteLineStyles.Read).
/// Last and defaulted, because every other kind of item is a list of things and says nothing here.
/// </param>
public sealed record PublicSharedItemLineDto(
    string Text, bool IsChecklistItem, bool IsChecked, string? Detail, bool IsFailed = false,
    string Style = "Body");

/// <param name="AlreadyHeld">The caller already had access, so nothing new was granted.</param>
public sealed record ClaimPublicShareLinkResponse(string ItemType, Guid ItemId, bool AlreadyHeld);
