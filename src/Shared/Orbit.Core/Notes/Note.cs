using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes;

/// <summary>
/// A single note owned by a user: a title and content, ordered lines of either plain text or checklist
/// items (see NoteContentLine).
/// </summary>
public sealed class Note
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; }
    public IReadOnlyList<NoteContentLine> Content { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// True for a copy created by accepting another user's share offer (see NoteShare and
    /// AcceptNoteShareCommand) - false for a note the owner created themselves. <see cref="Update"/>
    /// refuses to change a shared copy whose <see cref="AccessLevel"/> is <see cref="ShareAccessLevel.ReadOnly"/>.
    /// </summary>
    public bool IsShared { get; private set; }

    /// <summary>The sharing user's login, captured once at share-acceptance time. Null when IsShared is false.</summary>
    public string? SharedByUserName { get; private set; }

    /// <summary>The access level the share was accepted under - meaningless when IsShared is false.</summary>
    public ShareAccessLevel AccessLevel { get; private set; }

    private Note(
        Guid id, Guid userId, string title, IReadOnlyList<NoteContentLine> content, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
    }

    public static Note Create(Guid userId, string title, IReadOnlyList<NoteContentLine> content)
    {
        var now = DateTimeOffset.UtcNow;
        return new Note(Guid.NewGuid(), userId, title, content, now, now, isShared: false, sharedByUserName: null, ShareAccessLevel.ReadOnly);
    }

    /// <summary>
    /// Creates recipientUserId's own copy of title/content once they accept a share - see
    /// AcceptNoteShareCommandHandler.
    /// </summary>
    public static Note CreateShared(
        Guid recipientUserId, string title, IReadOnlyList<NoteContentLine> content, string sharedByUserName, ShareAccessLevel accessLevel)
    {
        var now = DateTimeOffset.UtcNow;
        return new Note(Guid.NewGuid(), recipientUserId, title, content, now, now, isShared: true, sharedByUserName, accessLevel);
    }

    /// <summary>
    /// Rebuilds a note from already-persisted values, bypassing creation rules.
    /// </summary>
    public static Note FromPersistence(
        Guid id, Guid userId, string title, IReadOnlyList<NoteContentLine> content, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
        => new(id, userId, title, content, createdAtUtc, updatedAtUtc, isShared, sharedByUserName, accessLevel);

    public void Update(string title, IReadOnlyList<NoteContentLine> content)
    {
        if (IsShared && AccessLevel == ShareAccessLevel.ReadOnly)
        {
            throw new InvalidOperationException("A shared, read-only note can't be edited.");
        }

        Title = title;
        Content = content;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
