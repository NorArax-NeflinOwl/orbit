using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One item of a task list, as the detail screen shows it. Carries the whole item rather than a few of
/// its fields, because tapping it now opens the rest - see <see cref="TaskItemEditor"/>.
/// </summary>
/// <param name="Detail">
/// Already in the reader's language: when it is due, and whether it says anything about being late or
/// repeats daily. Empty when the entry is only a line of text, which most are.
/// </param>
/// <param name="References">
/// Where an inventory errand points - the shelf it is about, and any other list asking for the same
/// product. Empty for every other kind of entry, which is most of them.
/// </param>
/// <param name="IsWaitingToReachTheServer">
/// True while this entry stands for something made on this phone that the server has not been told
/// about yet - today an appointment written with no connection. Said on the row rather than left to be
/// discovered: an appointment nobody else can see yet is a different thing from one they can, and the
/// difference is invisible otherwise.
/// </param>
public sealed record TaskItemRow(
    TaskItemDto Item, string Detail, bool IsOverdue, IReadOnlyList<TaskItemReference> References,
    bool IsWaitingToReachTheServer = false)
{
    public static TaskItemRow From(
        TaskItemDto item, Translations translations, DateTimeOffset nowUtc,
        IReadOnlyList<TaskItemReference>? references = null, bool isWaitingToReachTheServer = false)
        => new(
            item,
            Describe(item, translations),
            // Only worth saying about something still to do: a finished entry cannot be late any more.
            !item.IsCompleted && item.DueDateUtc is { } due && due < nowUtc,
            references ?? [],
            isWaitingToReachTheServer);

    public Guid Id => Item.Id;

    public string Description => Item.Description;

    public bool IsCompleted => Item.IsCompleted;

    public string CompletionMark => IsCompleted ? "✓" : "○";

    public bool HasDetail => Detail.Length > 0;

    public bool HasReferences => References.Count > 0;

    /// <summary>The other half of the pair - said as plainly as the waiting one, so neither is a puzzle.</summary>
    public bool HasReachedTheServer => !IsWaitingToReachTheServer && Item.Kind == nameof(TaskItemKind.Calendar);

    private static string Describe(TaskItemDto item, Translations translations)
    {
        var parts = new List<string>();
        if (item.DueDateUtc is { } due)
        {
            parts.Add(translations.Format(
                "Due {0}", due.LocalDateTime.ToString("d", translations.DisplayCulture)));
        }

        if (item.RemindDaily)
        {
            parts.Add(translations.Format(
                "Daily at {0}", item.DailyReminderTimeOfDay.ToString("t", translations.DisplayCulture)));
        }

        return string.Join(" · ", parts);
    }
}
