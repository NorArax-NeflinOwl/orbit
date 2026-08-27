namespace Orbit.Data.Entities;

/// <summary>
/// The code that unlocks one permission - see Orbit.Core.Permissions.PermissionCode. One row per
/// permission, made once and kept, so it can be read with a plain SELECT and survives a redeploy.
/// </summary>
public sealed class PermissionCodeEntity
{
    /// <summary>The ApplicationPermission name, and the key: one code per permission, never two.</summary>
    public string Permission { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
