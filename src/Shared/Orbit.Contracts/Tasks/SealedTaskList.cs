namespace Orbit.Contracts.Tasks;

/// <inheritdoc cref="Orbit.Contracts.Notes.SealedNote"/>
public sealed record SealedTaskList(string Title, IReadOnlyList<TaskItemDto> Items);
