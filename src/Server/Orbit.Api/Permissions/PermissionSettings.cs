namespace Orbit.Api.Permissions;

/// <summary>
/// The one secret every unlock code is derived from - see Orbit.Core.Permissions.PermissionCodeAuthority.
/// Set from the environment (Permissions__Secret), never committed: the CI workflow generates a fresh
/// one on each deploy and prints the resulting codes in the run summary. Left unset, the server makes
/// one up at startup and logs the codes, which is what local development runs on.
/// </summary>
public sealed class PermissionSettings
{
    public const string SectionName = "Permissions";

    public string Secret { get; set; } = string.Empty;
}
