using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// How long before an event to say something, and what to call that. The same eleven Orbit.Web offers,
/// so a reminder set in one client reads the same in the other.
/// </summary>
public sealed record ReminderChoice(int MinutesBefore, string Name)
{
    private static readonly (int Minutes, string Label)[] Presets =
    [
        (0, "When it starts"),
        (5, "5 minutes before"),
        (10, "10 minutes before"),
        (15, "15 minutes before"),
        (30, "30 minutes before"),
        (60, "1 hour before"),
        (120, "2 hours before"),
        (720, "12 hours before"),
        (1440, "1 day before"),
        (2880, "2 days before"),
        (10080, "1 week before")
    ];

    public static IReadOnlyList<ReminderChoice> All(Translations translations)
        => [.. Presets.Select(preset => new ReminderChoice(preset.Minutes, translations[preset.Label]))];

    /// <summary>
    /// What to call a reminder that is already set. Orbit.Web offers a custom number of minutes as well
    /// as the presets, so a value arriving from there need not be one of ours - and saying "80 minutes
    /// before" is better than dropping a reminder somebody set.
    /// </summary>
    public static string Describe(int minutesBefore, Translations translations)
        => All(translations).FirstOrDefault(choice => choice.MinutesBefore == minutesBefore) is { } known
            ? known.Name
            : translations.Format("{0} minutes before", minutesBefore);
}

/// <summary>One reminder already set on an event, as the screen lists it.</summary>
public sealed record ReminderRow(int MinutesBefore, string Name);

/// <summary>
/// Somebody invited to an event, or who could be. The id is what travels; the name is what a reader
/// needs, and it comes from this phone's contacts - which is why an invitation made elsewhere can turn
/// up with a name this phone does not know.
/// </summary>
public sealed record GuestRow(Guid UserId, string Name);
