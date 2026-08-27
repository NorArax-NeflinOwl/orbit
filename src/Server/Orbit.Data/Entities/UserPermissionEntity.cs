namespace Orbit.Data.Entities;

/// <summary>
/// One granted permission for one account - see Orbit.Core.Permissions.ApplicationPermission. A row per
/// grant rather than a column per permission on Users: "who holds what, and since when" is a question
/// that gets asked, and adding a fifth gated part of Orbit is then a change in code alone.
/// </summary>
public sealed class UserPermissionEntity
{
    public Guid UserId { get; set; }

    /// <summary>The ApplicationPermission name. Stored by name so a reordered enum cannot silently regrant something else.</summary>
    public string Permission { get; set; } = string.Empty;

    public DateTimeOffset GrantedAtUtc { get; set; }
}
