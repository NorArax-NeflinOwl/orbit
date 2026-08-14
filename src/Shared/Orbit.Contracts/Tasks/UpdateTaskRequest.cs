namespace Orbit.Contracts.Tasks;

public sealed record UpdateTaskRequest(string Title, IReadOnlyList<TaskItemRequest> Items);
