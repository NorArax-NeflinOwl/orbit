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
            ? $"{location.Latitude:F5}, {location.Longitude:F5}"
            : location.Address;
}
