using System.Globalization;

namespace Orbit.Core.Calendar.Reminders;

/// <summary>Builds the subject and body of a calendar event reminder email.</summary>
public static class EventReminderEmailContent
{
    public static (string Subject, string Body) Build(CalendarEventDetails details, int minutesBeforeStart)
    {
        var subject = $"Reminder: {details.Title}";

        var bodyLines = new List<string>
        {
            $"The event \"{details.Title}\" starts {FormatLeadTime(minutesBeforeStart)}.",
            $"Start: {details.StartUtc.LocalDateTime:dd.MM.yyyy HH:mm}"
        };

        if (!string.IsNullOrWhiteSpace(details.Description))
        {
            bodyLines.Add($"Description: {details.Description}");
        }

        if (details.Location is { } location)
        {
            bodyLines.Add($"Location: {FormatLocation(location)}");
        }

        return (subject, string.Join(Environment.NewLine, bodyLines));
    }

    private static string FormatLeadTime(int minutesBeforeStart)
        => minutesBeforeStart switch
        {
            0 => "now",
            _ when minutesBeforeStart % 60 == 0 => $"in {minutesBeforeStart / 60} hr",
            _ => $"in {minutesBeforeStart} min"
        };

    private static string FormatLocation(EventLocation location)
        => string.IsNullOrWhiteSpace(location.Address)
            // Invariant culture, not the server/thread's current culture: these are coordinates meant
            // to be pasted into a map, so they must always use a period as the decimal separator
            // rather than the comma pl-PL (and other cultures) format doubles with.
            ? $"{location.Latitude.ToString("F5", CultureInfo.InvariantCulture)}, {location.Longitude.ToString("F5", CultureInfo.InvariantCulture)}"
            : location.Address;
}
