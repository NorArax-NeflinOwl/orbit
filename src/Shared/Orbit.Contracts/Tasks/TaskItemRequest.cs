namespace Orbit.Contracts.Tasks;

public sealed record TaskItemRequest(string Description, DateTimeOffset? DueDateUtc, bool IsCompleted);
