namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a folder - see <see cref="Orbit.Core.Folders.Folder"/>. Only folders somebody
/// made themselves are rows: the two built-in ones (Public and Private) are decided from what an item
/// already is and have nothing stored, which is why this table starts empty for every account and why
/// nothing had to be backfilled when folders arrived.
/// </summary>
public sealed class FolderEntity
{
    public Guid Id { get; set; }

    /// <summary>Its one owner. A folder is never shared - see Orbit.Core.Folders.Folder.</summary>
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
