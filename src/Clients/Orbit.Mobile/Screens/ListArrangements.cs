using Orbit.Core.Abstractions;

namespace Orbit.Mobile.Screens;

/// <summary>
/// The four facts the ordering and the narrowing need about a row, whatever kind of row it is. A note
/// and a task list are different things and are read the same way, so the sorting is written once
/// against this rather than twice against them.
/// </summary>
/// <param name="Priority">The wire value - "Low", "Normal" or "High" - rather than the word shown.</param>
public sealed record ListRowFacts(string Name, DateTimeOffset ChangedAtUtc, bool IsPinned, string Priority);

/// <summary>
/// Puts a list screen's rows in the reader's order and takes out what they have narrowed away.
///
/// Pinned first, always, whatever the order is: a pin is the reader saying "this one, above the rest",
/// and an order that moved it back down would be answering a question they did not ask. See
/// <see cref="ListSortOrder"/>, where the same rule is written for the reader.
/// </summary>
public static class ListArrangements
{
    public static IEnumerable<T> Apply<T>(
        IEnumerable<T> rows, ListArrangement arrangement, Func<T, ListRowFacts> describe)
    {
        var kept = rows.Where(row => Keeps(arrangement.Filter, describe(row)));

        // Pinned first, then whatever was asked for. OrderBy is stable in .NET, so two rows the chosen
        // order cannot tell apart stay in the order the store handed them over in.
        var ordered = kept.OrderByDescending(row => describe(row).IsPinned);

        return arrangement.SortOrder switch
        {
            ListSortOrder.Name => ordered.ThenBy(row => describe(row).Name, StringComparer.CurrentCultureIgnoreCase),
            ListSortOrder.Priority => ordered.ThenByDescending(row => Rank(describe(row).Priority)),
            _ => ordered.ThenByDescending(row => describe(row).ChangedAtUtc)
        };
    }

    private static bool Keeps(ListFilter filter, ListRowFacts row) => filter switch
    {
        ListFilter.Pinned => row.IsPinned,
        ListFilter.HighPriority => Rank(row.Priority) == (int)ItemPriority.High,
        ListFilter.NormalPriority => Rank(row.Priority) == (int)ItemPriority.Normal,
        ListFilter.LowPriority => Rank(row.Priority) == (int)ItemPriority.Low,
        _ => true
    };

    /// <summary>
    /// How much a priority is worth, as a number to sort by. An unrecognised value reads as Normal,
    /// which is what a row saved by a newer client should look like rather than last - the same rule
    /// PriorityChoice.For applies.
    /// </summary>
    private static int Rank(string priority)
        => Enum.TryParse<ItemPriority>(priority, ignoreCase: true, out var known)
            ? (int)known
            : (int)ItemPriority.Normal;
}
