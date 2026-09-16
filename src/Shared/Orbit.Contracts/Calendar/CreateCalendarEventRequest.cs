namespace Orbit.Contracts.Calendar;

/// <param name="FolderId">
/// Where to file it as it is made - the tab the reader is standing on. Null for none. Filing an event
/// that already exists is its own endpoint (PUT /api/calendar-events/{id}/folder), for the reason
/// Orbit.Core.Calendar.CalendarEvent.MoveToFolder gives.
/// </param>
public sealed record CreateCalendarEventRequest(CalendarEventDetailsRequest Details, Guid? FolderId = null);
