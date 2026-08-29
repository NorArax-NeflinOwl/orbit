namespace Orbit.Data.Entities;

/// <summary>
/// The code that unlocks one permission - see Orbit.Core.Permissions.PermissionCode. One row per
/// permission, so it can be read with a plain SELECT, survives a redeploy, and can be rotated by
/// rewriting the row - by the application or by hand.
/// </summary>
public sealed class PermissionCodeEntity
{
    /// <summary>The ApplicationPermission name, and the key: one code per permission, never two.</summary>
    public string Permission { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    /// <summary>When the code standing in this row was made. A rotation moves it forward.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}
