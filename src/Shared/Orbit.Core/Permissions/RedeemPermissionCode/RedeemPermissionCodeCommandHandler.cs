using Orbit.Core.Abstractions;

namespace Orbit.Core.Permissions.RedeemPermissionCode;

/// <summary>
/// Returns the permission that was granted, or null when the code matched nothing. Redeeming a code for
/// something the account already holds still reports success: the person typed a valid code and the
/// account can now use that part of Orbit, which is the only thing they asked about.
/// </summary>
public sealed class RedeemPermissionCodeCommandHandler : IRequestHandler<RedeemPermissionCodeCommand, RedeemPermissionCodeOutcome>
{
    private readonly IUserPermissionRepository _userPermissionRepository;
    private readonly PermissionCodeAuthority _permissionCodeAuthority;

    public RedeemPermissionCodeCommandHandler(
        IUserPermissionRepository userPermissionRepository, PermissionCodeAuthority permissionCodeAuthority)
    {
        _userPermissionRepository = userPermissionRepository;
        _permissionCodeAuthority = permissionCodeAuthority;
    }

    public async Task<RedeemPermissionCodeOutcome> HandleAsync(RedeemPermissionCodeCommand request, CancellationToken cancellationToken)
    {
        if (_permissionCodeAuthority.Match(request.Code) is not { } permission)
        {
            return new RedeemPermissionCodeOutcome(Granted: null, MissingPrerequisite: null);
        }

        var granted = await _userPermissionRepository.GetForUserAsync(request.UserId, cancellationToken);
        // Refused rather than stored-and-inert: a code that appeared to work and changed nothing would
        // be worse than being told what to unlock first.
        if (permission.RequiredBefore() is { } required && !granted.Contains(required))
        {
            return new RedeemPermissionCodeOutcome(Granted: null, MissingPrerequisite: required);
        }

        await _userPermissionRepository.GrantAsync(request.UserId, permission, cancellationToken);
        return new RedeemPermissionCodeOutcome(permission, MissingPrerequisite: null);
    }
}
