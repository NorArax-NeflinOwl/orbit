namespace Orbit.Contracts.Users;

/// <summary>
/// What one account may use. Names match Orbit.Core.Permissions.ApplicationPermission: "Location",
/// "Chat", "GroupChat", "Sharing".
/// </summary>
public sealed record UserPermissionsDto(IReadOnlyList<string> Granted);

/// <summary>Trades a code typed in the Debug tab for whatever permission it unlocks.</summary>
public sealed record RedeemPermissionCodeRequest(string Code);

/// <summary>
/// The permission a code unlocked. Granted is null when the code matched nothing, and
/// MissingPrerequisite names what has to be unlocked first when the code was real but came too early -
/// see Orbit.Core.Permissions.PermissionPrerequisites.
/// </summary>
public sealed record RedeemPermissionCodeResultDto(string? Granted, string? MissingPrerequisite = null);
