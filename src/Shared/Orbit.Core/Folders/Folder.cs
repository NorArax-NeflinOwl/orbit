using Orbit.Core;
using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders;

/// <summary>
/// A tab on the pages made of cards - the dashboard, the notes and the task lists - and the one thing a
/// note or a list belongs to besides its owner. Owned by exactly one user and never shared: a folder is
/// where somebody keeps their own things, so a list shared with a second person sits in whichever folder
/// each of them put it in, and neither sees the other's.
///
/// Three folders exist without a row of their own - Public, Private and Done, see
/// <see cref="BuiltInFolder"/> - so an account that has never made a folder still has somewhere for
/// everything to be, and the tabs are there on the first visit rather than after a setup step. Only what
/// somebody made themselves is stored here.
/// </summary>
public sealed class Folder
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private Folder(Guid id, Guid userId, string name, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        Name = name;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static Folder Create(Guid userId, string name)
    {
        var trimmed = EnsureNamed(name);
        var now = DateTimeOffset.UtcNow;
        return new Folder(Guid.NewGuid(), userId, trimmed, now, now);
    }

    /// <summary>Rebuilds a folder from already-persisted values, bypassing creation rules.</summary>
    public static Folder FromPersistence(
        Guid id, Guid userId, string name, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
        => new(id, userId, name, createdAtUtc, updatedAtUtc);

    public void Rename(string name)
    {
        Name = EnsureNamed(name);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// A tab has to say something, and it is the only thing a folder is - a nameless one would draw as a
    /// gap between two tabs that nobody could tell from a rendering fault. Trimmed here rather than at
    /// the edge so every caller stores the same name for "  Work  ".
    /// </summary>
    private static string EnsureNamed(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidRequestException("A folder needs a name.");
        }

        StoredTextLimits.OrRefuse(trimmed, StoredTextLimits.Title, "folder's name");
        return trimmed;
    }
}
