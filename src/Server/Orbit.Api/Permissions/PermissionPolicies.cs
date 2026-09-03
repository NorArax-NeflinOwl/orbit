using Microsoft.AspNetCore.Authorization;
using Orbit.Core.Permissions;

namespace Orbit.Api.Permissions;

/// <summary>The authorization requirement behind one <see cref="ApplicationPermission"/>.</summary>
public sealed class PermissionRequirement(ApplicationPermission permission) : IAuthorizationRequirement
{
    public ApplicationPermission Permission { get; } = permission;
}

/// <summary>
/// One policy per gated part of Orbit, named after the permission it needs. Applied to whole route
/// groups where a group happens to be exactly one feature, and endpoint by endpoint where it does not -
/// sharing, for instance, lives across notes, task lists, calendar events and warehouses. An endpoint
/// that needs two (sharing a position needs Location and Contacts both) names both policies, and both
/// have to pass.
/// </summary>
public static class PermissionPolicies
{
    public const string Contacts = nameof(ApplicationPermission.Contacts);
    public const string Chat = nameof(ApplicationPermission.Chat);
    public const string Sharing = nameof(ApplicationPermission.Sharing);
    public const string Location = nameof(ApplicationPermission.Location);

    /// <summary>Declared for completeness; nothing names it, because Debug gates no endpoint - see ApplicationPermission.Debug.</summary>
    public const string Debug = nameof(ApplicationPermission.Debug);

    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var permission in Enum.GetValues<ApplicationPermission>())
        {
            options.AddPolicy(permission.ToString(), policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
        }
    }
}
