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
/// sharing, for instance, lives across notes, task lists, calendar events and warehouses.
/// </summary>
public static class PermissionPolicies
{
    public const string Location = nameof(ApplicationPermission.Location);
    public const string Chat = nameof(ApplicationPermission.Chat);
    public const string GroupChat = nameof(ApplicationPermission.GroupChat);
    public const string Sharing = nameof(ApplicationPermission.Sharing);

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
