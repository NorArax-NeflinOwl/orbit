using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.MoveCalendarEventToFolder;

/// <summary>
/// Files one event under <paramref name="FolderId"/>, or under none when it is null - which puts it back
/// in Public (see Orbit.Core.Folders.BuiltInFolder).
///
/// Its own command rather than a field on the update, for the reason CalendarEvent.MoveToFolder gives:
/// an update replaces the whole event, so null there would have to mean "leave it alone" and there would
/// be no way left to say "take it out of the folder". Mirrors MoveNoteToFolderCommand.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record MoveCalendarEventToFolderCommand(Guid UserId, Guid CalendarEventId, Guid? FolderId) : IRequest<bool>;
