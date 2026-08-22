namespace Orbit.Contracts.Tasks;

/// <summary>
/// IsShared/SharedByUserName/AccessLevel describe provenance, not content. AccessLevel is "ReadOnly" or
/// "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel) and is only meaningful when IsShared is true.
/// </summary>
public sealed record TaskDto(
    Guid Id, string Title, IReadOnlyList<TaskItemDto> Items, bool IsCompleted,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName, string AccessLevel);
