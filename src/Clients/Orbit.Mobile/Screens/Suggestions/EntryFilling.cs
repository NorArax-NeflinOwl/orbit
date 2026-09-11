using Orbit.Core.Tasks;

namespace Orbit.Mobile.Screens.Suggestions;

/// <summary>
/// Where this device keeps which kinds of entry a picked name fills in - see <see cref="EntryFilling"/>.
/// An interface for the reason every device preference here has one: the screens that read it are view
/// models this project tests.
/// </summary>
public interface IEntryFillingStore
{
    /// <summary>The kinds stored, or null when nothing has been - which means every kind.</summary>
    IReadOnlySet<string>? Read();

    void Write(IReadOnlySet<string> kinds);
}

/// <summary>
/// Which kinds of task entry a name picked from the suggestions fills in and makes the same thing as what
/// it names - see Orbit.Core.Tasks.TaskItem.ReferencesTaskItemId. Every kind until somebody says
/// otherwise, the user's rule; a kind left out still takes the name's words and nothing else. Kept on the
/// device, as Orbit.Web keeps its own (DevicePreferences.KindsFilledFromSuggestions), and set on the
/// account screen's Preferences tab.
/// </summary>
public sealed class EntryFilling(IEntryFillingStore store)
{
    /// <summary>The kinds of entry there are, in the order the form's kind picker lists them.</summary>
    public static readonly IReadOnlyList<string> EntryKinds =
    [
        nameof(TaskItemKind.Checklist),
        nameof(TaskItemKind.Calendar),
        nameof(TaskItemKind.Location),
        nameof(TaskItemKind.Inventory)
    ];

    public IReadOnlySet<string> Kinds => store.Read() ?? new HashSet<string>(EntryKinds);

    public bool Fills(string kind) => Kinds.Contains(kind);

    public void SetFills(string kind, bool fills)
    {
        var kinds = new HashSet<string>(Kinds);
        if (fills)
        {
            kinds.Add(kind);
        }
        else
        {
            kinds.Remove(kind);
        }

        store.Write(kinds);
    }
}
