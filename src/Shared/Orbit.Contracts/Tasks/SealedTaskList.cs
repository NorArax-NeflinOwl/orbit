namespace Orbit.Contracts.Tasks;

/// <inheritdoc cref="Orbit.Contracts.Notes.SealedNote"/>
/// <param name="Tags">
/// A private list's tags, which the server keeps none of for it - see TaskDto.Tags. Defaulted and last:
/// a payload sealed before tags existed says nothing here, and opens as a list with no tags.
/// </param>
public sealed record SealedTaskList(string Title, IReadOnlyList<TaskItemDto> Items, IReadOnlyList<string>? Tags = null);
