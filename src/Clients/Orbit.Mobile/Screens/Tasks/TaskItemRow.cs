using Orbit.Contracts.Tasks;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>One item of a task list, as the detail screen shows it.</summary>
public sealed record TaskItemRow(Guid Id, string Description, bool IsCompleted, DateTimeOffset? DueDateUtc)
{
    public static TaskItemRow From(TaskItemDto item)
        => new(item.Id, item.Description, item.IsCompleted, item.DueDateUtc);

    public string CompletionMark => IsCompleted ? "✓" : "○";

    public bool HasDueDate => DueDateUtc is not null;
}
