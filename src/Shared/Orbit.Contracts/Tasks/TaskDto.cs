namespace Orbit.Contracts.Tasks;

public sealed record TaskDto(
    Guid Id, string Title, IReadOnlyList<TaskItemDto> Items, bool IsCompleted,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
