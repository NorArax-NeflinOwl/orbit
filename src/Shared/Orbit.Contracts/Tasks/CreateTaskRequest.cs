namespace Orbit.Contracts.Tasks;

public sealed record CreateTaskRequest(string Title, IReadOnlyList<TaskItemRequest> Items);
