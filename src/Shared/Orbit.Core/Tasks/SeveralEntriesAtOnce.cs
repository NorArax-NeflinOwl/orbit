namespace Orbit.Core.Tasks;

/// <summary>
/// How a notice that speaks about several checklist entries at once names them, and where tapping it
/// goes. Shared by the overdue notices and the daily reminders so the two say it the same way.
///
/// One notice rather than one per entry, because the second one was never seen: the web shows a banner
/// for the newest entry only, and both clients keep a minimum gap between banners (see MainLayout and
/// ForegroundNotices), so two entries falling due in the same minute cost the reader one of them. The
/// user reported exactly that on 2026-09-21, with two entries due at 17:00 and a single banner between
/// them.
/// </summary>
public static class SeveralEntriesAtOnce
{
    /// <summary>
    /// Each entry with the list it is on beside it, in the order the notice gathered them - the list is
    /// named because the same errand is often written on two of them, and "Medicines, Medicines" is a
    /// notice that answers nothing. Unquoted, unlike the sentence about a single entry: what fills this
    /// hole is a list of things rather than one name, and quotes around each of them read as stammering
    /// in a line already broken up by commas and brackets.
    /// </summary>
    public static string Naming(IEnumerable<(string Description, string TaskListTitle)> entries)
        => string.Join(", ", entries.Select(entry => $"{entry.Description} ({entry.TaskListTitle})"));

    /// <summary>
    /// Where it leads: the one list while every entry is on the same one, and the lists themselves
    /// otherwise. There is no page for "these four entries", and landing on whichever of the four lists
    /// came first would answer a question nobody asked.
    /// </summary>
    public static string LeadingTo(IEnumerable<Guid> taskListIds)
        => taskListIds.Distinct().ToList() is [var theOnlyList] ? $"/tasks/{theOnlyList}" : "/tasks";
}
