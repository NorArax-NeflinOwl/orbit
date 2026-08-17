using System.Text.Json;
using Orbit.Core.Calendar;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

/// <summary>
/// Maps between <see cref="CalendarEventEntity"/> and <see cref="CalendarEvent"/> - shared by
/// <see cref="CalendarEventRepository"/> (per-user CRUD) and <see cref="EventReminderRepository"/>
/// (cross-user reminder scanning), so both stay in sync with the entity shape instead of keeping two
/// copies of the same mapping.
/// </summary>
internal static class CalendarEventEntityMapper
{
    public static CalendarEvent ToDomain(CalendarEventEntity entity)
    {
        var recurrence = entity.RecurrenceFrequency is null
            ? null
            : new EventRecurrence(
                Enum.Parse<RecurrenceFrequency>(entity.RecurrenceFrequency),
                entity.RecurrenceIntervalCount ?? 1,
                entity.RecurrenceUntilUtc);

        var location = entity.LocationLatitude is { } latitude && entity.LocationLongitude is { } longitude
            ? new EventLocation(entity.LocationAddress, latitude, longitude)
            : null;

        var details = new CalendarEventDetails(
            entity.Title,
            entity.Description,
            location,
            entity.Color,
            entity.StartUtc,
            entity.EndUtc,
            entity.IsAllDay,
            recurrence,
            JsonSerializer.Deserialize<List<string>>(entity.GuestsJson) ?? [],
            JsonSerializer.Deserialize<List<int>>(entity.RemindersJson) ?? [],
            entity.NotifyOnCreation,
            entity.NotifyBeforeStart);

        return CalendarEvent.FromPersistence(entity.Id, entity.UserId, details, entity.CreatedAtUtc, entity.UpdatedAtUtc);
    }

    public static CalendarEventEntity ToEntity(CalendarEvent calendarEvent)
    {
        var details = calendarEvent.Details;
        return new CalendarEventEntity
        {
            Id = calendarEvent.Id,
            UserId = calendarEvent.UserId,
            Title = details.Title,
            Description = details.Description,
            LocationAddress = details.Location?.Address,
            LocationLatitude = details.Location?.Latitude,
            LocationLongitude = details.Location?.Longitude,
            Color = details.Color,
            StartUtc = details.StartUtc,
            EndUtc = details.EndUtc,
            IsAllDay = details.IsAllDay,
            RecurrenceFrequency = details.Recurrence?.Frequency.ToString(),
            RecurrenceIntervalCount = details.Recurrence?.IntervalCount,
            RecurrenceUntilUtc = details.Recurrence?.UntilUtc,
            GuestsJson = JsonSerializer.Serialize(details.Guests),
            RemindersJson = JsonSerializer.Serialize(details.ReminderMinutesBeforeStart),
            NotifyOnCreation = details.NotifyOnCreation,
            NotifyBeforeStart = details.NotifyBeforeStart,
            CreatedAtUtc = calendarEvent.CreatedAtUtc,
            UpdatedAtUtc = calendarEvent.UpdatedAtUtc
        };
    }
}
