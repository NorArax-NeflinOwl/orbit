using System.Globalization;

namespace Orbit.Core.Calendar.Reminders;

/// <summary>Builds the subject and body of a calendar event reminder email.</summary>
public static class EventReminderEmailContent
{
    public static (string Subject, string Body) Build(CalendarEventDetails details, int minutesBeforeStart)
    {
        var subject = $"Przypomnienie: {details.Title}";

        var bodyLines = new List<string>
        {
            $"Wydarzenie \"{details.Title}\" zaczyna się {FormatLeadTime(minutesBeforeStart)}.",
            $"Początek: {details.StartUtc.LocalDateTime:dd.MM.yyyy HH:mm}"
        };

        if (!string.IsNullOrWhiteSpace(details.Description))
        {
            bodyLines.Add($"Opis: {details.Description}");
        }

        if (details.Location is { } location)
        {
            bodyLines.Add($"Lokalizacja: {FormatLocation(location)}");
        }

        return (subject, string.Join(Environment.NewLine, bodyLines));
    }

    private static string FormatLeadTime(int minutesBeforeStart)
        => minutesBeforeStart switch
        {
            0 => "teraz",
            _ when minutesBeforeStart % 60 == 0 => $"za {minutesBeforeStart / 60} godz.",
            _ => $"za {minutesBeforeStart} min"
        };

    private static string FormatLocation(EventLocation location)
        => string.IsNullOrWhiteSpace(location.Address)
            // Invariant culture, not the server/thread's current culture: these are coordinates meant
            // to be pasted into a map, so they must always use a period as the decimal separator
            // rather than the comma pl-PL (and other cultures) format doubles with.
            ? $"{location.Latitude.ToString("F5", CultureInfo.InvariantCulture)}, {location.Longitude.ToString("F5", CultureInfo.InvariantCulture)}"
            : location.Address;
}
