using Orbit.Core.Tasks;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// What one entry on a list is - something to fetch, or somewhere to be. The wire wants Orbit.Core's own
/// word, and a reader wants it in their own language, which is the same split
/// <see cref="NotificationChannelChoice"/> makes. See <see cref="TaskItemKind"/> for why the kind sits
/// on the entry rather than on the list.
/// </summary>
public sealed record TaskItemKindChoice(string Value, string Name)
{
    public static IReadOnlyList<TaskItemKindChoice> All(Translations translations)
        =>
        [
            new(nameof(TaskItemKind.Checklist), translations["Checklist"]),
            new(nameof(TaskItemKind.Calendar), translations["Calendar"])
        ];

    /// <summary>The one whose wire value this is, or the first - a stored value is always one of them.</summary>
    public static TaskItemKindChoice For(IReadOnlyList<TaskItemKindChoice> all, string value)
        => all.FirstOrDefault(choice => choice.Value == value) ?? all[0];
}
