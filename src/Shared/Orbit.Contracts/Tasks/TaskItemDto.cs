namespace Orbit.Contracts.Tasks;

public sealed record TaskItemDto(Guid Id, string Description, DateTimeOffset? DueDateUtc, bool IsCompleted);
