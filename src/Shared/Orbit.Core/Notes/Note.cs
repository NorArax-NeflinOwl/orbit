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

    /// <summary>
    /// The id of the user who first created this note, before any sharing - captured once at
    /// share-acceptance time from the source note's own <see cref="OriginalOwnerUserId"/> (or, if that
    /// source note wasn't itself shared, from its <see cref="UserId"/>), so it survives being re-shared
    /// through any number of hops. Null when IsShared is false, where <see cref="UserId"/> already is
    /// the original owner. ShareNoteCommandHandler uses this to stop a share ending up back with the
    /// person who originally owns it - see its class comment.
    /// </summary>
    public Guid? OriginalOwnerUserId { get; private set; }

    /// <summary>The original owner regardless of how many times this note has been re-shared since.</summary>
    public Guid EffectiveOwnerUserId => IsShared ? OriginalOwnerUserId!.Value : UserId;

    private Note(
        Guid id, Guid userId, string title, IReadOnlyList<NoteContentLine> content, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel, Guid? originalOwnerUserId)
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
        OriginalOwnerUserId = originalOwnerUserId;
    }

    public static Note Create(Guid userId, string title, IReadOnlyList<NoteContentLine> content)
    {
        var now = DateTimeOffset.UtcNow;
        return new Note(
            Guid.NewGuid(), userId, title, content, now, now,
            isShared: false, sharedByUserName: null, ShareAccessLevel.ReadOnly, originalOwnerUserId: null);
    }

    /// <summary>
    /// Creates recipientUserId's own copy of title/content once they accept a share - see
    /// AcceptNoteShareCommandHandler.
    /// </summary>
    public static Note CreateShared(
        Guid recipientUserId, string title, IReadOnlyList<NoteContentLine> content, string sharedByUserName, ShareAccessLevel accessLevel,
        Guid originalOwnerUserId)
    {
        var now = DateTimeOffset.UtcNow;
        return new Note(Guid.NewGuid(), recipientUserId, title, content, now, now, isShared: true, sharedByUserName, accessLevel, originalOwnerUserId);
    }

    /// <summary>
    /// Rebuilds a note from already-persisted values, bypassing creation rules.
    /// </summary>
    public static Note FromPersistence(
        Guid id, Guid userId, string title, IReadOnlyList<NoteContentLine> content, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel, Guid? originalOwnerUserId)
        => new(id, userId, title, content, createdAtUtc, updatedAtUtc, isShared, sharedByUserName, accessLevel, originalOwnerUserId);

    public void Update(string title, IReadOnlyList<NoteContentLine> content)
    {
        if (IsShared && AccessLevel != ShareAccessLevel.CanEdit)
        {
            throw new InvalidOperationException("A shared note without CanEdit access can't be edited.");
        }

        Title = title;
        Content = content;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
