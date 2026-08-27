using Orbit.Core.Abstractions;

namespace Orbit.Core.Permissions.RedeemPermissionCode;

/// <summary>
/// Returns the permission that was granted, or null when the code matched nothing. Redeeming a code for
/// something the account already holds still reports success: the person typed a valid code and the
/// account can now use that part of Orbit, which is the only thing they asked about.
/// </summary>
public sealed class RedeemPermissionCodeCommandHandler : IRequestHandler<RedeemPermissionCodeCommand, ApplicationPermission?>
{
    private readonly IUserPermissionRepository _userPermissionRepository;
    private readonly PermissionCodeAuthority _permissionCodeAuthority;

    public RedeemPermissionCodeCommandHandler(
        IUserPermissionRepository userPermissionRepository, PermissionCodeAuthority permissionCodeAuthority)
    {
        _userPermissionRepository = userPermissionRepository;
        _permissionCodeAuthority = permissionCodeAuthority;
    }

    public async Task<ApplicationPermission?> HandleAsync(RedeemPermissionCodeCommand request, CancellationToken cancellationToken)
    {
        if (_permissionCodeAuthority.Match(request.Code) is not { } permission)
        {
            return null;
        }

        await _userPermissionRepository.GrantAsync(request.UserId, permission, cancellationToken);
        return permission;
    }
}
