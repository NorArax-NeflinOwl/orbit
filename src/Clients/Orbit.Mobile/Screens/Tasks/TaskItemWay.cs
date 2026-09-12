using CommunityToolkit.Mvvm.ComponentModel;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One way of getting an entry done, while the entry's form edits it - see TaskItemEditor.Ways and
/// Orbit.Core.Tasks.TaskItem.Alternatives. Either a line of its own, typed and ticked here, or another
/// list, named and nothing more: that list is where it is done.
/// </summary>
public sealed partial class TaskItemWay : ObservableObject
{
    public TaskItemWay(string description, Guid? listServerId, string? listName, bool isDone)
    {
        _description = description;
        _isDone = isDone;
        ListServerId = listServerId;
        ListName = listName ?? string.Empty;
    }

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isDone;

    /// <summary>The list this way is, or null for a line of its own.</summary>
    public Guid? ListServerId { get; }

    /// <summary>What that list is called, for the form to show. Empty for a line.</summary>
    public string ListName { get; }

    public bool IsAList => ListServerId is not null;

    public bool IsALine => !IsAList;
}
