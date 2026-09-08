using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens;

/// <summary>
/// When something last changed, said the way somebody would say it - the same four answers
/// Orbit.Web's Notes page gives (see its WhenLastChanged).
///
/// Within the last week the weekday is enough and reads faster than a date; past that a weekday is
/// ambiguous - "Tuesday" could be any of them - so it becomes a date. The phone used to print the whole
/// timestamp, which is the line under every card on the screen and the one nobody reads.
/// </summary>
public static class LastChanged
{
    /// <param name="nowUtc">
    /// Taken rather than read off the machine: "today" is whatever the injected clock says, which is
    /// what lets a test about the wording be a test about the wording.
    /// </param>
    public static string Describe(DateTimeOffset updatedAtUtc, DateTimeOffset nowUtc, Translations translations)
    {
        var when = updatedAtUtc.ToLocalTime().DateTime;
        var daysAgo = (nowUtc.ToLocalTime().Date - when.Date).Days;

        return daysAgo switch
        {
            0 => translations["Today"],
            1 => translations["Yesterday"],
            // Only backwards: something dated ahead of now - a clock that has been put back, a row that
            // arrived from a device running fast - is a date rather than a weekday nobody can place.
            > 1 and < 7 => when.ToString("dddd", translations.DisplayCulture),
            _ => when.ToString("d", translations.DisplayCulture)
        };
    }
}
