using System.Text.Json;

namespace Orbit.Data.Repositories;

/// <summary>
/// A note's or a task list's tags as their one JSON column holds them - see NoteEntity.TagsJson. Read
/// leniently: a stored row must never throw while being read, so anything that is not a list of words
/// reads as no tags rather than as an error that would make every row beside it unreachable.
/// </summary>
internal static class StoredTags
{
    public static IReadOnlyList<string> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Write(IReadOnlyList<string> tags) => JsonSerializer.Serialize(tags);
}
