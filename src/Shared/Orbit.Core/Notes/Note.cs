namespace Orbit.Core.Notes;

/// <summary>
/// A single note owned by a user: a title and free-form content.
/// </summary>
public sealed class Note
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; }
    public string Content { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private Note(Guid id, Guid userId, string title, string content, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static Note Create(Guid userId, string title, string content)
    {
        var now = DateTimeOffset.UtcNow;
        return new Note(Guid.NewGuid(), userId, title, content, now, now);
    }

    /// <summary>
    /// Rebuilds a note from already-persisted values, bypassing creation rules.
    /// </summary>
    public static Note FromPersistence(Guid id, Guid userId, string title, string content, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
        => new(id, userId, title, content, createdAtUtc, updatedAtUtc);

    public void Update(string title, string content)
    {
        Title = title;
        Content = content;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
