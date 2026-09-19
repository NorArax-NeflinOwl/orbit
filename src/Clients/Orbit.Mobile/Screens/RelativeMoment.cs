using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens;

/// <summary>
/// How long ago something was, in the reader's own words. One wording for the whole app: the dashboard's
/// cards and the rows that show a person both say when there was last anything, and two of them would
/// have the same moment read two ways on two screens showing the same conversation.
/// </summary>
public static class RelativeMoment
{
    /// <summary>
    /// Just now under a minute, then minutes, then hours, then days - and a plain date past a month,
    /// where "43d ago" is arithmetic the reader has to do rather than an answer.
    /// </summary>
    public static string Ago(DateTimeOffset moment, TimeProvider timeProvider, Translations translations)
    {
        var elapsed = timeProvider.GetUtcNow() - moment;

        return elapsed switch
        {
            { TotalMinutes: < 1 } => translations["Just now"],
            { TotalHours: < 1 } => translations.Format("{0}m ago", (int)elapsed.TotalMinutes),
            { TotalDays: < 1 } => translations.Format("{0}h ago", (int)elapsed.TotalHours),
            { TotalDays: < 30 } => translations.Format("{0}d ago", (int)elapsed.TotalDays),
            _ => moment.ToLocalTime().ToString("d MMM yyyy", translations.DisplayCulture)
        };
    }
}
