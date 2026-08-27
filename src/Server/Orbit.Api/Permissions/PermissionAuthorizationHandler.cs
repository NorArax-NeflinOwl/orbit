using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Orbit.Core.Permissions;

namespace Orbit.Api.Permissions;

/// <summary>
/// Reads what the caller has been granted and decides on that alone. Nothing is inferred from the token:
/// a permission granted or taken away takes effect on the next request rather than when the token next
/// expires, which is the difference between revoking access and asking somebody to sign out first.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserPermissionRepository _userPermissionRepository;

    public PermissionAuthorizationHandler(IUserPermissionRepository userPermissionRepository)
    {
        _userPermissionRepository = userPermissionRepository;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is not { } subject
            || !Guid.TryParse(subject, out var userId))
        {
            return;
        }

        var granted = await _userPermissionRepository.GetForUserAsync(userId, CancellationToken.None);
        // Not "is it held" but "does it apply": group chat and sharing both rest on chat, and holding
        // one without it grants nothing - see PermissionPrerequisites.
        if (requirement.Permission.IsEffective(granted))
        {
            context.Succeed(requirement);
        }
    }
}
