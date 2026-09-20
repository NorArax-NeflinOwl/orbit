namespace Orbit.Web.Services;

/// <summary>
/// How long ago something was, in the reader's own words - "just now", "12m ago", "3h ago",
/// "Yesterday", "Wed", "Aug 12".
///
/// One wording for the whole browser: the dashboard's cards and the rows that show a person both say
/// when there was last anything, and two of them would have the same moment read two ways on two screens
/// showing the same conversation. Orbit.Mobile keeps its own (Orbit.Mobile.Screens.RelativeMoment) - the
/// two clients word it differently on purpose, each following its own design.
/// </summary>
public static class RelativeMoment
{
    public static string Ago(DateTimeOffset valueUtc, TimeProvider timeProvider, Translations translations)
    {
        var value = valueUtc.ToLocalTime();
        var now = timeProvider.GetLocalNow();
        var elapsed = now - value;

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return translations["just now"];
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return translations.Format("{0}m ago", (int)elapsed.TotalMinutes);
        }

        // Hours only while it is still the same day: "26h ago" on a Tuesday for something on Monday
        // morning is arithmetic the reader has to do, where "Yesterday" is the answer.
        if (value.Date == now.Date)
        {
            return translations.Format("{0}h ago", (int)elapsed.TotalHours);
        }

        if (value.Date == now.Date.AddDays(-1))
        {
            return translations["Yesterday"];
        }

        // Inside the week, the day's name says it: "Wed" places it without counting back.
        if (elapsed < TimeSpan.FromDays(7))
        {
            return value.ToString("ddd", translations.DisplayCulture);
        }

        return value.ToString("MMM d", translations.DisplayCulture);
    }
}
