namespace Orbit.Contracts.Tasks;

/// <summary>IsGroup marks a list that gathers the lists its items link to - see Orbit.Core.Tasks.TaskList.IsGroup.</summary>
public sealed record UpdateTaskRequest(string Title, IReadOnlyList<TaskItemRequest> Items, bool IsGroup = false);
