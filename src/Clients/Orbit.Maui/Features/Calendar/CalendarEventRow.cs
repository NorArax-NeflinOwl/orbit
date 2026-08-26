using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Maui.Features.Calendar;

/// <summary>One row of the calendar screen - the same shape as the notes and task-list rows.</summary>
public sealed record CalendarEventRow(
	Guid LocalId, string Title, DateTimeOffset StartUtc, DateTimeOffset EndUtc, bool IsAllDay,
	bool HasUnsentChanges, OfflineEditRefusal Refusal)
{
	public static CalendarEventRow From(LocalCalendarEvent calendarEvent, bool hasUnsentChanges, INetworkStatus networkStatus)
		=> new(
			calendarEvent.LocalId, calendarEvent.Details.Title, calendarEvent.Details.StartUtc,
			calendarEvent.Details.EndUtc, calendarEvent.Details.IsAllDay, hasUnsentChanges,
			OfflineEditPolicy.Evaluate(calendarEvent, networkStatus));

	public string When => IsAllDay
		? $"{StartUtc.LocalDateTime:d} · all day"
		: $"{StartUtc.LocalDateTime:g} – {EndUtc.LocalDateTime:t}";

	/// <summary>Empty when there is nothing worth saying, which is the common case.</summary>
	public string Status => Refusal switch
	{
		OfflineEditRefusal.SharedWithYou => "Shared with you - read-only until you're back online",
		OfflineEditRefusal.SharedWithOthers => "Shared with others - read-only until you're back online",
		_ => HasUnsentChanges ? "Waiting to sync" : string.Empty
	};

	public bool HasStatus => Status.Length > 0;
}
