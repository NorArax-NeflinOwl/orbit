using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.CreateCalendarEvent;

/// <param name="FolderId">
/// The folder to file it under as it is made, or null for none - which is how something new lands in the
/// tab the reader is standing on (see Orbit.Core.Folders.FolderScope). Defaulted and last, so a client
/// that has not heard of folders on the calendar still creates events.
/// </param>
[ClientAction(ClientActionCategory.Save)]
public sealed record CreateCalendarEventCommand(
    Guid UserId, CalendarEventDetails Details, Guid? FolderId = null) : IRequest<Guid>;
